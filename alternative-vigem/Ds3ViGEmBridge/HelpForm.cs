namespace Ds3ViGEmBridge;

public sealed class HelpForm : Form
{
    public HelpForm()
    {
        Text = "How to use — DS3 ViGEm Bridge";
        Width = 660; Height = 600; StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false; MaximizeBox = false;

        var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 34 };
        close.Click += (_, _) => Close();
        Controls.Add(close);

        var tb = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            Text = HelpText.Replace("\n", Environment.NewLine),
        };
        var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
        pad.Controls.Add(tb);
        Controls.Add(pad);
    }

    private const string HelpText =
@"DS3 to ViGEm bridge + rumble tester
====================================

This tool has two tabs. Pick the one that matches what you want.


TAB 1 — XInput rumble tester   (use this to FEEL vibration now)
--------------------------------------------------------------
Works with your current setup: the fixed DsHidMini driver, controller over
Bluetooth. No DS3 removal needed.

  1. Connect the DS3 (it appears as an Xbox pad through DsHidMini).
  2. Leave the Controller picker on 'Auto' (or choose a slot).
  3. Move the Large / Small sliders and click 'Apply now', or use the
     Test 1 s / Hold / Pulse / Stop buttons.
  4. You should feel it immediately. The log shows what was sent and which
     slot responded.

This is the simplest way to confirm the Bluetooth rumble fix is working.


TAB 2 — DS3 direct + ViGEm bridge   (the 'no Test Mode' alternative)
-------------------------------------------------------------------
This path talks to the DS3 DIRECTLY and drives a virtual Xbox 360 pad through
the officially signed ViGEmBus — so it needs no Test Mode. But it needs the
DS3 as a raw USB HID device, which means DsHidMini must NOT own it.

  - If you see 'DS3 not found' while the pad is connected, that is because
    DsHidMini currently owns it. In that state the Output report log still
    prints the report bytes (for learning), but nothing physically vibrates
    because no DS3 handle is open.

  - To use it for real:
      1. Remove DsHidMini (or bind the DS3 to WinUSB).
      2. Connect the DS3 over USB.
      3. Click 'Open DS3 (USB)'.
      4. Click 'Start bridge -> Xbox 360'. The DS3 now works as a virtual
         Xbox pad with NO Test Mode, and game rumble is forwarded back to it.

  - The Channel dropdown (USB / BT 0xA2 / BT 0x52) changes the byte framing
    shown in the log, so you can SEE why the control channel (0x52) is ignored
    and the interrupt channel (0xA2) works over Bluetooth. That is exactly the
    one-line difference the driver fix changes.


Requirements
------------
  - ViGEmBus installed (for Tab 2):  https://github.com/nefarius/ViGEmBus
  - .NET 8 Desktop Runtime (for the released .exe).
  - Tab 1 works regardless; Tab 2 needs the DS3 free of DsHidMini.


More: https://github.com/Cybertiron/ps3_bt_rumble_final_fix
";
}
