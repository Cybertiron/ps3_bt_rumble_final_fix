# Ds3ViGEmBridge — DS3 ↔ ViGEm bridge + rumble tester (prototype)

A small WinForms tool that demonstrates the **[ViGEmBus userland alternative](../docs/ALTERNATYVA-vigem-userland.md)**
(no Test Mode) and doubles as a **vibration tester** with a channel display.

> Prototype / educational. The main deliverable is still the driver fix — see the
> [repo root](../README.md) and [upstream PR #460](https://github.com/nefarius/DsHidMini/pull/460).

## What it does

**Tab 1 — XInput rumble tester** (works right now with your fixed DsHidMini over Bluetooth):
- Large/Small motor sliders, real-time "apply", **Test 1 s / Hold / Pulse / Stop**, slot picker, live log.
- Sends `XInputSetState` to any connected XInput pad — same idea as the browser tester, native.

**Tab 2 — DS3 direct + ViGEm bridge** (the userland alternative):
- Reads a DS3 over USB (HidSharp) → creates a virtual **Xbox 360** pad via **ViGEmBus** (no Test Mode).
- Forwards game rumble (ViGEm feedback) back to the DS3.
- Manual DS3 rumble tester with a **channel selector** — USB interrupt / **BT `0xA2`** / **BT `0x52`** —
  and a byte-level output-report log, so you can *see* why `0x52` (control) fails and `0xA2`
  (interrupt) works over Bluetooth (the exact thing the driver fix changes).

## How to use (step by step)

### To actually feel vibration right now → **Tab 1 (XInput rumble tester)**
This works with your current setup (fixed DsHidMini, controller over Bluetooth) — no DS3 removal needed.
1. Connect the DS3 (it shows up as an Xbox pad via DsHidMini).
2. Open **XInput rumble tester**.
3. Leave the controller picker on **Auto** (or pick the slot).
4. Drag **Large** / **Small** and hit **Apply now**, or use **Test 1 s** / **Hold** / **Pulse**.
   You should feel it immediately. The log shows what was sent + which slot answered.

### To try the userland alternative → **Tab 2 (DS3 direct + ViGEm bridge)**
This path talks to the DS3 **directly** and bypasses Test Mode — but it needs the DS3 as a raw USB
HID device, so **DsHidMini must not own it**.
- If you see **"DS3 not found"** while the pad is connected, that's because **DsHidMini currently
  owns it**. In that state the *Output report log still prints the report bytes* (educational), but
  nothing physically vibrates because no DS3 handle is open.
- To use it for real: remove DsHidMini (or bind the DS3 to WinUSB), connect the DS3 over USB, then
  **Open DS3 (USB)** → **Start bridge → Xbox 360**. Now the DS3 acts as a virtual Xbox pad with **no
  Test Mode**, and game rumble is forwarded back to it.
- The **Channel** dropdown (USB / BT `0xA2` / BT `0x52`) changes the byte framing shown in the log —
  so you can *see* why control-channel `0x52` is ignored and interrupt `0xA2` works over Bluetooth.

## Requirements

- **[ViGEmBus](https://github.com/nefarius/ViGEmBus)** installed (for Tab 2).
- **.NET 8 Desktop Runtime** (the released `.exe` is framework-dependent).
- For Tab 2 the DS3 must be reachable as a raw USB HID device — i.e. **DsHidMini must not own it**
  (remove DsHidMini, or bind the DS3 to WinUSB). Tab 1 works regardless.

## Run

Grab `Ds3ViGEmBridge.exe` from the [release](https://github.com/Cybertiron/ps3_bt_rumble_final_fix/releases),
or build it yourself:

```
dotnet build -c Release
# or a single-file exe:
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## Notes / honesty

- In this prototype the real transport for DS3 output is **USB interrupt OUT**, so rumble always
  works there; the BT channel options render the byte framing for teaching/demo. Wiring a real BT
  output path (via BthPS3 or raw L2CAP) is the harder extension — that's exactly why the driver
  (DsHidMini) exists.
- The input mapping is a straightforward DS3→X360 layout; tweak offsets in `Ds3Device.cs` if your
  pad/firmware differs.
