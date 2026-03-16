using StardewModdingAPI.Events;

namespace StardewModdingAPI.Framework.Events;

/// <summary>Provides pooled EventArgs instances to reduce GC pressure.</summary>
/// <remarks>Instances are safely reusable because tick properties read from SCore.TicksElapsed rather than storing state.</remarks>
internal static class EventArgsPool
{
    /// <summary>Pool for UpdateTickingEventArgs instances.</summary>
    public static readonly ObjectPool<UpdateTickingEventArgs> UpdateTicking = new(
        factory: () => new UpdateTickingEventArgs(),
        reset: null,
        maxSize: 4
    );

    /// <summary>Pool for UpdateTickedEventArgs instances.</summary>
    public static readonly ObjectPool<UpdateTickedEventArgs> UpdateTicked = new(
        factory: () => new UpdateTickedEventArgs(),
        reset: null,
        maxSize: 4
    );

    /// <summary>Pool for OneSecondUpdateTickingEventArgs instances.</summary>
    public static readonly ObjectPool<OneSecondUpdateTickingEventArgs> OneSecondUpdateTicking = new(
        factory: () => new OneSecondUpdateTickingEventArgs(),
        reset: null,
        maxSize: 2
    );

    /// <summary>Pool for OneSecondUpdateTickedEventArgs instances.</summary>
    public static readonly ObjectPool<OneSecondUpdateTickedEventArgs> OneSecondUpdateTicked = new(
        factory: () => new OneSecondUpdateTickedEventArgs(),
        reset: null,
        maxSize: 2
    );
}
