namespace StardewModdingAPI.Framework;

/// <summary>Determines when Android can expose the initial mod lifecycle events.</summary>
internal static class AndroidLifecycleEventGate
{
    /// <summary>Whether the active game finished the initial content load required by public mod lifecycle events.</summary>
    /// <param name="targetPlatform">The launcher-selected game platform.</param>
    /// <param name="finishedIncrementalLoad">The mobile game's incremental-load completion flag, if available.</param>
    /// <param name="hasPlayerTeam">Whether the game has created its temporary player and team.</param>
    public static bool IsInitialContentLoaded(
        GamePlatform targetPlatform,
        bool? finishedIncrementalLoad,
        bool hasPlayerTeam
    )
    {
        if (targetPlatform != GamePlatform.Android)
            return true;

        return finishedIncrementalLoad ?? hasPlayerTeam;
    }

    /// <summary>Whether the game-launched event can be raised.</summary>
    /// <param name="isGameLaunched">Whether the event has already been raised.</param>
    /// <param name="areAllModsInitialized">Whether all mod entry points have completed.</param>
    /// <param name="isInitialContentLoaded">Whether the base game finished its initial content load.</param>
    public static bool CanRaiseGameLaunched(
        bool isGameLaunched,
        bool areAllModsInitialized,
        bool isInitialContentLoaded
    )
    {
        return !isGameLaunched && areAllModsInitialized && isInitialContentLoaded;
    }

    /// <summary>Whether public update events can be raised for the current tick.</summary>
    /// <param name="isGameLaunched">Whether the game-launched event has already been raised.</param>
    public static bool CanRaisePublicUpdateEvents(bool isGameLaunched)
    {
        return isGameLaunched;
    }

    /// <summary>Get the tick number exposed through public lifecycle events.</summary>
    /// <param name="ticksElapsed">The internal game tick number.</param>
    /// <param name="firstPublicTick">The internal tick on which public lifecycle events began.</param>
    public static uint GetPublicTicks(uint ticksElapsed, uint firstPublicTick)
    {
        return ticksElapsed - firstPublicTick;
    }
}
