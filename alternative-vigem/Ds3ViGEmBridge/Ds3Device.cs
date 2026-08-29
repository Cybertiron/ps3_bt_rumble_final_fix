using HidSharp;

namespace Ds3ViGEmBridge;

/// <summary>How a DS3 output (rumble/LED) report is framed.</summary>
public enum Ds3OutputChannel
{
    /// <summary>USB interrupt OUT endpoint. The only real transport in this prototype; rumble works.</summary>
    UsbInterrupt,
    /// <summary>Bluetooth HID interrupt channel, DATA|Output prefix 0xA2. The channel our DsHidMini fix uses.</summary>
    BtInterrupt0xA2,
    /// <summary>Bluetooth HID control channel, SET_REPORT|Output prefix 0x52. DS3 ignores rumble here (the bug).</summary>
    BtControl0x52,
}

public sealed class Ds3InputState
{
    public bool Cross, Circle, Square, Triangle;
    public bool L1, R1, L3, R3, Start, Select, Ps;
    public bool Up, Down, Left, Right;
    public byte LX = 128, LY = 128, RX = 128, RY = 128;   // 0..255, 128 = center
    public byte L2Analog, R2Analog;                        // 0..255
}

/// <summary>
/// Minimal DS3 (SIXAXIS) USB HID access: enable reporting, read input, write rumble/LED output.
/// NOTE: requires the DS3 bound to the generic Windows HID driver (i.e. DsHidMini NOT owning it).
/// </summary>
public sealed class Ds3Device : IDisposable
{
    public const int VendorId = 0x054C;
    public const int ProductId = 0x0268;

    private readonly HidDevice _dev;
    private readonly HidStream _stream;
    private readonly byte[] _inBuf;

    public string Info { get; }

    private Ds3Device(HidDevice dev, HidStream stream)
    {
        _dev = dev;
        _stream = stream;
        _inBuf = new byte[Math.Max(64, dev.GetMaxInputReportLength())];
        Info = SafeName(dev);
    }

    private static string SafeName(HidDevice d)
    {
        try { return d.GetFriendlyName(); } catch { return $"VID_{VendorId:X4}&PID_{ProductId:X4}"; }
    }

    public static Ds3Device? TryOpen(out string message)
    {
        try
        {
            var dev = DeviceList.Local.GetHidDevices(VendorId, ProductId).FirstOrDefault();
            if (dev is null) { message = "DS3 not found (VID 054C / PID 0268). Connect over USB."; return null; }
            if (!dev.TryOpen(out var stream))
            {
                message = "DS3 found but could not open it. It is likely owned by DsHidMini — " +
                          "remove that driver (or bind the DS3 to WinUSB) to use this userland path.";
                return null;
            }
            var d = new Ds3Device(dev, stream);
            d.SendEnable();
            message = $"DS3 opened: {d.Info}";
            return d;
        }
        catch (Exception ex) { message = "Open failed: " + ex.Message; return null; }
    }

    /// <summary>DS3 USB "start reporting" feature report (0xF4 = {0x42,0x03,0x00,0x00}).</summary>
    public void SendEnable()
    {
        try { _stream.SetFeature(new byte[] { 0xF4, 0x42, 0x03, 0x00, 0x00 }); } catch { /* some stacks reject; input may still stream */ }
    }

    /// <summary>Read one input report and parse it. Returns false on timeout / non-input report.</summary>
    public bool ReadState(out Ds3InputState state, int timeoutMs = 50)
    {
        state = new Ds3InputState();
        _stream.ReadTimeout = timeoutMs;
        int n;
        try { n = _stream.Read(_inBuf, 0, _inBuf.Length); }
        catch (TimeoutException) { return false; }
        if (n < 20 || _inBuf[0] != 0x01) return false;

        byte b1 = _inBuf[2];   // Select L3 R3 Start Up Right Down Left
        byte b2 = _inBuf[3];   // L2 R2 L1 R1 Triangle Circle Cross Square
        byte b3 = _inBuf[4];   // PS

        state.Select = (b1 & 0x01) != 0; state.L3 = (b1 & 0x02) != 0; state.R3 = (b1 & 0x04) != 0; state.Start = (b1 & 0x08) != 0;
        state.Up = (b1 & 0x10) != 0; state.Right = (b1 & 0x20) != 0; state.Down = (b1 & 0x40) != 0; state.Left = (b1 & 0x80) != 0;
        state.L1 = (b2 & 0x04) != 0; state.R1 = (b2 & 0x08) != 0;
        state.Triangle = (b2 & 0x10) != 0; state.Circle = (b2 & 0x20) != 0; state.Cross = (b2 & 0x40) != 0; state.Square = (b2 & 0x80) != 0;
        state.Ps = (b3 & 0x01) != 0;

        state.LX = _inBuf[6]; state.LY = _inBuf[7]; state.RX = _inBuf[8]; state.RY = _inBuf[9];
        if (n > 19) { state.L2Analog = _inBuf[18]; state.R2Analog = _inBuf[19]; }
        return true;
    }

    /// <summary>
    /// Build a DS3 output report (rumble + LED). Layout is the classic report id 0x01 form.
    /// The channel only changes the leading transport byte (educational): USB has none,
    /// BT interrupt uses 0xA2, BT control uses 0x52.
    /// </summary>
    public static byte[] BuildOutputReport(byte largeMotor, byte smallMotor, byte ledMask,
        Ds3OutputChannel channel, out string prefixDescription)
    {
        // report id 0x01 form (offsets match DsHidMini's G_Ds3UsbHidOutputReport)
        byte[] core =
        {
            0x01,               // [0] report id
            0x00,               // [1]
            0xFF,               // [2] small motor duration
            (byte)(smallMotor > 0 ? 0x01 : 0x00), // [3] small motor on/off
            0xFF,               // [4] large motor duration
            largeMotor,         // [5] large motor strength
            0x00, 0x00, 0x00, 0x00,
            ledMask,            // [10] LED bitmask (0x02=LED1 .. 0x10=LED4)
            0xFF,0x27,0x10,0x00,0x32,
            0xFF,0x27,0x10,0x00,0x32,
            0xFF,0x27,0x10,0x00,0x32,
            0xFF,0x27,0x10,0x00,0x32,
            0x00,0x00,0x00,0x00,0x00,
            0x00,0x00,0x00,0x00,0x00,
            0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
        };

        switch (channel)
        {
            case Ds3OutputChannel.BtInterrupt0xA2:
                prefixDescription = "0xA2 (BT DATA|Output, interrupt channel) — rumble ACTUATES";
                return Prepend(0xA2, core);
            case Ds3OutputChannel.BtControl0x52:
                prefixDescription = "0x52 (BT SET_REPORT|Output, control channel) — DS3 IGNORES rumble";
                return Prepend(0x52, core);
            default:
                prefixDescription = "USB interrupt OUT (no BT prefix) — rumble ACTUATES";
                return core;
        }
    }

    private static byte[] Prepend(byte b, byte[] rest)
    {
        var r = new byte[rest.Length + 1];
        r[0] = b;
        Array.Copy(rest, 0, r, 1, rest.Length);
        return r;
    }

    /// <summary>
    /// Write a rumble/LED report to the physical DS3. In this prototype the real transport is USB
    /// interrupt OUT, so only the USB-framed report is actually sent to the device; BT-framed
    /// variants are for byte-level display until a real BT transport is wired in.
    /// </summary>
    public void WriteRumble(byte largeMotor, byte smallMotor, byte ledMask = 0x02)
    {
        var report = BuildOutputReport(largeMotor, smallMotor, ledMask, Ds3OutputChannel.UsbInterrupt, out _);
        try { _stream.Write(report); } catch { /* device may have detached */ }
    }

    public void Dispose()
    {
        try { WriteRumble(0, 0, 0x00); } catch { }
        _stream?.Dispose();
    }
}
