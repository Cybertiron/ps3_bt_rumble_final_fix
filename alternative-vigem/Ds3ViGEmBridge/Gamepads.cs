namespace Ds3ViGEmBridge;

/// <summary>Discovers and opens every supported controller (DS3 + DS4) as raw USB HID.</summary>
public static class Gamepads
{
    public static List<IGamepad> OpenAll(out string message)
    {
        var all = new List<IGamepad>();

        var ds3 = Ds3Device.OpenAll(out var ds3Msg);
        all.AddRange(ds3);

        var ds4 = Ds4Device.OpenAll(out int ds4Busy);
        all.AddRange(ds4);

        if (all.Count == 0)
        {
            message = "No controllers opened. " + ds3Msg +
                      (ds4Busy > 0 ? $" ({ds4Busy} DS4 busy.)" : "");
        }
        else
        {
            message = $"opened {ds3.Count} DS3 + {ds4.Count} DS4"
                      + (ds4Busy > 0 ? $" ({ds4Busy} DS4 busy)" : "");
        }
        return all;
    }
}
