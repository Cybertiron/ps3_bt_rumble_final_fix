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

        var ds5 = Ds5Device.OpenAll(out int ds5Busy);
        all.AddRange(ds5);

        int busy = ds4Busy + ds5Busy;
        if (all.Count == 0)
        {
            message = "No controllers opened. " + ds3Msg + (busy > 0 ? $" ({busy} busy.)" : "");
        }
        else
        {
            message = $"opened {ds3.Count} DS3 + {ds4.Count} DS4 + {ds5.Count} DS5"
                      + (busy > 0 ? $" ({busy} busy)" : "");
        }
        return all;
    }
}
