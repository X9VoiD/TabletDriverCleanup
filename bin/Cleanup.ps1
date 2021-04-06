Write-Output "Huion/Gaomon Cleanup Script"

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
    Write-Output "No Gaomon/Huion driver detected"
}

Write-Output "Done."