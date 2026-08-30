using HidSharp;

namespace Ds3ViGEmBridge;

/// <summary>DualShock 4 over USB HID. VID 054C, PID 05C4 (v1 / CUH-ZCT1) or 09CC (v2 / CUH-ZCT2).</summary>
public sealed class Ds4Device : IGamepad
{
    public const int VendorId = 0x054C;
    public static readonly int[] ProductIds = { 0x05C4, 0x09CC };

    private readonly HidStream _stream;
    private readonly byte[] _in;
    public string Info { get; }

    private Ds4Device(HidDevice dev, HidStream stream)
    {
        _stream = stream;
        _in = new byte[Math.Max(64, dev.GetMaxInputReportLength())];
        try { Info = dev.GetFriendlyName(); } catch { Info = "DualShock 4"; }
    }

    public static List<Ds4Device> OpenAll(out int failed)
    {
        failed = 0;
        var list = new List<Ds4Device>();
        var devs = DeviceList.Local.GetHidDevices()
            .Where(d => d.VendorID == VendorId && ProductIds.Contains(d.ProductID))
            .ToList();
        foreach (var dev in devs)
        {
            if (dev.TryOpen(out var stream)) list.Add(new Ds4Device(dev, stream));
            else failed++;
        }
        return list;
    }

    public bool ReadState(out PadState s, int timeoutMs = 50)
    {
        s = new PadState();
        _stream.ReadTimeout = timeoutMs;
        int n;
        try { n = _stream.Read(_in, 0, _in.Length); }
        catch (TimeoutException) { return false; }

        // USB input report id 0x01: analog + buttons start right after the report id.
        int o = _in[0] == 0x01 ? 1 : 0;
        if (n < o + 9) return false;

        s.LX = _in[o + 0]; s.LY = _in[o + 1]; s.RX = _in[o + 2]; s.RY = _in[o + 3];
        byte btn = _in[o + 4];   // dpad (low nibble) + face buttons (high nibble)
        byte sh = _in[o + 5];    // L1 R1 L2 R2 Share Options L3 R3
        byte ps = _in[o + 6];    // PS + touchpad click

        int hat = btn & 0x0F;    // 0=N,1=NE,2=E,3=SE,4=S,5=SW,6=W,7=NW,8=released
        s.Up = hat == 7 || hat == 0 || hat == 1;
        s.Right = hat == 1 || hat == 2 || hat == 3;
        s.Down = hat == 3 || hat == 4 || hat == 5;
        s.Left = hat == 5 || hat == 6 || hat == 7;

        s.Square = (btn & 0x10) != 0; s.Cross = (btn & 0x20) != 0;
        s.Circle = (btn & 0x40) != 0; s.Triangle = (btn & 0x80) != 0;
        s.L1 = (sh & 0x01) != 0; s.R1 = (sh & 0x02) != 0;
        s.Select = (sh & 0x10) != 0;   // Share -> Back
        s.Start = (sh & 0x20) != 0;    // Options -> Start
        s.L3 = (sh & 0x40) != 0; s.R3 = (sh & 0x80) != 0;
        s.Ps = (ps & 0x01) != 0;
        s.L2Analog = _in[o + 7]; s.R2Analog = _in[o + 8];
        return true;
    }

    public void WriteRumble(byte large, byte small)
    {
        // DS4 USB output report 0x05
        var r = new byte[32];
        r[0] = 0x05;
        r[1] = 0x07;      // flags: enable rumble + LED + flash
        r[4] = small;     // right / weak (high-frequency) motor
        r[5] = large;     // left / strong (low-frequency) motor
        r[6] = 0x00; r[7] = 0x00; r[8] = 0x40;   // LED colour (dim blue)
        try { _stream.Write(r); } catch { }
    }

    public void Dispose()
    {
        try { WriteRumble(0, 0); } catch { }
        _stream?.Dispose();
    }
}
