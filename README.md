# TabletDriverCleanup

A CLI program that cleanly uninstalls tablet drivers.

## Instructions

- Download `tabletdrivercleanup.zip` from [latest release](https://github.com/X9VoiD/TabletDriverCleanup/releases).
- Extract the contents of the zip file.
- Run as administrator.

## Updating Identifiers

TabletDriverCleanup makes use of *identifiers* to know what devices or drivers to uninstall. If there is no `config`
directory/folder beside TabletDriverCleanup, it will attempt to download up-to-date identifiers from this repository.

> *tl;dr*: delete `config` folder if it exists to update identifiers.

## CLI

```
Usage: tabletdrivercleanup.exe [OPTIONS]

Options:
  -d, --dry-run                    Only print what would be done, do not actually do anything
  -D, --dump                       Dump information about the system
  -s, --no-prompt                  Do not prompt for user input. Useful for scripting
  -c, --no-cache                   Do not use cached identifiers
  -u, --no-update                  Do not check online for identifier updates
      --no-driver-package-cleanup  Do not uninstall driver software packages
      --no-device-cleanup          Do not remove devices from the system
      --no-driver-cleanup          Do not uninstall device drivers from the system
  -h, --help                       Print help
  -V, --version                    Print version
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

- Install [Rust](https://www.rust-lang.org/tools/install)
- Clone the repository
- Run `build.ps1` in the root directory

The binaries will be in `build`.
