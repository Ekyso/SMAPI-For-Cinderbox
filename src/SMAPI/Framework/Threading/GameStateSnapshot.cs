using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewModdingAPI.Framework.Threading;

/// <summary>A thread-safe snapshot of game state for use by worker threads.</summary>
internal sealed class GameStateSnapshot
{
    /// <summary>The current in-game time of day (e.g., 600 = 6:00 AM).</summary>
    public int TimeOfDay { get; private set; }

    /// <summary>The current season name.</summary>
    public string CurrentSeason { get; private set; } = "";

    /// <summary>The current day of the month.</summary>
    public int DayOfMonth { get; private set; }

    /// <summary>The name of the current location.</summary>
    public string CurrentLocationName { get; private set; } = "";

    /// <summary>The game viewport X position.</summary>
    public int ViewportX { get; private set; }

    /// <summary>The game viewport Y position.</summary>
    public int ViewportY { get; private set; }

    /// <summary>The game viewport width.</summary>
    public int ViewportWidth { get; private set; }

    /// <summary>The game viewport height.</summary>
    public int ViewportHeight { get; private set; }

    /// <summary>The player's current money.</summary>
    public int PlayerMoney { get; private set; }

    /// <summary>Whether the world is ready for interaction.</summary>
    public bool IsWorldReady { get; private set; }

    /// <summary>The current year.</summary>
    public int Year { get; private set; }

    /// <summary>Whether it's currently raining.</summary>
    public bool IsRaining { get; private set; }

    /// <summary>Whether the game is paused.</summary>
    public bool IsPaused { get; private set; }

    /// <summary>Capture the current game state. Must be called from the game thread.</summary>
    public void Capture()
    {
        if (!Context.IsWorldReady)
        {
            IsWorldReady = false;
            return;
        }

        IsWorldReady = true;
        TimeOfDay = Game1.timeOfDay;
        CurrentSeason = Game1.currentSeason ?? "";
        DayOfMonth = Game1.dayOfMonth;
        CurrentLocationName = Game1.currentLocation?.Name ?? "";
        ViewportX = Game1.viewport.X;
        ViewportY = Game1.viewport.Y;
        ViewportWidth = Game1.viewport.Width;
        ViewportHeight = Game1.viewport.Height;
        PlayerMoney = Game1.player?.Money ?? 0;
        Year = Game1.year;
        IsRaining = Game1.isRaining;
        IsPaused = Game1.paused;
    }

    /// <summary>Reset the snapshot to default values.</summary>
    public void Reset()
    {
        IsWorldReady = false;
        TimeOfDay = 0;
        CurrentSeason = "";
        DayOfMonth = 0;
        CurrentLocationName = "";
        ViewportX = 0;
        ViewportY = 0;
        ViewportWidth = 0;
        ViewportHeight = 0;
        PlayerMoney = 0;
        Year = 0;
        IsRaining = false;
        IsPaused = false;
    }
}
