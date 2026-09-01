using HidSharp;

namespace Ds3ViGEmBridge;

/// <summary>DualSense (PS5) over USB HID. VID 054C, PID 0CE6 (DualSense) or 0DF2 (DualSense Edge).</summary>
public sealed class Ds5Device : IGamepad
{
    public const int VendorId = 0x054C;
    public static readonly int[] ProductIds = { 0x0CE6, 0x0DF2 };

    private readonly HidStream _stream;
    private readonly byte[] _in;
    public string Info { get; }

    private Ds5Device(HidDevice dev, HidStream stream)
    {
        _stream = stream;
        _in = new byte[Math.Max(64, dev.GetMaxInputReportLength())];
        try { Info = dev.GetFriendlyName(); } catch { Info = "DualSense"; }
    }

    public static List<Ds5Device> OpenAll(out int failed)
    {
        failed = 0;
        var list = new List<Ds5Device>();
        foreach (var dev in DeviceList.Local.GetHidDevices()
                     .Where(d => d.VendorID == VendorId && ProductIds.Contains(d.ProductID)))
        {
            if (dev.TryOpen(out var stream)) list.Add(new Ds5Device(dev, stream));
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

        int o = _in[0] == 0x01 ? 1 : 0;      // USB input report id 0x01
        if (n < o + 10) return false;

        s.LX = _in[o + 0]; s.LY = _in[o + 1]; s.RX = _in[o + 2]; s.RY = _in[o + 3];
        s.L2Analog = _in[o + 4]; s.R2Analog = _in[o + 5];
        // _in[o+6] = sequence counter
        byte btn = _in[o + 7];   // dpad (low nibble) + face (high nibble)
        byte sh = _in[o + 8];    // L1 R1 L2 R2 Create Options L3 R3
        byte ps = _in[o + 9];    // PS + touchpad + mute

        int hat = btn & 0x0F;
        s.Up = hat == 7 || hat == 0 || hat == 1;
        s.Right = hat == 1 || hat == 2 || hat == 3;
        s.Down = hat == 3 || hat == 4 || hat == 5;
        s.Left = hat == 5 || hat == 6 || hat == 7;
        s.Square = (btn & 0x10) != 0; s.Cross = (btn & 0x20) != 0;
        s.Circle = (btn & 0x40) != 0; s.Triangle = (btn & 0x80) != 0;
        s.L1 = (sh & 0x01) != 0; s.R1 = (sh & 0x02) != 0;
        s.Select = (sh & 0x10) != 0;   // Create -> Back
        s.Start = (sh & 0x20) != 0;    // Options -> Start
        s.L3 = (sh & 0x40) != 0; s.R3 = (sh & 0x80) != 0;
        s.Ps = (ps & 0x01) != 0;
        return true;
    }

    public void WriteRumble(byte large, byte small)
    {
        // DualSense USB output report 0x02 (basic "compatible" rumble only)
        var r = new byte[48];
        r[0] = 0x02;   // report id
        r[1] = 0x03;   // validFlag0: enable rumble emulation
        r[3] = small;  // right / weak motor
        r[4] = large;  // left / strong motor
        try { _stream.Write(r); } catch { }
    }

    // ----------------------------------------------------------------------------------
    // EXPERIMENTAL / UNTESTED — DualSense adaptive-trigger effects.
    // The output-report layout and effect encodings below are best-effort and NOT verified
    // on real hardware. Byte offsets / modes very likely need tuning. Do not rely on this.
    // ----------------------------------------------------------------------------------

    /// <summary>Effect params (mode byte + up to 9 params). EXPERIMENTAL.</summary>
    public static readonly byte[] TriggerOff = { 0x00, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    public static readonly byte[] TriggerRigid = { 0x01, 0x00, 0xFF, 0, 0, 0, 0, 0, 0, 0 };   // continuous resistance
    public static readonly byte[] TriggerWeapon = { 0x02, 0x90, 0xB0, 0xFF, 0, 0, 0, 0, 0, 0 }; // section ("weapon")

    /// <summary>
    /// EXPERIMENTAL: write rumble + left/right adaptive-trigger effects in one DualSense output
    /// report (0x02). Offsets/flags are unverified — needs testing on a real DS5.
    /// </summary>
    public void WriteAdvanced(byte largeRumble, byte smallRumble, byte[]? rightTrigger, byte[]? leftTrigger)
    {
        var r = new byte[48];
        r[0] = 0x02;   // USB output report id
        r[1] = 0x0F;   // validFlag0: enable rumble + both trigger effects (best-effort)
        r[2] = 0x00;   // validFlag1
        r[3] = smallRumble;   // right / weak
        r[4] = largeRumble;   // left / strong
        if (rightTrigger != null) for (int i = 0; i < 10 && i < rightTrigger.Length; i++) r[11 + i] = rightTrigger[i];
        if (leftTrigger != null) for (int i = 0; i < 10 && i < leftTrigger.Length; i++) r[22 + i] = leftTrigger[i];
        try { _stream.Write(r); } catch { }
    }

    public void Dispose()
    {
        try { WriteRumble(0, 0); } catch { }
        _stream?.Dispose();
    }
}
