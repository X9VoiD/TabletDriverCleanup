use core::{fmt::Debug, result::Result as CResult};
use std::{
    ffi::{c_void, OsStr, OsString},
    path::Path,
    time::Duration,
};

use error_stack::{bail, report, IntoReport, Result, ResultExt};
use lazy_static::lazy_static;
use regex::Regex;
use serde::Serialize;
use thiserror::Error;
use tokio_util::sync::CancellationToken;
use uuid::Uuid;
use windows::{
    core::{HRESULT, HSTRING},
    Win32::{
        Devices::{DeviceAndDriverInstallation::*, Properties::*},
        Foundation::*,
        Security::{GetTokenInformation, TokenElevation, TOKEN_ELEVATION, TOKEN_QUERY},
        System::Threading::{
            GetCurrentProcess, OpenProcess, OpenProcessToken, WaitForSingleObject,
            PROCESS_SYNCHRONIZE,
        },
    },
};
use winreg::RegKey;
use winreg::{enums::*, types::FromRegValue};

struct Handle {
    handle: HANDLE,
}

impl Handle {
    pub fn new(handle: HANDLE) -> Self {
        Self { handle }
    }
}

impl From<HANDLE> for Handle {
    fn from(handle: HANDLE) -> Self {
        Self::new(handle)
    }
}

impl Drop for Handle {
    fn drop(&mut self) {
        unsafe {
            CloseHandle(self.handle);
        }
    }
}

#[derive(Serialize)]
pub struct Device {
    is_generic: bool,
    instance_id: String,
    hardware_ids: Vec<String>,
    friendly_name: Option<String>,
    description: Option<String>,
    manufacturer: Option<String>,
    driver_name: Option<String>,
    class: Option<String>,
    class_guid: Uuid,
    inf_name: Option<String>,
    inf_original_name: Option<String>,
    inf_section: Option<String>,
    inf_provider: Option<String>,
    driver_store_location: Option<String>,
}

#[allow(dead_code)]
#[allow(clippy::too_many_arguments)]
impl Device {
    pub fn new(
        is_generic: bool,
        instance_id: String,
        hardware_ids: Option<String>,
        friendly_name: Option<String>,
        description: Option<String>,
        manufacturer: Option<String>,
        driver_name: Option<String>,
        class: Option<String>,
        class_guid: Uuid,
        inf_name: Option<String>,
        inf_original_name: Option<String>,
        inf_section: Option<String>,
        inf_provider: Option<String>,
        driver_store_location: Option<String>,
    ) -> Self {
        Self {
            is_generic,
            instance_id,
            hardware_ids: match hardware_ids {
                Some(s) => s.split('\u{0}').map(|s| s.to_string()).collect(),
                None => Vec::new(),
            },
            friendly_name,
            description,
            manufacturer,
            driver_name,
            class,
            class_guid,
            inf_name,
            inf_original_name,
            inf_section,
            inf_provider,
            driver_store_location,
        }
    }

    pub fn is_generic(&self) -> bool {
        self.is_generic
    }

    pub fn instance_id(&self) -> &str {
        &self.instance_id
    }

    pub fn hardware_ids(&self) -> &[String] {
        self.hardware_ids.as_slice()
    }

    pub fn friendly_name(&self) -> Option<&str> {
        match &self.friendly_name {
            Some(_) => self.friendly_name.as_deref(),
            None => self.description.as_deref(),
        }
    }

    pub fn description(&self) -> Option<&str> {
        self.description.as_deref()
    }

    pub fn manufacturer(&self) -> Option<&str> {
        self.manufacturer.as_deref()
    }

    pub fn driver_name(&self) -> Option<&str> {
        self.driver_name.as_deref()
    }

    pub fn class(&self) -> Option<&str> {
        self.class.as_deref()
    }

    pub fn class_guid(&self) -> &Uuid {
        &self.class_guid
    }

    pub fn inf_name(&self) -> Option<&str> {
        self.inf_name.as_deref()
    }

    pub fn inf_original_name(&self) -> Option<&str> {
        self.inf_original_name.as_deref()
    }

    pub fn inf_section(&self) -> Option<&str> {
        self.inf_section.as_deref()
    }

    pub fn inf_provider(&self) -> Option<&str> {
        self.inf_provider.as_deref()
    }

    pub fn driver_store_location(&self) -> Option<&str> {
        self.driver_store_location.as_deref()
    }
}

impl std::fmt::Display for Device {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self.friendly_name() {
            Some(name) => write!(f, "{} ({})", name, self.instance_id()),
            None => write!(f, "{}", self.instance_id()),
        }
    }
}

impl std::fmt::Debug for Device {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Device")
            .field("friendly_name", &self.friendly_name)
            .field("class", &self.class)
            .finish()
    }
}

#[derive(Serialize)]
pub struct Driver {
    inf_name: String,
    inf_original_name: Option<String>,
    driver_store_location: Option<String>,
    provider: Option<String>,
    class: Option<String>,
    class_guid: Uuid,
}

#[allow(dead_code)]
impl Driver {
    pub fn new(
        inf_name: String,
        inf_original_name: Option<String>,
        driver_store_location: Option<String>,
        provider: Option<String>,
        class: Option<String>,
        class_guid: Uuid,
    ) -> Driver {
        Driver {
            inf_name,
            inf_original_name,
            driver_store_location,
            provider,
            class,
            class_guid,
        }
    }

    pub fn inf_name(&self) -> &str {
        &self.inf_name
    }

    pub fn inf_original_name(&self) -> Option<&str> {
        self.inf_original_name.as_deref()
    }

    pub fn driver_store_location(&self) -> Option<&str> {
        self.driver_store_location.as_deref()
    }

    pub fn provider(&self) -> Option<&str> {
        self.provider.as_deref()
    }

    pub fn class(&self) -> Option<&str> {
        self.class.as_deref()
    }

    pub fn class_guid(&self) -> &Uuid {
        &self.class_guid
    }
}

impl std::fmt::Display for Driver {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match &self.inf_original_name {
            Some(original) => write!(f, "{} ({})", self.inf_name, original),
            None => write!(f, "{}", self.inf_name),
        }
    }
}

impl std::fmt::Debug for Driver {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Driver")
            .field("provider", &self.provider)
            .field("inf_original_name", &self.inf_original_name)
            .finish()
    }
}

#[derive(Serialize)]
pub struct DriverPackage {
    x86: bool,
    key_name: String,
    display_name: Option<String>,
    display_version: Option<String>,
    publisher: Option<String>,
    install_location: Option<String>,
    uninstall_string: Option<String>,
}

#[allow(dead_code)]
impl DriverPackage {
    pub fn new(
        x86: bool,
        key_name: String,
        display_name: Option<String>,
        display_version: Option<String>,
        publisher: Option<String>,
        install_location: Option<String>,
        uninstall_string: Option<String>,
    ) -> Self {
        Self {
            x86,
            key_name,
            display_name,
            display_version,
            publisher,
            install_location,
            uninstall_string,
        }
    }

    pub fn from(reg_key: &RegKey, name: String, x86: bool) -> Self {
        let display_name: Option<String> = Self::reg_get_value(reg_key, "DisplayName");
        let display_version: Option<String> = Self::reg_get_value(reg_key, "DisplayVersion");
        let publisher: Option<String> = Self::reg_get_value(reg_key, "Publisher");
        let install_location: Option<String> = Self::reg_get_value(reg_key, "InstallLocation");
        let uninstall_string: Option<String> = Self::reg_get_value(reg_key, "UninstallString");

        Self::new(
            x86,
            name,
            display_name,
            display_version,
            publisher,
            install_location,
            uninstall_string,
        )
    }

    fn reg_get_value<T: FromRegValue>(reg_key: &RegKey, name: &str) -> Option<T> {
        match reg_key.get_value::<T, _>(name) {
            Ok(value) => Some(value),
            Err(_) => None,
        }
    }

    pub fn x86(&self) -> bool {
        self.x86
    }

    pub fn key_name(&self) -> &str {
        &self.key_name
    }

    pub fn display_name(&self) -> Option<&str> {
        self.display_name.as_deref()
    }

    pub fn display_version(&self) -> Option<&str> {
        self.display_version.as_deref()
    }

    pub fn publisher(&self) -> Option<&str> {
        self.publisher.as_deref()
    }

    pub fn install_location(&self) -> Option<&str> {
        self.install_location.as_deref()
    }

    pub fn uninstall_string(&self) -> Option<&str> {
        self.uninstall_string.as_deref()
    }
}

impl std::fmt::Display for DriverPackage {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self.display_name() {
            Some(display_name) => write!(f, "{}", display_name),
            None => write!(f, "{}", self.key_name()),
        }
    }
}

pub fn process_is_elevated() -> bool {
    unsafe {
        let mut token: HANDLE = HANDLE::default();
        let mut token_elevation_info: TOKEN_ELEVATION = Default::default();
        let token_elevation_info_ptr = &mut token_elevation_info as *mut TOKEN_ELEVATION;
        let mut size: u32 = 0;

        let current_process = GetCurrentProcess();
        if !OpenProcessToken(current_process, TOKEN_QUERY, &mut token).as_bool() {
            return false;
        }

        let token = Handle::new(token);

        let success = GetTokenInformation(
            token.handle,
            TokenElevation,
            Some(token_elevation_info_ptr.cast()),
            std::mem::size_of::<TOKEN_ELEVATION>() as u32,
            &mut size,
        )
        .as_bool();

        if !success {
            return false;
        }

        token_elevation_info.TokenIsElevated != 0
    }
}

#[derive(Debug, Error)]
enum FfiError {
    #[error("I/O failed")]
    Io,
    #[error("parser has failed to parse the buffer")]
    Parser,
}

#[derive(Debug, Error)]
pub enum EnumerationError {
    #[error("failed to enumerate devices")]
    Device,
    #[error("failed to enumerate device drivers")]
    Driver,
    #[error("failed to enumerate driver packages")]
    DriverPackage,
}

pub fn enumerate_devices() -> Result<Vec<Device>, EnumerationError> {
    unsafe {
        let mut devices = Vec::<Device>::new();
        let device_info_set =
            SetupDiGetClassDevsW(None, None, None, DIGCF_ALLCLASSES | DIGCF_PRESENT)
                .into_report()
                .change_context(EnumerationError::Device)
                .attach_printable_lazy(|| "failed to initialize a device info set")?;
        let mut device_info = SP_DEVINFO_DATA {
            cbSize: std::mem::size_of::<SP_DEVINFO_DATA>() as u32,
            ..Default::default()
        };

        for i in 0.. {
            if !SetupDiEnumDeviceInfo(device_info_set, i, &mut device_info).as_bool() {
                let err = windows::core::Error::from_win32();
                if err.code() != HRESULT::from(ERROR_NO_MORE_ITEMS) {
                    return Err(err)
                        .into_report()
                        .change_context(EnumerationError::Device)
                        .attach_printable_lazy(|| {
                            format!("failed to enumerate device info at index {i}")
                        });
                }

                break;
            }

            let device = create_device(device_info_set, device_info)?;
            devices.push(device);
        }

        Ok(devices)
    }
}

fn create_device(
    device_info_set: HDEVINFO,
    device_info: SP_DEVINFO_DATA,
) -> Result<Device, EnumerationError> {
    let generic = get_device_property(
        device_info_set,
        &device_info,
        &DEVPKEY_Device_GenericDriverInstalled,
        parse_bool,
    );
    let generic = generic
        .change_context(EnumerationError::Device)
        .attach_printable("failed to get device 'DEVPKEY_Device_GenericDriverInstalled'")?
        .unwrap();
    let instance_id = get_device_instance_id(device_info_set, &device_info)
        .change_context(EnumerationError::Device)
        .attach_printable("failed to get device InstanceID")?
        .unwrap();
    let hardware_ids =
        get_device_registry_property(device_info_set, &device_info, SPDRP_HARDWAREID, parse_str)
            .change_context(EnumerationError::Device)
            .attach_printable("failed to get device registry property: 'SPDRP_HARDWAREID'")?;
    let friendly_name =
        get_device_registry_property(device_info_set, &device_info, SPDRP_FRIENDLYNAME, parse_str)
            .change_context(EnumerationError::Device)
            .attach_printable("failed to get device registry property: 'SPDRP_FRIENDLYNAME'")?;
    let description =
        get_device_registry_property(device_info_set, &device_info, SPDRP_DEVICEDESC, parse_str)
            .change_context(EnumerationError::Device)
            .attach_printable("failed to get device registry property: 'SPDRP_DEVICEDESC'")?;
    let manufacturer =
        get_device_registry_property(device_info_set, &device_info, SPDRP_MFG, parse_str)
            .change_context(EnumerationError::Device)
            .attach_printable("failed to get device registry property: 'SPDRP_MFG'")?;
    let driver_name =
        get_device_registry_property(device_info_set, &device_info, SPDRP_DRIVER, parse_str)
            .change_context(EnumerationError::Device)
            .attach_printable("failed to get device registry property: 'SPDRP_DRIVER'")?;
    let class_name =
        get_device_registry_property(device_info_set, &device_info, SPDRP_CLASS, parse_str)
            .change_context(EnumerationError::Device)
            .attach_printable("failed to get device registry property: 'SPDRP_CLASS'")?;
    let class_guid =
        get_device_registry_property(device_info_set, &device_info, SPDRP_CLASSGUID, parse_uuid)
            .change_context(EnumerationError::Device)
            .attach_printable("failed to get device registry property: 'SPDRP_CLASSGUID'")?
            .unwrap_or_default();
    let inf_name = get_device_property(
        device_info_set,
        &device_info,
        &DEVPKEY_Device_DriverInfPath,
        parse_str,
    );
    let inf_name = inf_name
        .change_context(EnumerationError::Device)
        .attach_printable("failed to get device 'DEVPKEY_Device_DriverInfPath'")?;
    let inf_original_name = if let Some(inf_name) = &inf_name {
        get_inf_driver_store_location(&OsString::from(inf_name.as_str()))
            .change_context(EnumerationError::Device)
            .attach_printable("failed to get inf original name")?
    } else {
        None
    };
    let inf_original_name = inf_original_name.as_ref().map(Path::new);
    let inf_section = get_device_property(
        device_info_set,
        &device_info,
        &DEVPKEY_Device_DriverInfSection,
        parse_str,
    )
    .change_context(EnumerationError::Device)
    .attach_printable("failed to get device 'DEVPKEY_Device_DriverInfSection'")?;
    let inf_provider = get_device_property(
        device_info_set,
        &device_info,
        &DEVPKEY_Device_DriverProvider,
        parse_str,
    )
    .change_context(EnumerationError::Device)
    .attach_printable("failed to get device 'DEVPKEY_Device_DriverProvider'")?;

    Ok(Device::new(
        generic,
        instance_id,
        hardware_ids,
        friendly_name,
        description,
        manufacturer,
        driver_name,
        class_name,
        class_guid,
        inf_name,
        inf_original_name
            .and_then(|f| f.file_name())
            .and_then(|f| f.to_str())
            .map(|f| f.to_owned()),
        inf_section,
        inf_provider,
        inf_original_name
            .and_then(|f| f.parent())
            .and_then(|f| f.to_str())
            .map(|f| f.to_owned()),
    ))
}

struct InfFileHandle {
    handle: *const c_void,
}

impl Drop for InfFileHandle {
    fn drop(&mut self) {
        unsafe {
            SetupCloseInfFile(self.handle);
        }
    }
}

pub fn enumerate_drivers() -> Result<Vec<Driver>, EnumerationError> {
    unsafe {
        let mut drivers = Vec::<Driver>::new();
        let inf_list = get_inf_file_list();

        for inf in inf_list {
            let inf_file = SetupOpenInfFileW(
                &HSTRING::from(&inf),
                None,
                INF_STYLE_WIN4.0 | INF_STYLE_OLDNT.0,
                None,
            );
            let inf_file = InfFileHandle { handle: inf_file };

            if inf_file.handle.is_null() {
                let error = windows::core::Error::from_win32();
                return Err(error)
                    .into_report()
                    .attach_printable_lazy(|| {
                        format!("failed to get a file handle to '{}'", inf.to_str().unwrap())
                    })
                    .change_context(EnumerationError::Driver);
            }

            let driver: Result<Driver, EnumerationError> = {
                let inf_original_name = get_inf_driver_store_location(&inf)
                    .change_context(EnumerationError::Driver)
                    .attach_printable("failed to get inf original name")?;
                let inf_provider =
                    get_inf_property(inf_file.handle, "Version", "Provider", parse_str)
                        .change_context(EnumerationError::Driver)
                        .attach_printable(
                            "failed to get inf property 'Provider' in section 'Version'",
                        )?;
                let class_name = get_inf_property(inf_file.handle, "Version", "Class", parse_str)
                    .change_context(EnumerationError::Driver)
                    .attach_printable("failed to get inf property 'Class' in section 'Version'")?;
                let class_uuid =
                    get_inf_property(inf_file.handle, "Version", "ClassGUID", parse_uuid)
                        .change_context(EnumerationError::Driver)
                        .attach_printable(
                            "failed to get inf property 'ClassGUID' in section 'Version'",
                        )?
                        .unwrap_or_default();

                let inf_original_name = inf_original_name.as_ref().map(Path::new);

                Ok(Driver::new(
                    inf.to_str().unwrap().to_string(),
                    inf_original_name
                        .and_then(|f| f.file_name())
                        .and_then(|f| f.to_str())
                        .map(|f| f.to_owned()),
                    inf_original_name
                        .and_then(|f| f.parent())
                        .and_then(|f| f.to_str())
                        .map(|f| f.to_owned()),
                    inf_provider,
                    class_name,
                    class_uuid,
                ))
            };

            let driver = driver?;
            drivers.push(driver);
        }

        Ok(drivers)
    }
}

pub fn enumerate_driver_packages() -> Result<Vec<DriverPackage>, EnumerationError> {
    let mut driver_packages = Vec::<DriverPackage>::new();
    let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
    let uninstall_path = Path::new("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
    let uninstall_key = open_key(&hklm, uninstall_path);
    let x86_uninstall_path =
        Path::new("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
    let x86_uninstall_key = open_key(&hklm, x86_uninstall_path);

    if let Ok(key) = uninstall_key {
        for subkey_name in key.enum_keys().map(|x| x.unwrap()) {
            if let Ok(subkey) = key.open_subkey(&subkey_name) {
                let subkey_path: String = Path::join(uninstall_path, subkey_name)
                    .to_str()
                    .unwrap()
                    .to_string();
                driver_packages.push(DriverPackage::from(&subkey, subkey_path, false));
            }
        }
    } else {
        return Err(report!(EnumerationError::DriverPackage))
            .attach_printable("failed to open uninstall key");
    }

    if let Ok(key) = x86_uninstall_key {
        for subkey_name in key.enum_keys().map(|x| x.unwrap()) {
            if let Ok(subkey) = key.open_subkey(&subkey_name) {
                let subkey_path: String = Path::join(x86_uninstall_path, subkey_name)
                    .to_str()
                    .unwrap()
                    .to_string();
                driver_packages.push(DriverPackage::from(&subkey, subkey_path, true));
            }
        }
    }

    Ok(driver_packages)
}

fn open_key(hklm: &RegKey, uninstall_path: &Path) -> Result<RegKey, EnumerationError> {
    hklm.open_subkey(uninstall_path)
        .into_report()
        .attach_printable_lazy(|| {
            format!(
                "failed to open registry key '{}'",
                uninstall_path.to_str().unwrap()
            )
        })
        .change_context(EnumerationError::DriverPackage)
}

fn get_inf_property<T>(
    inf: *const c_void,
    section: &str,
    key: &str,
    parser: impl FnOnce(&[u8]) -> Result<T, FfiError>,
) -> Result<Option<T>, FfiError>
where
    T: Default,
{
    generic_get(
        |buffer| unsafe {
            let mut size: u32 = 0;
            let is_none = buffer.is_none();
            let new_slice = buffer.map(to_u16_slice_mut);
            let ret = SetupGetLineTextW(
                None,
                Some(inf),
                &HSTRING::from(section),
                &HSTRING::from(key),
                new_slice,
                Some(&mut size),
            )
            .as_bool();

            if !ret || is_none {
                return Err(GenericGetError {
                    required_size: size * 2, // PCWSTR to byte
                    error: GetLastError(),
                });
            }

            Ok(())
        },
        parser,
        &[],
    )
}

fn get_inf_file_list() -> Vec<OsString> {
    let windir = std::env::var("WINDIR").unwrap();
    lazy_static! {
        static ref INF_REGEX: Regex = Regex::new(r"^oem[0-9]+\.inf$").unwrap();
    }

    Path::new(&windir)
        .join("inf")
        .read_dir()
        .unwrap()
        .map(|e| e.unwrap().file_name())
        .filter(|e| INF_REGEX.is_match(e.to_str().unwrap()))
        .collect()
}

fn get_device_registry_property<T>(
    device_info_set: HDEVINFO,
    device_info: &SP_DEVINFO_DATA,
    prop: u32,
    parser: impl FnOnce(&[u8]) -> Result<T, FfiError>,
) -> Result<Option<T>, FfiError>
where
    T: Default,
{
    generic_get(
        |buffer| unsafe {
            let mut size: u32 = 0;
            if !SetupDiGetDeviceRegistryPropertyW(
                device_info_set,
                device_info,
                prop,
                None,
                buffer,
                Some(&mut size),
            )
            .as_bool()
            {
                return Err(GenericGetError {
                    required_size: size,
                    error: GetLastError(),
                });
            }

            Ok(())
        },
        parser,
        &[ERROR_INVALID_DATA],
    )
}

fn get_device_instance_id(
    device_info_set: HDEVINFO,
    device_info: &SP_DEVINFO_DATA,
) -> Result<Option<String>, FfiError> {
    generic_get(
        |buffer| unsafe {
            let mut size: u32 = match &buffer {
                Some(b) => b.len() as u32,
                None => 0,
            };

            let new_slice = buffer.map(to_u16_slice_mut);
            if !SetupDiGetDeviceInstanceIdW(
                device_info_set,
                device_info,
                new_slice,
                Some(&mut size),
            )
            .as_bool()
            {
                return Err(GenericGetError {
                    required_size: size * 2, // PWSTR to byte
                    error: GetLastError(),
                });
            }

            Ok(())
        },
        parse_str,
        &[],
    )
}

fn to_u16_slice(slice: &[u8]) -> &[u16] {
    assert!(
        (slice.len() & 1) == 0,
        "slice should have even value, has: {}",
        slice.len()
    );
    unsafe { std::slice::from_raw_parts(slice.as_ptr() as *const u16, slice.len() / 2) }
}

fn to_u16_slice_mut(slice: &mut [u8]) -> &mut [u16] {
    assert!(
        (slice.len() & 1) == 0,
        "slice should have even value, has: {}",
        slice.len()
    );
    unsafe { std::slice::from_raw_parts_mut(slice.as_mut_ptr() as *mut u16, slice.len() / 2) }
}

fn get_device_property<T>(
    device_info_set: HDEVINFO,
    device_info: &SP_DEVINFO_DATA,
    prop_key: &DEVPROPKEY,
    parser: impl FnOnce(&[u8]) -> Result<T, FfiError>,
) -> Result<Option<T>, FfiError>
where
    T: Default,
{
    generic_get(
        |buffer| unsafe {
            let mut size: u32 = 0;
            let mut prop_type: u32 = 0;
            if !SetupDiGetDevicePropertyW(
                device_info_set,
                device_info,
                prop_key,
                &mut prop_type,
                buffer,
                Some(&mut size),
                0,
            )
            .as_bool()
            {
                return Err(GenericGetError {
                    required_size: size,
                    error: GetLastError(),
                });
            }

            Ok(())
        },
        parser,
        &[ERROR_NOT_FOUND],
    )
}

fn get_inf_driver_store_location(inf_name: &OsStr) -> Result<Option<String>, FfiError> {
    generic_get(
        |buffer| unsafe {
            let mut size: u32 = 0;
            let mut empty_arr: [u16; 0] = [];
            if !SetupGetInfDriverStoreLocationW(
                &HSTRING::from(inf_name),
                None,
                None,
                buffer.map(to_u16_slice_mut).unwrap_or(&mut empty_arr),
                Some(&mut size),
            )
            .as_bool()
            {
                Err(GenericGetError {
                    required_size: size * 2, // PCWSTR to byte
                    error: GetLastError(),
                })
            } else {
                Ok(())
            }
        },
        parse_str,
        &[ERROR_NOT_FOUND, ERROR_FILE_NOT_FOUND],
    )
}

fn parse_str(buffer: &[u8]) -> Result<String, FfiError> {
    Ok(HSTRING::from_wide(to_u16_slice(buffer))
        .into_report()
        .change_context(FfiError::Parser)
        .attach_printable("failed to parse string")?
        .to_string())
}

fn parse_uuid(buffer: &[u8]) -> Result<Uuid, FfiError> {
    let string = HSTRING::from_wide(to_u16_slice(buffer))
        .into_report()
        .change_context(FfiError::Parser)
        .attach_printable("failed to parse a uuid into a string")?
        .to_string();
    let str = string.trim_matches(|c: char| !c.is_ascii_alphanumeric());
    let str = match str.starts_with('{') {
        true => &str[1..str.len() - 1],
        false => str,
    };

    Uuid::parse_str(str)
        .into_report()
        .change_context(FfiError::Parser)
        .attach_printable_lazy(|| format!("failed to parse uuid from {str}"))
}

fn parse_bool(buffer: &[u8]) -> Result<bool, FfiError> {
    Ok((buffer[0] as i8) == -1)
}

#[derive(Debug, Error)]
#[error("failed to get value")]
struct GenericGetError {
    required_size: u32,
    error: WIN32_ERROR,
}

fn generic_get<T>(
    getter: impl Fn(Option<&mut [u8]>) -> CResult<(), GenericGetError>,
    parser: impl FnOnce(&[u8]) -> Result<T, FfiError>,
    skip_codes: &[WIN32_ERROR],
) -> Result<Option<T>, FfiError>
where
    T: Default,
{
    let size: u32;
    let get = getter(None);
    match get {
        CResult::Ok(_) => panic!("should not be able to get value without buffer"),
        CResult::Err(GenericGetError {
            required_size,
            error,
        }) => match error {
            _ if skip_codes.contains(&error) => return Ok(Some(T::default())),
            ERROR_INSUFFICIENT_BUFFER | ERROR_INVALID_USER_BUFFER | NO_ERROR => {
                size = required_size
            }
            _ => {
                let error: windows::core::Error = error.into();
                return Err(error)
                    .into_report()
                    .attach_printable("failed to query required buffer size")
                    .change_context(FfiError::Io);
            }
        },
    }

    let mut buffer: Vec<u8> = Vec::with_capacity(size as usize);
    let buffer_slice =
        unsafe { std::slice::from_raw_parts_mut(buffer.as_mut_ptr(), buffer.capacity()) };

    let get = getter(Some(buffer_slice));
    match get {
        CResult::Ok(_) => Ok(Some(parser(buffer_slice)?)),
        CResult::Err(GenericGetError { error, .. }) => {
            let error: windows::core::Error = error.into();
            Err(error)
                .into_report()
                .attach_printable("failed to get value")
                .change_context(FfiError::Io)
        }
    }
}

#[derive(Error, Debug)]
pub enum WaitError {
    #[error("timed out waiting for process")]
    Timeout,
    #[error("failed to wait for process: {0}")]
    Failed(windows::core::Error),
}

// pub fn wait_for_process(process_id: u32, timeout: Option<u32>) -> Result<(), WaitError> {
//     unsafe {
//         let process = OpenProcess(PROCESS_SYNCHRONIZE, false, process_id);

//         let process = match process {
//             Ok(process) => process,
//             Err(error) => bail!(WaitError::Failed(error.code().into())),
//         };

//         let err = WaitForSingleObject(process, timeout.unwrap_or(INFINITE));
//         match err {
//             WAIT_OBJECT_0 => Ok(()),
//             WAIT_ABANDONED => Ok(()),
//             WAIT_TIMEOUT => Err(report!(WaitError::Timeout)),
//             WAIT_FAILED => Err(report!(WaitError::Failed(
//                 windows::core::Error::from_win32()
//             ))),
//             _ => unreachable!("WaitForSingleObject returned an invalid value"),
//         }
//     }
// }

pub async fn wait_for_process_async(
    process_id: u32,
    ct: Option<CancellationToken>,
) -> Result<(), WaitError> {
    unsafe {
        let process = OpenProcess(PROCESS_SYNCHRONIZE, false, process_id);

        let process = match process {
            Ok(process) => process,
            Err(error) => bail!(WaitError::Failed(error.code().into())),
        };

        let process = Handle::from(process);

        loop {
            let err = WaitForSingleObject(process.handle, 0);
            match err {
                WAIT_OBJECT_0 => return Ok(()),
                WAIT_ABANDONED => return Ok(()),
                WAIT_TIMEOUT => {
                    if let Some(ct) = &ct {
                        if ct.is_cancelled() {
                            bail!(WaitError::Timeout);
                        }
                    }
                    tokio::time::sleep(Duration::from_millis(20)).await;
                }
                WAIT_FAILED => bail!(WaitError::Failed(windows::core::Error::from_win32())),
                _ => unreachable!("WaitForSingleObject returned an invalid value"),
            }
        }
    }
}

// const INFINITE: u32 = 4294967295u32;

pub(crate) fn inf_regex() -> Regex {
    Regex::new(r"^oem[0-9]+\.inf$").unwrap()
}
