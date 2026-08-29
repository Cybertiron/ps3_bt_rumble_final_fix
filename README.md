# DsHidMini — Bluetooth rumble fix (DualShock 3)

A one-line patch to [**nefarius/DsHidMini**](https://github.com/nefarius/DsHidMini) that makes
**DualShock 3 / SIXAXIS rumble work over Bluetooth**, plus a ready-to-install prebuilt driver so
you don't have to set up the WDK/EWDK yourself.

> This is **not** a separate driver. All the hard work is [Nefarius'](https://github.com/nefarius)
> DsHidMini. This repo only carries a small fix on top of it and a convenience build. The proper
> home for the fix is the upstream pull request — see below.

[![Buy Me A Coffee](https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20coffee&emoji=%E2%98%95&slug=cybertiron&button_colour=FFDD00&font_colour=000000&font_family=Cookie&outline_colour=000000&coffee_colour=ffffff)](https://buymeacoffee.com/cybertiron)

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

## How this fix was found

1. **The key symptom.** Rumble worked over USB but never over Bluetooth — yet **LED changes *did*
   apply over BT**. That was the whole clue: if LED updates arrive, the output reports are reaching
   the pad. So the rumble bytes weren't being *lost* — the DS3 was **receiving and ignoring** them.
2. **Read the source, compare the two transports.** In DsHidMini the USB path writes the output
   report to the **interrupt OUT** endpoint, while the Bluetooth path sends the *same* report over
   the HID **control** channel (`0x52` Set_Report). Same bytes, different channel.
3. **Hypothesis.** The DS3 firmware accepts LED from a control-channel Set_Report but only actuates
   the motors from an **interrupt-channel** report (`0xA2` DATA|Output) — the form the real PS3 and
   the USB path use.
4. **Patch + build.** Three lines to route BT output over `IOCTL_BTHPS3_HID_INTERRUPT_WRITE` with a
   `0xA2` prefix. Built with the EWDK, test-signed, and installed under Test Signing.
5. **Verify on hardware.** Direct `XInputSetState` from PowerShell, then the browser Gamepad API
   ([hardwaretester.com/gamepad](https://hardwaretester.com/gamepad)), then a 6-second sustained
   test — all three showed the motors running **over Bluetooth**. Fixed. ✅

## Upstream

Pull request: **[nefarius/DsHidMini#460](https://github.com/nefarius/DsHidMini/pull/460)** — please
👍 there so it lands in an official, Microsoft-signed release. Once it does, you won't need any of
the test-signing steps below.

---

## Install — pick a path

Two ways to get Bluetooth rumble, plus the future official one. **Only Option A touches Test Mode**;
Option B needs no cert, no Test Mode, and nothing to undo.

### Option A — install the fixed driver

The release ships a **test-signed** build of the patched DsHidMini. Easiest: extract the zip and run
[`install.ps1`](install.ps1) — it self-elevates and does everything (trust cert → install driver →
enable Test Signing → offer reboot). `uninstall.ps1` reverts it. Prefer to do it by hand? In an
**elevated PowerShell**:

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

*A note on the cert:* the binary is signed with a self-signed certificate (not Microsoft's), so this
path needs Test Signing on. Trusting the cert means your machine trusts anything signed by its key —
whose private key only lives on my machine. It's the usual community-driver setup; fine for a
personal PC if you accept that. Reverting is easy — see [Revert](#revert-option-a). Provided as-is,
no warranty.

### Option B — inject via ViGEmBus (no Test Mode, nothing to revert)

No driver install, no cert, no Test Mode — just a portable app. **Extract the tool zip
(`Ds3ViGEmBridge-tool-v1.0.0.zip`) → run `Ds3ViGEmBridge.exe` → follow the GUI.** In-app **Help → How
to use** and the hover-tooltips walk you through it; nothing to undo afterwards — just close it.

Under the hood it drives the **officially-signed ViGEmBus** to inject a virtual Xbox 360 pad from the
DS3. Two prerequisites: **[ViGEmBus](https://github.com/nefarius/ViGEmBus) installed**, and for the
direct path the DS3 must be **free of DsHidMini** (this path runs instead of it). Prototype — details
in [docs/ALTERNATYVA-vigem-userland.md](docs/ALTERNATYVA-vigem-userland.md) and
[alternative-vigem/](alternative-vigem/). Same `0xA2` interrupt-channel rule applies for BT rumble.

### Option C — wait for official signing

Once the [upstream PR](https://github.com/nefarius/DsHidMini/pull/460) merges and Nefarius ships an
officially signed build, install that — no Test Mode, no cert, none of the above.

## Revert (Option A)

Only needed if you took **Option A** (the driver). **Option B (inject) has nothing to revert** — just
close the app; uninstall ViGEmBus normally if you ever want to.

For Option A, in an **elevated PowerShell**:

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

If this fix saved you a headache, you can buy me a coffee — it keeps me debugging and shipping free
fixes. Completely optional, and thank you!

[![Buy Me A Coffee](https://img.buymeacoffee.com/button-api/?text=Buy%20me%20a%20coffee&emoji=%E2%98%95&slug=cybertiron&button_colour=FFDD00&font_colour=000000&font_family=Cookie&outline_colour=000000&coffee_colour=ffffff)](https://www.buymeacoffee.com/cybertiron)

## Credits

- **[Nefarius Software Solutions](https://github.com/nefarius)** — the DsHidMini driver and the
  entire DS3-on-Windows stack. This repo is only a small fix on top of their work.
- Root-cause analysis & fix by [@Cybertiron](https://github.com/Cybertiron).
