use core::result::Result as CResult;
use std::{
    future::Future,
    io::{ErrorKind, Write},
    path::Path,
    process::{Child, ExitStatus},
};

use async_trait::async_trait;
use error_stack::{bail, IntoReport, Result, ResultExt};
use lazy_static::lazy_static;
use regex::Regex;
use serde::Deserialize;
use tokio_util::sync::CancellationToken;
use winreg::{enums::HKEY_LOCAL_MACHINE, RegKey};
use wmi::{COMLibrary, WMIConnection, WMIError};

use super::{
    create_dump_file, Dumper, IntoModuleReport, ModuleError, ModuleMetadata, ModuleRunInfo,
    ModuleStrategy, ToUninstall, UninstallError,
};
use crate::{
    cleanup_modules::get_path_to_dump,
    services::{
        self, identifiers, regex_cache, terminal,
        windows::{enumerate_driver_packages, DriverPackage},
    },
    State,
};

const MODULE_NAME: &str = "Driver Package Cleanup";
const MODULE_CLI: &str = "driver-package-cleanup";
const IDENTIFIER: &str = "driver_package_identifiers.json";

#[derive(Default)]
pub struct DriverPackageCleanupModule {
    objects_to_uninstall: Vec<DriverPackageToUninstall>,
    dumper: DriverPackageDumper,
}

impl DriverPackageCleanupModule {
    pub fn new() -> Self {
        Self::default()
    }
}

impl ModuleMetadata for DriverPackageCleanupModule {
    fn name(&self) -> &str {
        MODULE_NAME
    }

    fn cli_name(&self) -> &str {
        MODULE_CLI
    }

    fn help(&self) -> &str {
        "uninstall driver software packages"
    }

    fn noun(&self) -> &str {
        "driver packages"
    }
}

#[async_trait]
impl ModuleStrategy for DriverPackageCleanupModule {
    type Object = DriverPackage;
    type ToUninstall = DriverPackageToUninstall;

    async fn initialize(&mut self, state: &State) -> Result<(), ModuleError> {
        let _name = self.name().to_string();
        let resource = identifiers::get_resource(IDENTIFIER, state)
            .await
            .into_module_report(MODULE_NAME)?;
        let driver_packages_raw = resource.get_content();
        let driver_packages: Vec<DriverPackageToUninstall> =
            serde_json::from_slice(driver_packages_raw)
                .into_report()
                .into_module_report(MODULE_NAME)?;
        self.objects_to_uninstall = driver_packages;
        Ok(())
    }

    fn get_objects(&self) -> Result<Vec<Self::Object>, ModuleError> {
        services::windows::enumerate_driver_packages().into_module_report(MODULE_NAME)
    }

    fn get_objects_to_uninstall(&self) -> &[Self::ToUninstall] {
        self.objects_to_uninstall.as_slice()
    }

    async fn uninstall_object(
        &self,
        object: Self::Object,
        to_uninstall: &Self::ToUninstall,
        state: &State,
        _run_info: &mut ModuleRunInfo,
    ) -> Result<(), UninstallError> {
        use UninstallMethod::*;

        match &to_uninstall.uninstall_method {
            Normal => run_uninstall_method(uninstall_normal, state, &object, to_uninstall).await,
            Deferred => {
                run_uninstall_method(uninstall_deferred, state, &object, to_uninstall).await
            }
            RegistryOnly => uninstall_registry_only(object, to_uninstall),
        }
    }

    fn get_dumper(&self) -> Option<&dyn Dumper> {
        Some(&self.dumper)
    }
}

fn uninstall_registry_only(
    object: DriverPackage,
    to_uninstall: &DriverPackageToUninstall,
) -> Result<(), UninstallError> {
    let base_key_name = if object.x86() {
        "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
    } else {
        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
    };

    let flags = winreg::enums::KEY_WRITE;
    let uninstall_key = RegKey::predef(HKEY_LOCAL_MACHINE)
        .open_subkey_with_flags(base_key_name, flags)
        .into_report()
        .change_context(UninstallError::UninstallFailed)
        .attach_printable_lazy(|| {
            format!(
                "failed to open uninstall key for driver package '{}'",
                to_uninstall.friendly_name
            )
        })?;

    uninstall_key
        .delete_subkey_all(Path::new(object.key_name()))
        .into_report()
        .attach_printable_lazy(|| {
            object.key_name().to_string()
        })
        .change_context(UninstallError::UninstallFailed)
        .attach_printable_lazy(|| {
            format!(
                "failed to delete uninstall key for driver package '{}'",
                to_uninstall.friendly_name
            )
        })
}

#[derive(Default)]
struct DriverPackageDumper {}

#[async_trait]
impl Dumper for DriverPackageDumper {
    async fn dump(&self, state: &State) -> Result<(), ModuleError> {
        let driver_packages: Vec<DriverPackage> = enumerate_driver_packages()
            .into_module_report(MODULE_NAME)?
            .into_iter()
            .filter(is_of_interest)
            .collect();

        let file_path =
            get_path_to_dump(state, "driver-packages.json").into_module_report(MODULE_NAME)?;
        let dump_file = create_dump_file(&file_path).into_module_report(MODULE_NAME)?;
        let file_name = file_path.file_name().unwrap().to_string_lossy();

        if driver_packages.is_empty() {
            println!("No driver packages to dump");
            return Ok(());
        }

        serde_json::to_writer_pretty(dump_file, &driver_packages)
            .into_report()
            .attach_printable_lazy(|| {
                format!("failed to dump driver packages into '{}'", file_name)
            })
            .into_module_report(MODULE_NAME)?;

        match driver_packages.len() {
            1 => println!("Dumped 1 driver package into '{}'", file_name),
            n => println!("Dumped {} driver packages into '{}'", n, file_name),
        }

        Ok(())
    }
}

fn is_of_interest(driver_package: &DriverPackage) -> bool {
    use crate::services::interest::is_of_interest_iter as candidate_iter;
    driver_package.display_name().is_some()
        && driver_package.uninstall_string().is_some()
        && candidate_iter(
            [
                driver_package.display_name(),
                driver_package.publisher(),
                driver_package.uninstall_string(),
            ]
            .into_iter()
            .flatten(),
        )
}

async fn run_uninstall_method<'a, T>(
    method: impl FnOnce(&'a State, &'a DriverPackage, &'a DriverPackageToUninstall, CancellationToken) -> T
        + 'a,
    state: &'a State,
    object: &'a DriverPackage,
    to_uninstall: &'a DriverPackageToUninstall,
) -> Result<(), UninstallError>
where
    T: Future<Output = Result<(), UninstallError>>,
{
    let ct = CancellationToken::new();
    if state.interactive {
        let _guard = terminal::enter_temp_print();
        let result: Result<(), UninstallError>;
        tokio::select! {
            ret = method(state, object, to_uninstall, ct.child_token()) => { result = ret },
            _ = wait_for_user(ct.child_token()) => { result = Ok(()) }
        }
        ct.cancel();
        result
    } else {
        let _guard = terminal::enter_temp_print();
        method(state, object, to_uninstall, ct.child_token()).await
    }
}

async fn uninstall_normal(
    _state: &State,
    object: &DriverPackage,
    _to_uninstall: &DriverPackageToUninstall,
    _ct: CancellationToken,
) -> Result<(), UninstallError> {
    let uninstall_string = object.uninstall_string().unwrap();
    let child_process = match to_command(uninstall_string).spawn() {
        Ok(child) => child,
        Err(err) => match err.kind() {
            ErrorKind::NotFound => bail!(UninstallError::AlreadyUninstalled),
            _ => {
                return Err(err)
                    .into_report()
                    .change_context(UninstallError::UninstallFailed)
                    .attach_printable_lazy(|| {
                        format!("failed to launch uninstaller: {}", uninstall_string)
                    })
            }
        },
    };

    wait_for_process_async(child_process)
        .await
        .into_report()
        .change_context(UninstallError::UninstallFailed)
        .attach_printable_lazy(|| {
            format!("failed to wait on child process, exe: {}", uninstall_string)
        })?;

    Ok(())
}

async fn uninstall_deferred(
    _state: &State,
    object: &DriverPackage,
    _to_uninstall: &DriverPackageToUninstall,
    _ct: CancellationToken,
) -> Result<(), UninstallError> {
    let uninstall_string = object.uninstall_string().unwrap();
    let mut command = to_command(uninstall_string);
    let target_dir = Path::new(command.get_program())
        .parent()
        .unwrap()
        .to_str()
        .unwrap()
        .to_string();

    let child = match command.spawn() {
        Ok(child) => child,
        Err(err) => match err.kind() {
            ErrorKind::NotFound => bail!(UninstallError::AlreadyUninstalled),
            _ => {
                return Err(err)
                    .into_report()
                    .change_context(UninstallError::UninstallFailed)
                    .attach_printable_lazy(|| {
                        format!("failed to launch uninstaller: {}", uninstall_string)
                    })
            }
        },
    };

    let id = child.id();

    tokio::time::sleep(std::time::Duration::from_secs_f32(0.5)).await;

    let processes = get_process_infos().unwrap();
    let process_delegate = processes
        .iter()
        .filter(|p| p.parent_process_id == id)
        .find(|p| {
            p.command_line
                .as_ref()
                .map_or(false, |p| p.contains(&target_dir))
        });

    if let Some(process_delegate) = process_delegate {
        let ct = CancellationToken::new();
        let results = tokio::join!(
            wait_for_process_async(child),
            services::windows::wait_for_process_async(
                process_delegate.process_id,
                Some(ct.child_token())
            )
        );
        match results {
            (Ok(_), Ok(_)) => {}
            (Err(err), _) => {
                return Err(err)
                    .into_report()
                    .change_context(UninstallError::UninstallFailed)
                    .attach_printable("failed to wait for main uninstaller process")
            }
            (_, Err(err)) => {
                return Err(err)
                    .change_context(UninstallError::UninstallFailed)
                    .attach_printable("failed to wait for uninstaller's delegated process")
            }
        }
        ct.cancel();
    } else {
        wait_for_process_async(child)
            .await
            .into_report()
            .change_context(UninstallError::UninstallFailed)
            .attach_printable("failed to wait for main uninstaller process")?;
    }

    Ok(())
}

async fn wait_for_user(ct: CancellationToken) {
    print!("Complete the uninstall process. If this message is not gone after uninstall is complete, then press any key to continue... ");
    std::io::stdout().flush().unwrap();
    terminal::read_key_async(Some(ct)).await.unwrap();
}

async fn wait_for_process_async(child: Child) -> CResult<ExitStatus, std::io::Error> {
    tokio::spawn(async move {
        let mut child = child;
        loop {
            match child.try_wait() {
                Ok(Some(exit_code)) => break Ok(exit_code),
                Ok(None) => tokio::time::sleep(std::time::Duration::from_millis(20)).await,
                Err(error) => break Err(error),
            }
        }
    })
    .await
    .unwrap()
}

#[derive(Deserialize, Debug)]
enum UninstallMethod {
    Normal,
    Deferred,
    RegistryOnly,
}

#[derive(Deserialize, Debug)]
#[serde(deny_unknown_fields)]
pub struct DriverPackageToUninstall {
    friendly_name: String,
    display_name: Option<String>,
    display_version: Option<String>,
    publisher: Option<String>,
    uninstall_method: UninstallMethod,
}

impl ToUninstall<DriverPackage> for DriverPackageToUninstall {
    fn matches(&self, other: &DriverPackage) -> bool {
        regex_cache::cached_match(other.display_name(), self.display_name.as_deref())
            && regex_cache::cached_match(other.display_version(), self.display_version.as_deref())
            && regex_cache::cached_match(other.publisher(), self.publisher.as_deref())
    }
}

impl std::fmt::Display for DriverPackageToUninstall {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.friendly_name)
    }
}

#[derive(Deserialize, Debug)]
#[serde(rename = "Win32_Process")]
#[serde(rename_all = "PascalCase")]
struct ProcessInfo {
    process_id: u32,
    parent_process_id: u32,
    command_line: Option<String>,
}

fn get_process_infos() -> CResult<Vec<ProcessInfo>, WMIError> {
    let wmi_con = WMIConnection::new(COMLibrary::new()?)?;
    wmi_con.query()
}

fn to_command(command: &str) -> std::process::Command {
    lazy_static! {
        static ref COMMAND_REGEX: Regex =
            Regex::new(r#""?(?P<command>.*?\.[a-zA-Z]{3})"?(?: (?P<args>.*)?)?"#).unwrap();
    }

    let captures = COMMAND_REGEX.captures(command).unwrap();
    let process = captures.name("command").unwrap().as_str();
    let args = captures.name("args");

    let mut command = std::process::Command::new(process);

    if let Some(args) = args {
        command.args(args.as_str().split(' '));
    }

    command
}
