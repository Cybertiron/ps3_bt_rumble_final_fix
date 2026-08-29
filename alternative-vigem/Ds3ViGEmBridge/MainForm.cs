using System.Runtime.InteropServices;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace Ds3ViGEmBridge;

public sealed class MainForm : Form
{
    // ---- XInput P/Invoke (for the XInput rumble tester tab) ----
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION { public ushort Left; public ushort Right; }
    [DllImport("XInput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern uint XInputSetState(uint index, ref XINPUT_VIBRATION vib);

    // ---- XInput tab controls ----
    private readonly ComboBox _cmbSlot = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly TrackBar _xiLarge = new() { Minimum = 0, Maximum = 65535, TickFrequency = 8192, Width = 320 };
    private readonly TrackBar _xiSmall = new() { Minimum = 0, Maximum = 65535, TickFrequency = 8192, Width = 320 };
    private readonly Label _xiLargeVal = new() { AutoSize = true, Text = "0" };
    private readonly Label _xiSmallVal = new() { AutoSize = true, Text = "0" };
    private readonly Label _xiStatus = new() { AutoSize = true, Text = "—" };
    private readonly TextBox _xiLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 8.5f) };
    private readonly System.Windows.Forms.Timer _pulseTimer = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer _autoOff = new() { Interval = 1000 };
    private bool _pulseOn;

    // ---- DS3 / ViGEm tab controls ----
    private readonly Label _ds3Status = new() { AutoSize = true, Text = "DS3: not opened" };
    private readonly Label _vigemStatus = new() { AutoSize = true, Text = "ViGEm: idle" };
    private readonly Label _feedback = new() { AutoSize = true, Text = "Incoming game rumble: L=0 S=0" };
    private readonly ComboBox _cmbChannel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
    private readonly TrackBar _dsLarge = new() { Minimum = 0, Maximum = 255, TickFrequency = 32, Width = 320 };
    private readonly TrackBar _dsSmall = new() { Minimum = 0, Maximum = 255, TickFrequency = 32, Width = 320 };
    private readonly RichTextBox _dsLog = new() { ReadOnly = true, Dock = DockStyle.Fill, Font = new Font("Consolas", 8.5f) };
    private readonly System.Windows.Forms.Timer _dsPulse = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer _dsAutoOff = new() { Interval = 1000 };
    private bool _dsPulseOn;

    private Ds3Device? _ds3;
    private ViGEmClient? _vigem;
    private IXbox360Controller? _pad;
    private CancellationTokenSource? _bridgeCts;
    private volatile byte _lastLarge, _lastSmall;

    private readonly ToolTip _tips = new() { AutoPopDelay = 20000, InitialDelay = 300, ReshowDelay = 100, ShowAlways = true };

    public MainForm()
    {
        Text = "DS3 ↔ ViGEm bridge + rumble tester";
        Width = 780; Height = 640; StartPosition = FormStartPosition.CenterScreen;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildXInputTab());
        tabs.TabPages.Add(BuildDs3Tab());
        Controls.Add(tabs);

        var menu = new MenuStrip();
        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add(new ToolStripMenuItem("How to use…", null, (_, _) => { using var f = new HelpForm(); f.ShowDialog(this); }));
        help.DropDownItems.Add(new ToolStripMenuItem("About", null, (_, _) =>
            MessageBox.Show(this,
                "DS3 ↔ ViGEm bridge + rumble tester\n\nPart of ps3_bt_rumble_final_fix\nhttps://github.com/Cybertiron/ps3_bt_rumble_final_fix\n\nTip: hover any control for a short description.",
                "About", MessageBoxButtons.OK, MessageBoxIcon.Information)));
        menu.Items.Add(help);
        Controls.Add(menu);
        MainMenuStrip = menu;

        for (int i = 0; i < 4; i++) _cmbSlot.Items.Add($"Slot {i}");
        _cmbSlot.Items.Insert(0, "Auto");
        _cmbSlot.SelectedIndex = 0;

        _cmbChannel.Items.AddRange(new object[]
        {
            "USB interrupt OUT (rumble works)",
            "BT interrupt 0xA2 (the fix — rumble works)",
            "BT control 0x52 (the bug — DS3 ignores rumble)",
        });
        _cmbChannel.SelectedIndex = 0;

        _pulseTimer.Tick += (_, _) => { _pulseOn = !_pulseOn; SendXInput(_pulseOn ? (ushort)_xiLarge.Value : (ushort)0, _pulseOn ? (ushort)_xiSmall.Value : (ushort)0, "pulse"); };
        _autoOff.Tick += (_, _) => { _autoOff.Stop(); SendXInput(0, 0, "auto-off"); };
        _dsPulse.Tick += (_, _) => { _dsPulseOn = !_dsPulseOn; SendDs3(_dsPulseOn ? (byte)_dsLarge.Value : (byte)0, _dsPulseOn ? (byte)_dsSmall.Value : (byte)0, "pulse"); };
        _dsAutoOff.Tick += (_, _) => { _dsAutoOff.Stop(); SendDs3(0, 0, "auto-off"); };

        FormClosing += (_, _) => Cleanup();
    }

    // =================== XInput tab ===================
    private TabPage BuildXInputTab()
    {
        var page = new TabPage("XInput rumble tester");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        top.Controls.Add(new Label { AutoSize = true, Text = "Sends vibration to any XInput controller — works with your fixed DsHidMini over Bluetooth. No DS3 access needed here." });
        top.Controls.Add(Row(new Label { AutoSize = true, Text = "Controller:" }, _cmbSlot, _xiStatus));
        top.Controls.Add(Row(new Label { AutoSize = true, Width = 90, Text = "Large motor" }, _xiLarge, _xiLargeVal));
        top.Controls.Add(Row(new Label { AutoSize = true, Width = 90, Text = "Small motor" }, _xiSmall, _xiSmallVal));

        var btnApply = new Button { Text = "Apply now", AutoSize = true };
        var btn1s = new Button { Text = "Test 1 s", AutoSize = true };
        var btnHold = new Button { Text = "Hold", AutoSize = true };
        var btnPulse = new Button { Text = "Pulse", AutoSize = true };
        var btnStop = new Button { Text = "Stop", AutoSize = true };
        top.Controls.Add(Row(btnApply, btn1s, btnHold, btnPulse, btnStop));

        _xiLarge.ValueChanged += (_, _) => { _xiLargeVal.Text = _xiLarge.Value.ToString(); if (btnHold.Tag is bool b && b) SendXInput((ushort)_xiLarge.Value, (ushort)_xiSmall.Value, "slider"); };
        _xiSmall.ValueChanged += (_, _) => { _xiSmallVal.Text = _xiSmall.Value.ToString(); if (btnHold.Tag is bool b2 && b2) SendXInput((ushort)_xiLarge.Value, (ushort)_xiSmall.Value, "slider"); };
        btnApply.Click += (_, _) => SendXInput((ushort)_xiLarge.Value, (ushort)_xiSmall.Value, "apply");
        btn1s.Click += (_, _) => { SendXInput((ushort)_xiLarge.Value, (ushort)_xiSmall.Value, "test1s"); _autoOff.Stop(); _autoOff.Start(); };
        btnHold.Click += (_, _) => { bool on = !(btnHold.Tag is bool t && t); btnHold.Tag = on; btnHold.Text = on ? "Hold (on)" : "Hold"; SendXInput(on ? (ushort)_xiLarge.Value : (ushort)0, on ? (ushort)_xiSmall.Value : (ushort)0, "hold"); };
        btnPulse.Click += (_, _) => { if (_pulseTimer.Enabled) { _pulseTimer.Stop(); SendXInput(0, 0, "pulse-stop"); btnPulse.Text = "Pulse"; } else { _pulseTimer.Start(); btnPulse.Text = "Pulse (on)"; } };
        btnStop.Click += (_, _) => { _pulseTimer.Stop(); _autoOff.Stop(); btnHold.Tag = false; btnHold.Text = "Hold"; btnPulse.Text = "Pulse"; SendXInput(0, 0, "stop"); };

        _tips.SetToolTip(_cmbSlot, "Which XInput controller to buzz. 'Auto' sends to every connected pad.");
        _tips.SetToolTip(_xiLarge, "Left / large motor — low-frequency, strong rumble (0–65535).");
        _tips.SetToolTip(_xiSmall, "Right / small motor — high-frequency, weak rumble (0–65535).");
        _tips.SetToolTip(btnApply, "Send the current slider values once, right now.");
        _tips.SetToolTip(btn1s, "Buzz at the current values for 1 second, then auto-stop.");
        _tips.SetToolTip(btnHold, "Toggle continuous buzz; the sliders update it live while it's on.");
        _tips.SetToolTip(btnPulse, "Toggle an on/off pulsing pattern (every 250 ms).");
        _tips.SetToolTip(btnStop, "Stop all vibration.");

        root.Controls.Add(top, 0, 0);
        var logBox = new GroupBox { Text = "Log", Dock = DockStyle.Fill };
        logBox.Controls.Add(_xiLog);
        root.Controls.Add(logBox, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private void SendXInput(ushort large, ushort small, string reason)
    {
        int start = 0, end = 3;
        if (_cmbSlot.SelectedIndex > 0) { start = _cmbSlot.SelectedIndex - 1; end = start; }
        bool any = false;
        for (int i = start; i <= end; i++)
        {
            var v = new XINPUT_VIBRATION { Left = large, Right = small };
            uint rc = XInputSetState((uint)i, ref v);
            if (rc == 0) { any = true; if (_cmbSlot.SelectedIndex == 0) { LogXi($"slot {i}: L={large} S={small} ({reason})"); } }
        }
        if (_cmbSlot.SelectedIndex > 0) LogXi($"slot {start}: L={large} S={small} ({reason})");
        _xiStatus.Text = any ? "connected ✓" : "no XInput pad in selected slot";
    }

    private void LogXi(string s) => AppendLine(_xiLog, $"{DateTime.Now:HH:mm:ss.fff}  {s}");

    // =================== DS3 / ViGEm tab ===================
    private TabPage BuildDs3Tab()
    {
        var page = new TabPage("DS3 direct + ViGEm bridge");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        top.Controls.Add(new Label { AutoSize = true, Text = "Userland path: read DS3 over USB → virtual Xbox 360 via ViGEmBus (no Test Mode). Requires the DS3 NOT owned by DsHidMini." });

        var btnOpen = new Button { Text = "Open DS3 (USB)", AutoSize = true };
        top.Controls.Add(Row(btnOpen, _ds3Status));
        var btnBridge = new Button { Text = "Start bridge → Xbox 360", AutoSize = true };
        top.Controls.Add(Row(btnBridge, _vigemStatus));
        top.Controls.Add(_feedback);

        top.Controls.Add(new Label { AutoSize = true, Text = "— Manual DS3 rumble tester —", Font = new Font(Font, FontStyle.Bold) });
        top.Controls.Add(Row(new Label { AutoSize = true, Width = 90, Text = "Channel" }, _cmbChannel));
        top.Controls.Add(new Label { AutoSize = true, ForeColor = Color.DimGray, Text = "Note: real transport here is USB (interrupt). BT channels show the byte-level framing that\nmakes the difference over Bluetooth (0x52 control = ignored, 0xA2 interrupt = works)." });
        top.Controls.Add(Row(new Label { AutoSize = true, Width = 90, Text = "Large" }, _dsLarge));
        top.Controls.Add(Row(new Label { AutoSize = true, Width = 90, Text = "Small" }, _dsSmall));

        var b1 = new Button { Text = "Test 1 s", AutoSize = true };
        var bh = new Button { Text = "Hold", AutoSize = true };
        var bp = new Button { Text = "Pulse", AutoSize = true };
        var bs = new Button { Text = "Stop", AutoSize = true };
        top.Controls.Add(Row(b1, bh, bp, bs));

        btnOpen.Click += (_, _) =>
        {
            _ds3?.Dispose();
            _ds3 = Ds3Device.TryOpen(out var msg);
            _ds3Status.Text = "DS3: " + msg;
        };
        btnBridge.Click += (_, _) => { if (_bridgeCts is null) StartBridge(); else StopBridge(); btnBridge.Text = _bridgeCts is null ? "Start bridge → Xbox 360" : "Stop bridge"; };

        b1.Click += (_, _) => { SendDs3((byte)_dsLarge.Value, (byte)_dsSmall.Value, "test1s"); _dsAutoOff.Stop(); _dsAutoOff.Start(); };
        bh.Click += (_, _) => { bool on = !(bh.Tag is bool t && t); bh.Tag = on; bh.Text = on ? "Hold (on)" : "Hold"; SendDs3(on ? (byte)_dsLarge.Value : (byte)0, on ? (byte)_dsSmall.Value : (byte)0, "hold"); };
        bp.Click += (_, _) => { if (_dsPulse.Enabled) { _dsPulse.Stop(); SendDs3(0, 0, "pulse-stop"); bp.Text = "Pulse"; } else { _dsPulse.Start(); bp.Text = "Pulse (on)"; } };
        bs.Click += (_, _) => { _dsPulse.Stop(); _dsAutoOff.Stop(); bh.Tag = false; bh.Text = "Hold"; bp.Text = "Pulse"; SendDs3(0, 0, "stop"); };

        _tips.SetToolTip(btnOpen, "Open the DS3 as a raw USB HID device. Fails with 'not found' if DsHidMini owns it.");
        _tips.SetToolTip(btnBridge, "Start/stop feeding a virtual Xbox 360 pad (via ViGEmBus) from the DS3 — no Test Mode.");
        _tips.SetToolTip(_cmbChannel, "Output framing shown in the log: USB, BT interrupt 0xA2 (works), BT control 0x52 (DS3 ignores it).");
        _tips.SetToolTip(_dsLarge, "Large / heavy motor value (0–255).");
        _tips.SetToolTip(_dsSmall, "Small / light motor value (0–255).");
        _tips.SetToolTip(b1, "Buzz for 1 second.");
        _tips.SetToolTip(bh, "Toggle continuous buzz.");
        _tips.SetToolTip(bp, "Toggle a pulsing pattern.");
        _tips.SetToolTip(bs, "Stop.");
        _tips.SetToolTip(_feedback, "Live rumble coming from games into the virtual pad (forwarded to the DS3).");

        root.Controls.Add(top, 0, 0);
        var logBox = new GroupBox { Text = "Output report log", Dock = DockStyle.Fill };
        logBox.Controls.Add(_dsLog);
        root.Controls.Add(logBox, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private void SendDs3(byte large, byte small, string reason)
    {
        var ch = (Ds3OutputChannel)_cmbChannel.SelectedIndex;
        var report = Ds3Device.BuildOutputReport(large, small, 0x02, ch, out var chDesc);
        if (_ds3 is { } d) d.WriteRumble(large, small);   // real transport = USB interrupt
        AppendLine(_dsLog, $"{DateTime.Now:HH:mm:ss.fff}  L={large,-3} S={small,-3} [{chDesc}] ({reason})\n    {BytesToHex(report)}");
    }

    // =================== ViGEm bridge ===================
    private void StartBridge()
    {
        if (_ds3 is null || !_ds3.GetType().Name.Equals(nameof(Ds3Device))) { _vigemStatus.Text = "ViGEm: open the DS3 first"; return; }
        try
        {
            _vigem ??= new ViGEmClient();
            _pad = _vigem.CreateXbox360Controller();
            _pad.AutoSubmitReport = false;
            _pad.FeedbackReceived += (_, e) =>
            {
                _lastLarge = e.LargeMotor; _lastSmall = e.SmallMotor;
                if (_ds3 is { } d) d.WriteRumble(e.LargeMotor, e.SmallMotor);
                BeginInvoke(() => _feedback.Text = $"Incoming game rumble: L={e.LargeMotor} S={e.SmallMotor}  → forwarded to DS3");
            };
            _pad.Connect();
        }
        catch (Exception ex) { _vigemStatus.Text = "ViGEm: " + ex.Message + " (is ViGEmBus installed?)"; return; }

        _vigemStatus.Text = "ViGEm: bridging";
        _bridgeCts = new CancellationTokenSource();
        var ct = _bridgeCts.Token;
        Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                if (_ds3 is { } d && d.ReadState(out var st)) MapToPad(st);
            }
        }, ct);
    }

    private void MapToPad(Ds3InputState st)
    {
        if (_pad is null) return;
        _pad.SetButtonState(Xbox360Button.A, st.Cross);
        _pad.SetButtonState(Xbox360Button.B, st.Circle);
        _pad.SetButtonState(Xbox360Button.X, st.Square);
        _pad.SetButtonState(Xbox360Button.Y, st.Triangle);
        _pad.SetButtonState(Xbox360Button.LeftShoulder, st.L1);
        _pad.SetButtonState(Xbox360Button.RightShoulder, st.R1);
        _pad.SetButtonState(Xbox360Button.Back, st.Select);
        _pad.SetButtonState(Xbox360Button.Start, st.Start);
        _pad.SetButtonState(Xbox360Button.LeftThumb, st.L3);
        _pad.SetButtonState(Xbox360Button.RightThumb, st.R3);
        _pad.SetButtonState(Xbox360Button.Guide, st.Ps);
        _pad.SetButtonState(Xbox360Button.Up, st.Up);
        _pad.SetButtonState(Xbox360Button.Down, st.Down);
        _pad.SetButtonState(Xbox360Button.Left, st.Left);
        _pad.SetButtonState(Xbox360Button.Right, st.Right);
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger, st.L2Analog);
        _pad.SetSliderValue(Xbox360Slider.RightTrigger, st.R2Analog);
        _pad.SetAxisValue(Xbox360Axis.LeftThumbX, ToAxis(st.LX));
        _pad.SetAxisValue(Xbox360Axis.LeftThumbY, ToAxis((byte)(255 - st.LY)));
        _pad.SetAxisValue(Xbox360Axis.RightThumbX, ToAxis(st.RX));
        _pad.SetAxisValue(Xbox360Axis.RightThumbY, ToAxis((byte)(255 - st.RY)));
        _pad.SubmitReport();
    }

    private static short ToAxis(byte v) => (short)Math.Clamp((v - 128) * 258, short.MinValue, short.MaxValue);

    private void StopBridge()
    {
        _bridgeCts?.Cancel(); _bridgeCts = null;
        try { _pad?.Disconnect(); } catch { }
        _pad = null;
        _vigemStatus.Text = "ViGEm: idle";
    }

    // =================== helpers ===================
    private static string BytesToHex(byte[] b) => string.Join(" ", b.Take(12).Select(x => x.ToString("X2"))) + (b.Length > 12 ? " …" : "");

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        p.Controls.AddRange(controls);
        return p;
    }

    private static void AppendLine(TextBoxBase box, string s)
    {
        if (box.InvokeRequired) { box.BeginInvoke(() => AppendLine(box, s)); return; }
        box.AppendText(s + Environment.NewLine);
    }

    private void Cleanup()
    {
        _pulseTimer.Stop(); _autoOff.Stop(); _dsPulse.Stop(); _dsAutoOff.Stop();
        StopBridge();
        try { SendXInput(0, 0, "exit"); } catch { }
        _ds3?.Dispose();
        _vigem?.Dispose();
    }
}
