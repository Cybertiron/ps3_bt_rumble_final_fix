namespace Ds3ViGEmBridge;

/// <summary>Normalized controller state, mapped straight onto a virtual Xbox 360 pad.</summary>
public sealed class PadState
{
    public bool Cross, Circle, Square, Triangle;
    public bool L1, R1, L3, R3, Start, Select, Ps;
    public bool Up, Down, Left, Right;
    public byte LX = 128, LY = 128, RX = 128, RY = 128;   // 0..255, 128 = center
    public byte L2Analog, R2Analog;                        // 0..255
}

/// <summary>A physical controller we can read input from and send rumble to.</summary>
public interface IGamepad : IDisposable
{
    string Info { get; }
    bool ReadState(out PadState state, int timeoutMs = 50);
    void WriteRumble(byte large, byte small);
}
