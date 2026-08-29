# DsHidMini — Bluetooth rumble fix (DualShock 3)

A one-line patch to [**nefarius/DsHidMini**](https://github.com/nefarius/DsHidMini) that makes
**DualShock 3 / SIXAXIS rumble work over Bluetooth**, plus a ready-to-install prebuilt driver so
you don't have to set up the WDK/EWDK yourself.

> This is **not** a separate driver. All the hard work is [Nefarius'](https://github.com/nefarius)
> DsHidMini. This repo only carries a small fix on top of it and a convenience build. The proper
> home for the fix is the upstream pull request — see below.

[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-support-FFDD00?logo=buymeacoffee&logoColor=black)](https://buymeacoffee.com/cybertiron)

## The problem

DS3 rumble works over USB but has **never** worked over Bluetooth with DsHidMini. LED changes are
applied over BT, but the motors never actuate wirelessly. This is a long-standing, previously
unresolved issue:

- Discussion: [nefarius/DsHidMini#97](https://github.com/nefarius/DsHidMini/discussions/97)
- Issue: [nefarius/DsHidMini#4](https://github.com/nefarius/DsHidMini/issues/4)

## Root cause

On Bluetooth the output report (LED + rumble) is sent over the HID **control** channel
(`IOCTL_BTHPS3_HID_CONTROL_WRITE`, `0x52` = SET_REPORT|Output). The DS3 firmware honours LED state
from a control-channel Set_Report but **ignores the rumble bytes** delivered that way. Over USB the
same report is written to the **interrupt OUT** endpoint — which is exactly why rumble works wired.

## The fix

Send the Bluetooth output report over the HID **interrupt** channel
(`IOCTL_BTHPS3_HID_INTERRUPT_WRITE`, `0xA2` = DATA|Output), mirroring the USB path. Three lines:

| File | Change |
|------|--------|
| `driver/Device.c` | output writer module IOCTL → `IOCTL_BTHPS3_HID_INTERRUPT_WRITE` |
| `driver/OutputReport.c` | send-call IOCTL → `IOCTL_BTHPS3_HID_INTERRUPT_WRITE` |
| `driver/Ds3.c` | `G_Ds3BthHidOutputReport[0]` `0x52` → `0xA2` |

See [`dshidmini-bt-rumble.patch`](dshidmini-bt-rumble.patch).

**Verified on real hardware** (SIXAXIS over BthPS3, XInput mode): rumble now actuates over
Bluetooth, LED state is unaffected. Also confirmed independently in-browser via the Gamepad API at
[hardwaretester.com/gamepad](https://hardwaretester.com/gamepad) — the pad shows `VIBRATION: Yes`
and both its "Vibration, 1 sec" and "Vibration, infinite" buttons drive the motors over BT. A single
output report runs the motor for ~1 s (the DS3 duration byte); games that stream rumble refresh it
and sustain continuous vibration (confirmed 6 s).

## Upstream

Pull request: **[nefarius/DsHidMini#460](https://github.com/nefarius/DsHidMini/pull/460)** — please
👍 there so it lands in an official, Microsoft-signed release. Once it does, you won't need any of
the test-signing steps below.

---

## ⚠️ Security warning — read before installing the prebuilt driver

The release binary is **test-signed with a self-signed certificate**, not signed by Microsoft.
To use it you must:

1. Trust the included certificate (import it into your machine's trusted stores), and
2. Enable Windows **Test Signing** mode (`bcdedit /set testsigning on` — leaves a "Test Mode"
   desktop watermark and lowers a driver-signing security boundary system-wide).

Trusting the certificate means your machine will accept **anything** signed by that cert's private
key. Only do this if you understand and accept that risk. If you'd rather not: build it yourself and
sign with your own cert (see below), or wait for the upstream PR to ship an officially signed build.

Use at your own risk. Provided as-is, no warranty.

## Install (prebuilt)

**Easiest:** download the zip from [Releases](../../releases), extract, and run
[`install.ps1`](install.ps1) — it self-elevates and does everything below (trust cert → install
driver → enable Test Signing → offer reboot). To undo, run [`uninstall.ps1`](uninstall.ps1).

Prefer to do it by hand? In an **elevated PowerShell**:

```powershell
# 1. trust the test certificate
Import-Certificate -FilePath .\DsHidMiniTest.cer -CertStoreLocation Cert:\LocalMachine\Root
Import-Certificate -FilePath .\DsHidMiniTest.cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher

# 2. enable test signing (requires Secure Boot to be OFF), then REBOOT
bcdedit /set testsigning on
```

After rebooting (you'll see the "Test Mode" watermark), install the driver:

```powershell
pnputil /add-driver .\dshidmini.inf /install
```

Then re-pair the controller: plug it in over **USB** for a few seconds (BthPS3 rewrites the host
address), unplug, press the **PS** button to reconnect over Bluetooth, and rumble will work.

## Revert

```powershell
# restore the official driver first (Nefarius updater), THEN turn test signing off + reboot
bcdedit /set testsigning off
```
(Do not turn test signing off while the test-signed driver is still the active one, or the device
may fail to start until you reinstall the official package.)

## Build from source

Requires the EWDK (self-contained; no Visual Studio integration needed). From an EWDK build prompt:

```
msbuild DMF\Dmf.sln /t:DmfU /p:Configuration=Release /p:Platform=x64
msbuild driver\dshidmini.vcxproj /p:Configuration=Release /p:Platform=x64 /p:SignMode=Off /p:SolutionDir=<repo-root>\
```

Then test-sign `dshidmini.dll`, regenerate the catalog with `inf2cat`, and sign the `.cat`.

## Support

If this fix saved you a headache, you can [**buy me a coffee** ☕](https://buymeacoffee.com/cybertiron)
— it keeps me debugging and shipping free fixes. Completely optional, and thank you!

## Credits

- **[Nefarius Software Solutions](https://github.com/nefarius)** — the DsHidMini driver and the
  entire DS3-on-Windows stack. This repo is only a small fix on top of their work.
- Root-cause analysis & fix by [@Cybertiron](https://github.com/Cybertiron).
