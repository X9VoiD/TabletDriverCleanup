@echo off
setlocal EnableDelayedExpansion

cd /D "%~dp0"

:: Define CR to contain a carriage return (0x0D)
for /f %%A in ('copy /Z "%~dpf0" nul') do set "CR=%%A"

:: Dump
tabletdrivercleanup.exe --dump
pnputil /enum-drivers > .\dumps\pnputil_drivers.txt
pnputil /enum-devices /connected /drivers > .\dumps\pnputil_devices.txt

md .\dumps\DriverStore

for /D %%G in ("C:\Windows\System32\DriverStore\FileRepository\*") DO (
    md .\dumps\DriverStore\%%~nxG
    for /F %%H in ("%%~G\*.inf") DO (
        xcopy /Q /Y "%%H" .\dumps\DriverStore\%%~nxG > nul
        <nul set /p "=Dumped '%%~nxH'                                 !CR!"
    )
)

<nul set /p "=Dumped INF files                                     !CR!"

echo Dumped INF files

set "srcDir=.\dumps"
set "destZip=.\dumps.zip"

if exist "%destZip%" del "%destZip%"

powershell -nologo -noprofile -command "& { Compress-Archive -Path '%srcDir%' -DestinationPath '%destZip%' }"

echo Created '%destZip%'
rd /s /q .\dumps

echo Done

pause