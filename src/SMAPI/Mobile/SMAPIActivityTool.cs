using System;
using Android.App;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Framework;
using StardewModdingAPI.Internal;

namespace StardewModdingAPI.Mobile;

/// <summary>Tool for interacting with the Android Activity.</summary>
public static class SMAPIActivityTool
{
    /// <summary>The main activity instance. Set by DesktopGameLauncher.</summary>
    public static Activity? MainActivity { get; set; }

    /// <summary>Exit the game cleanly.</summary>
    public static void ExitGame()
    {
        IMonitor? monitor = SCore.Instance?.SMAPIMonitor;
        monitor?.Log("Exiting game via SMAPIActivityTool");
        try
        {
            if (MainActivity == null)
            {
                MainActivity = Game.Activity;
            }

            MainActivity?.Finish();
            monitor?.Log("Game exit completed.");
        }
        catch (Exception ex)
        {
            monitor?.Log(ex.GetLogSummary());
            throw;
        }
    }
}
