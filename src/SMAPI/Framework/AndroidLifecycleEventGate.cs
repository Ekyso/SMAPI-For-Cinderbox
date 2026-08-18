namespace StardewModdingAPI.Framework;

/// <summary>Determines when Android can expose the initial mod lifecycle events.</summary>
internal static class AndroidLifecycleEventGate
{
    /// <summary>Whether the game-launched event can be raised.</summary>
    /// <param name="isGameLaunched">Whether the event has already been raised.</param>
    /// <param name="areAllModsInitialized">Whether all mod entry points have completed.</param>
    public static bool CanRaiseGameLaunched(bool isGameLaunched, bool areAllModsInitialized)
    {
        return !isGameLaunched && areAllModsInitialized;
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
