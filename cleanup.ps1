# Self-elevate the script if required
if (-Not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] 'Administrator'))
{
    if ([int](Get-CimInstance -Class Win32_OperatingSystem | Select-Object -ExpandProperty BuildNumber) -ge 6000)
    {
        $Location = (Get-Item $MyInvocation.MyCommand.Path).DirectoryName
        $CommandLine = "-ExecutionPolicy Bypass -File `"" + $MyInvocation.MyCommand.Path + "`" "
        Start-Process -FilePath PowerShell.exe -WorkingDirectory $Location -Verb Runas -ArgumentList $CommandLine
        Exit
    }
}

Write-Output "Huion/Gaomon Cleanup Script"
Set-Location ((Get-Item $MyInvocation.MyCommand.Path).DirectoryName)

$pnputil = & pnputil -e
$detected = 0

if (($pnputil -match "GAOMON") -and ($pnputil -match "11/02/2018"))
{
    Write-Output ""
    Write-Output ""
    Write-Output "Uninstalling VMulti: GAOMON HID (11 - 02 - 2018)"
    Write-Output ""
    Set-Location "VMulti\GAOMON HID 11-02-2018"
    & ".\remove_gaomonhiddriver.bat"
    Set-Location ../..
    $detected = 1
}

if (($pnputil -match "HUION Animation") -and ($pnputil -match "03/16/2018"))
{
    Write-Output ""
    Write-Output ""
    Write-Output "Uninstalling VMulti: HUION HID (03 - 16 - 2018)"
    Write-Output ""
    Set-Location "VMulti\HUION HID 03-16-2018"
    & ".\remove_huionhiddriver.bat"
    Set-Location ../..
    $detected = 1
}

if (($pnputil -match "HUION Animation") -and ($pnputil -match "09/10/2014"))
{
    Write-Output ""
    Write-Output ""
    Write-Output "Uninstalling VMulti: HUION HID (09-10-2014)"
    Write-Output ""
    Set-Location "VMulti\HUION HID 09-16-2014"
    & ".\remove_huionhiddriver.bat"
    Set-Location ../..
    $detected = 1
}

if (($pnputil -match "Graphics Tablet") -and ($pnputil -match "04/10/2014"))
{
    Write-Output ""
    Write-Output ""
    Write-Output "Uninstalling WinUSB: Graphics Tablet (04-10-2014)"
    Write-Output ""
    Set-Location "WinUSB\Graphics Tablet 04-10-2014"
    & ".\tabetdriveruninstall.bat"
    Set-Location ../..
    $detected = 1
}

if (($pnputil -match "Graphics Tablet") -and ($pnputil -match "04/10/2017"))
{
    Write-Output ""
    Write-Output ""
    Write-Output "Uninstalling WinUSB: Graphics Tablet (04-10-2017)"
    Write-Output ""
    Set-Location "WinUSB\Graphics Tablet 04-10-2017"
    & ".\tabetdriveruninstall.bat"
    Set-Location ../..
    $detected = 1
}

if (-not $detected)
{
    Write-Output ""
    Write-Output ""
    Write-Output "No Gaomon/Huion driver detected"
}
else
{
    Write-Output ""
    Write-Output "Done"
}

Write-Output ""
Write-Output "Press any key to exit..."
[System.Console]::ReadKey()