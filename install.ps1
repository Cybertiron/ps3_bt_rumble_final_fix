# DsHidMini - Bluetooth rumble fix - one-shot installer (test-signed build)
#
# WHAT THIS DOES (read before running):
#   1. Trusts the bundled self-signed certificate (LocalMachine Root + TrustedPublisher)
#   2. Installs the patched, test-signed driver package
#   3. Enables Windows Test Signing mode (requires Secure Boot OFF; adds a "Test Mode" watermark)
#   4. Offers to reboot (required for the driver to load)
#
# This lowers a driver-signing security boundary system-wide and makes your machine trust anything
# signed by the bundled certificate's key. Only continue if you accept that. See README.
#
# Run from the extracted release folder. It self-elevates via UAC.

$ErrorActionPreference = 'Stop'

# --- self-elevate ---
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell -Verb RunAs -ArgumentList `
        "-NoProfile","-ExecutionPolicy","Bypass","-File","`"$PSCommandPath`"" -WorkingDirectory $PSScriptRoot
    exit
}

Set-Location $PSScriptRoot
$cer = Join-Path $PSScriptRoot 'DsHidMiniTest.cer'
$inf = Join-Path $PSScriptRoot 'dshidmini.inf'
if (-not (Test-Path $cer) -or -not (Test-Path $inf)) {
    Write-Host "ERROR: run this from the extracted release folder (DsHidMiniTest.cer / dshidmini.inf not found)." -ForegroundColor Red
    Read-Host "Enter to close"; exit 1
}

Write-Host "== DsHidMini Bluetooth rumble fix installer ==" -ForegroundColor Cyan
Write-Host "This will trust a self-signed cert, install a test-signed driver, and enable Test Signing." -ForegroundColor Yellow
if ((Read-Host "Continue? (y/N)") -notmatch '^(y|Y)') { Write-Host "Aborted."; exit }

# Secure Boot check (Test Signing won't take effect if Secure Boot is on)
try {
    if (Confirm-SecureBootUEFI) {
        Write-Host "WARNING: Secure Boot is ON. Test Signing will not activate until you disable Secure Boot in UEFI/BIOS." -ForegroundColor Red
    }
} catch { }

Write-Host "[1/3] Trusting certificate..." -ForegroundColor Cyan
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null

Write-Host "[2/3] Installing driver package..." -ForegroundColor Cyan
pnputil /add-driver $inf /install

Write-Host "[3/3] Enabling Test Signing..." -ForegroundColor Cyan
$ts = 'testsigning'
& bcdedit.exe /set $ts on

Write-Host ""
Write-Host "Done. A REBOOT is required for the driver to load." -ForegroundColor Green
Write-Host "After reboot: plug the controller in over USB for ~5s (re-pairs), unplug, press PS to" -ForegroundColor Green
Write-Host "connect over Bluetooth. Rumble will work wirelessly." -ForegroundColor Green
Write-Host ""
if ((Read-Host "Reboot now? (y/N)") -match '^(y|Y)') { Restart-Computer } else { Write-Host "Reboot later to finish." }
