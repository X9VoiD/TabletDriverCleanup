# TabletDriverCleanup
A small tool that cleans up leftover drivers of various tablet drivers.

## Instructions

- Download the `TabletDriverCleanup.exe` from the [latest release](https://github.com/X9VoiD/WinUSBCleanup/releases).
- Run as administrator.

## Build Instructions

For people who want to build the tool themselves.

- Install .NET 7.0 SDK
- Clone the repository
- `dotnet publish .\src\TabletDriverCleanup\ /p:PublishSingleFile=true /p:PublishTrimmed=true /p:DebugType=embedded --configuration=Release --self-contained=true`

The executable would be in `src/TabletDriverCleanup/bin/Release/net7.0/win-x64/publish`.