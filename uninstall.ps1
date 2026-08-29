# DsHidMini - Bluetooth rumble fix - uninstaller
#
# Removes the test-signed patched driver, turns Test Signing OFF, and removes the bundled
# certificate. After this, reinstall the official Nefarius DsHidMini driver (its updater) so your
# controller keeps working. Self-elevates via UAC.

$ErrorActionPreference = 'Continue'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell -Verb RunAs -ArgumentList `
        "-NoProfile","-ExecutionPolicy","Bypass","-File","`"$PSCommandPath`""
    exit
}

Write-Host "== Uninstalling DsHidMini BT rumble fix ==" -ForegroundColor Cyan

Write-Host "[1/3] Removing the test-signed driver package..." -ForegroundColor Cyan
$ours = Get-WindowsDriver -Online | Where-Object {
    $_.OriginalFileName -match 'dshidmini\.inf$' -and $_.Version -eq '1.0.0.0'
}
if ($ours) {
    foreach ($p in $ours) {
        Write-Host "  deleting $($p.Driver)"
        pnputil /delete-driver $p.Driver /uninstall /force | Out-Null
    }
} else {
    Write-Host "  (patched package not found - maybe already removed)"
}

Write-Host "[2/3] Disabling Test Signing..." -ForegroundColor Cyan
$ts = 'testsigning'
& bcdedit.exe /set $ts off

Write-Host "[3/3] Removing bundled certificate..." -ForegroundColor Cyan
Get-ChildItem Cert:\LocalMachine\Root, Cert:\LocalMachine\TrustedPublisher -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -eq 'CN=DsHidMini Test Cert' } |
    Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Done. Now REINSTALL the official Nefarius DsHidMini driver so your controller keeps" -ForegroundColor Green
Write-Host "working (run its updater / reinstall), then REBOOT to finish disabling Test Mode." -ForegroundColor Green
Read-Host "Enter to close"
