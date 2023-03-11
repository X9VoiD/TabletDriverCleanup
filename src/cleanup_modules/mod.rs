use core::result::Result as CResult;
use std::{
    error::Error,
    fmt::Display,
    fs::File,
    path::{Path, PathBuf},
};

use crate::{services::terminal, State};
use async_trait::async_trait;
use error_stack::{Context, IntoReport, Report, Result, ResultExt};
use thiserror::Error;

mod device_cleanup;
mod driver_cleanup;
mod driver_package_cleanup;

pub use device_cleanup::DeviceCleanupModule;
pub use driver_cleanup::DriverCleanupModule;
pub use driver_package_cleanup::DriverPackageCleanupModule;

#[async_trait]
pub trait Module {
    fn name(&self) -> &str;
    fn cli_name(&self) -> &str;
    fn help(&self) -> &str;
    async fn run(&mut self, state: &State) -> Result<ModuleRunInfo, ModuleError>;
    fn get_dumper(&self) -> Option<&dyn Dumper>;
}

#[derive(Debug, Error)]
#[error("module '{name}' has encountered issues while running")]
pub struct ModuleError {
    name: &'static str,
}

#[derive(Debug, Error)]
pub(crate) enum UninstallError {
    #[error("Failed to uninstall {0}")]
    UninstallFailed(&'static str),
    #[error("{0} is already uninstalled")]
    AlreadyUninstalled(&'static str),
}

impl UninstallError {
    fn failed<T>(uninstall_object: &T) -> Self
    where
        T: Display,
    {
        let str: &'static str = Box::leak(uninstall_object.to_string().into_boxed_str());
        Self::UninstallFailed(str)
    }

    fn uninstalled<T>(uninstall_object: &T) -> Self
    where
        T: Display,
    {
        let str: &'static str = Box::leak(uninstall_object.to_string().into_boxed_str());
        Self::AlreadyUninstalled(str)
    }
}

trait ToUninstall<T> {
    fn matches(&self, other: &T) -> bool;
}

trait ModuleMetadata {
    fn name(&self) -> &str;
    fn cli_name(&self) -> &str;
    fn help(&self) -> &str;
    fn noun(&self) -> &str;
}

#[async_trait]
trait ModuleStrategy {
    type Object: std::fmt::Display + Sync + Send;
    type ToUninstall: ToUninstall<Self::Object> + std::fmt::Display + Sync + Send;

    async fn initialize(&mut self, state: &State) -> Result<(), ModuleError>;
    fn get_objects(&self) -> Result<Vec<Self::Object>, ModuleError>;
    fn get_objects_to_uninstall(&self) -> &[Self::ToUninstall];
    async fn uninstall_object(
        &self,
        object: Self::Object,
        to_uninstall: &Self::ToUninstall,
        state: &State,
        run_info: &mut ModuleRunInfo,
    ) -> Result<(), UninstallError>;
    fn get_dumper(&self) -> Option<&dyn Dumper>;
}

#[async_trait]
impl<T> Module for T
where
    T: ModuleMetadata + ModuleStrategy + Sync + Send,
{
    fn name(&self) -> &str {
        self.name()
    }

    fn cli_name(&self) -> &str {
        self.cli_name()
    }

    fn help(&self) -> &str {
        self.help()
    }

    async fn run(&mut self, state: &State) -> Result<ModuleRunInfo, ModuleError> {
        self.initialize(state).await?;
        let objects = self.get_objects()?;
        let objects_to_uninstall = self.get_objects_to_uninstall();
        let mut module_run_info = ModuleRunInfo::default();

        let mut found = false;
        for object in objects {
            let object_to_uninstall = match should_uninstall(&object, objects_to_uninstall) {
                Some(object_to_uninstall) => object_to_uninstall,
                None => continue,
            };

            found = true;
            if state.interactive && !state.dry_run {
                let prompt =
                    terminal::prompt_yes_no(&format!("Uninstall '{}'?", object_to_uninstall));

                match prompt {
                    terminal::PromptResult::No => {
                        println!("Skipping '{}'...", object_to_uninstall);
                        continue;
                    }
                    terminal::PromptResult::Cancel => {
                        println!("Aborting...");
                        std::process::exit(0);
                    }
                    _ => {}
                }
            }

            println!("Uninstalling '{}'...", object_to_uninstall);
            if !state.dry_run {
                let ret = &self
                    .uninstall_object(object, object_to_uninstall, state, &mut module_run_info)
                    .await;

                if let Err(err) = ret {
                    eprintln!("{:?}", err);
                }
            }
        }

        if !found {
            println!("No {} to uninstall is found.", self.noun());
        }

        Ok(module_run_info)
    }

    fn get_dumper(&self) -> Option<&dyn Dumper> {
        self.get_dumper()
    }
}

fn should_uninstall<'a, T, U>(object: &T, objects_to_uninstall: &'a [U]) -> Option<&'a U>
where
    U: ToUninstall<T>,
{
    objects_to_uninstall
        .iter()
        .find(|&object_to_uninstall| object_to_uninstall.matches(object))
}

#[derive(Default)]
pub struct ModuleRunInfo {
    pub reboot_required: bool,
}

#[async_trait]
pub trait Dumper {
    async fn dump(&self, state: &State) -> Result<(), ModuleError>;
}

fn get_path_to_dump(state: &State, filename: &str) -> Result<PathBuf, std::io::Error> {
    let dump_path = Path::join(&state.current_path, "dumps");
    if !dump_path.exists() {
        std::fs::create_dir_all(&dump_path)
            .into_report()
            .attach_printable_lazy(|| format!("cannot create path '{}'", dump_path.display()))?;
    }

    let file_path = Path::join(&dump_path, filename);

    Ok(file_path)
}

fn create_dump_file(path: &Path) -> Result<File, std::io::Error> {
    let file = File::create(path)
        .into_report()
        .attach_printable_lazy(|| format!("cannot create file '{}'", path.display()))?;

    Ok(file)
}

pub(crate) trait IntoModuleReport<T> {
    fn into_module_report(self, module_name: &'static str) -> Result<T, ModuleError>;
}

impl<T, E> IntoModuleReport<T> for CResult<T, Report<E>>
where
    E: Error + Context + Display,
{
    fn into_module_report(self, module_name: &'static str) -> Result<T, ModuleError> {
        self.change_context_lazy(|| ModuleError { name: module_name })
    }
}

pub(crate) trait IntoUninstallReport<T, T2> {
    fn into_uninstall_report(self, uninstall_object: &T2) -> Result<T, UninstallError>;
}

impl<T, T2, E> IntoUninstallReport<T, T2> for CResult<T, Report<E>>
where
    T2: Display,
    E: Error + Context + Display,
{
    fn into_uninstall_report(self, uninstall_object: &T2) -> Result<T, UninstallError> {
        self.change_context_lazy(|| UninstallError::failed(uninstall_object))
    }
}
