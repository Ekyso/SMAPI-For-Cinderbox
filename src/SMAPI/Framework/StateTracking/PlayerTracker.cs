using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using StardewModdingAPI.Enums;
using StardewModdingAPI.Framework.StateTracking.Comparers;
using StardewModdingAPI.Framework.StateTracking.FieldWatchers;
using StardewValley;

namespace StardewModdingAPI.Framework.StateTracking;

/// <summary>Tracks changes to a player's data.</summary>
internal class PlayerTracker : IDisposable
{
    /*********
    ** Fields
    *********/
    /// <summary>The player's inventory as of the last reset.</summary>
    private readonly Dictionary<Item, int> PreviousInventory;

    /// <summary>The player's inventory change as of the last update.</summary>
    private readonly Dictionary<Item, int> CurrentInventory = [];

    /// <summary>A pooled set used to track the added items when detecting changes.</summary>
    private readonly HashSet<Item> PooledAdded;

    /// <summary>A pooled set used to track the removed items when detecting changes.</summary>
    private readonly HashSet<Item> PooledRemoved;

    /// <summary>The player's last valid location.</summary>
    private GameLocation? LastValidLocation;

    /// <summary>The underlying watchers.</summary>
    private readonly List<IWatcher> Watchers = [];

    /*********
        ** Accessors
        *********/
    /// <summary>The player being tracked.</summary>
    public Farmer Player { get; }

    /// <summary>The player's current location.</summary>
    public IValueWatcher<GameLocation?> LocationWatcher { get; }

    /// <summary>Tracks changes to the player's skill levels.</summary>
    public IDictionary<SkillType, IValueWatcher<int>> SkillWatchers { get; }

    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="player">The player to track.</param>
    public PlayerTracker(Farmer player)
    {
        // init player data
        this.Player = player;
        this.SaveInventoryTo(this.CurrentInventory);
        this.PreviousInventory = new Dictionary<Item, int>(this.CurrentInventory);

        // init trackers
        this.LocationWatcher = WatcherFactory.ForReference(
            $"player.{nameof(player.currentLocation)}",
            this.GetCurrentLocation
        );
        this.SkillWatchers = new Dictionary<SkillType, IValueWatcher<int>>
        {
            [SkillType.Combat] = WatcherFactory.ForNetValue(
                $"player.{nameof(player.combatLevel)}",
                player.combatLevel
            ),
            [SkillType.Farming] = WatcherFactory.ForNetValue(
                $"player.{nameof(player.farmingLevel)}",
                player.farmingLevel
            ),
            [SkillType.Fishing] = WatcherFactory.ForNetValue(
                $"player.{nameof(player.fishingLevel)}",
                player.fishingLevel
            ),
            [SkillType.Foraging] = WatcherFactory.ForNetValue(
                $"player.{nameof(player.foragingLevel)}",
                player.foragingLevel
            ),
            [SkillType.Luck] = WatcherFactory.ForNetValue(
                $"player.{nameof(player.luckLevel)}",
                player.luckLevel
            ),
            [SkillType.Mining] = WatcherFactory.ForNetValue(
                $"player.{nameof(player.miningLevel)}",
                player.miningLevel
            ),
        };

        // track watchers for convenience
        this.Watchers.Add(this.LocationWatcher);
        this.Watchers.AddRange(this.SkillWatchers.Values);

        // init pooled sets
        var comparer = new ObjectReferenceComparer<Item>();
        this.PooledAdded = new HashSet<Item>(comparer);
        this.PooledRemoved = new HashSet<Item>(comparer);
    }

    /// <summary>Update the current values if needed.</summary>
    public void Update()
    {
        // update valid location
        this.LastValidLocation = this.GetCurrentLocation();

        // update watchers
        foreach (IWatcher watcher in this.Watchers)
            watcher.Update();

        // update inventory
        this.CurrentInventory.Clear();
        this.SaveInventoryTo(this.CurrentInventory);
    }

    /// <summary>Reset all trackers so their current values are the baseline.</summary>
    public void Reset()
    {
        // reset watchers
        foreach (IWatcher watcher in this.Watchers)
            watcher.Reset();

        // reset PreviousInventory to match current
        this.PreviousInventory.Clear();
        foreach ((Item item, int stack) in this.CurrentInventory)
            this.PreviousInventory.Add(item, stack);
    }

    /// <summary>Get the player's current location, ignoring temporary null values.</summary>
    /// <remarks>The game will set <see cref="Character.currentLocation"/> to null in some cases, e.g. when they're a secondary player in multiplayer and transition to a location that hasn't been synced yet. While that's happening, this returns the player's last valid location instead.</remarks>
    public GameLocation? GetCurrentLocation()
    {
        return this.Player.currentLocation ?? this.LastValidLocation;
    }

    /// <summary>Get the inventory changes since the last update, if anything changed.</summary>
    /// <param name="changes">The inventory changes, or <c>null</c> if nothing changed.</param>
    /// <returns>Returns whether anything changed.</returns>
    public bool TryGetInventoryChanges([NotNullWhen(true)] out SnapshotItemListDiff? changes)
    {
        this.PooledAdded.Clear();
        this.PooledRemoved.Clear();

        foreach (Item item in this.PreviousInventory.Keys)
        {
            if (!this.CurrentInventory.ContainsKey(item))
                this.PooledRemoved.Add(item);
        }

        foreach (Item item in this.CurrentInventory.Keys)
        {
            if (!this.PreviousInventory.ContainsKey(item))
                this.PooledAdded.Add(item);
        }

        return SnapshotItemListDiff.TryGetChanges(
            added: this.PooledAdded,
            removed: this.PooledRemoved,
            stackSizes: this.PreviousInventory,
            out changes
        );
    }

    /// <summary>Release watchers and resources.</summary>
    public void Dispose()
    {
        this.PreviousInventory.Clear();
        this.CurrentInventory.Clear();

        foreach (IWatcher watcher in this.Watchers)
            watcher.Dispose();
    }

    /*********
    ** Private methods
    *********/
    /// <summary>Get the player's current inventory.</summary>
    /// <param name="inventory">The dictionary to fill with the player's inventory.</param>
    private void SaveInventoryTo(Dictionary<Item, int> inventory)
    {
        foreach (Item? item in this.Player.Items)
        {
            if (item is not null)
                inventory.TryAdd(item, item.Stack);
        }
    }
}
