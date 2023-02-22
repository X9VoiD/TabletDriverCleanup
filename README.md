# TabletDriverCleanup

A CLI program that cleanly uninstalls tablet drivers.

## Instructions

- Download `TabletDriverCleanup.zip` from [latest release](https://github.com/X9VoiD/TabletDriverCleanup/releases).
- Extract the contents of the zip file.
- Run as administrator.

## Updating Identifiers

TabletDriverCleanup makes use of *identifiers* to know what devices or drivers to uninstall. If there is no `config`
directory/folder beside TabletDriverCleanup, it will attempt to download up-to-date identifiers from this repository.

> *tl;dr*: delete `config` folder if it exists to update identifiers.

## CLI

```
Usage: TabletDriverCleanup [options]
Options:
  --no-prompt                   do not prompt for user input
  --no-cache                    do not use cached data in ./config
  --no-update                   do not check for config updates
  --no-driver-package-cleanup           do not remove driver packages from the system
  --no-device-cleanup           do not remove devices from the system
  --no-driver-cleanup           do not uninstall drivers from the system
  --dry-run                     only print the changes that would be made
  --dump                        dump some information about devices and drivers
  --help                        show this help message
```

Batch files are provided for convenience in invoking certain flags/options.

## Supported Drivers

- [x] 10moon
- [x] Acepen
- [x] Artisul
- [x] Gaomon
- [x] Genius
- [x] Huion
- [x] Kenting
- [x] Monoprice
- [x] Parblo
- [x] Veikk
- [x] ViewSonic
- [x] Wacom
- [x] XenceLab
- [x] XENX
- [x] XP-Pen

## Build Instructions

For those who want to build the project from source:

- Install .NET 7.0 SDK
- Clone the repository
- Run `build.ps1` in the root directory

The binaries will be in `build`.
