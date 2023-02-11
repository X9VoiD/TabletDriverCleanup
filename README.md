# TabletDriverCleanup

A small tool that cleans up leftover drivers of various tablet drivers.

## Instructions

- Download `TabletDriverCleanup.zip` from [latest release](https://github.com/X9VoiD/WinUSBCleanup/releases).
- Extract the contents of the zip file.
- Run as administrator.

## Updating Identifiers

TabletDriverCleanup makes use of "identifiers" to know what devices or drivers to uninstall. If there is no `config`
directory/folder beside TabletDriverCleanup, it will attempt to download up-to-date identifiers from this repository.

To update identifiers:
- Delete `config` folder if it exists.

## Build Instructions

For those who want to build the project from source:

- Install .NET 7.0 SDK
- Clone the repository
- Run `build.ps1` in the root directory

The binaries will be in `build`.
