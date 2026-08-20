using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using StardewModdingAPI.Framework.StateTracking;
using StardewModdingAPI.Framework.StateTracking.FieldWatchers;
using StardewValley;
using StardewValley.Locations;

namespace SMAPI.Tests.Framework.StateTracking.FieldWatchers;

[TestFixture]
internal class WatcherFactoryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ForReferenceCollection_TracksItemsAddedAfterConstruction(bool observable)
    {
        ICollection<object> values = observable ? new ObservableCollection<object>() : [];
        var watcher = WatcherFactory.ForReferenceCollection(
            "test collection",
            values
        );
        var added = new object();

        watcher.Update();
        watcher.Reset();
        values.Add(added);
        watcher.Update();

        watcher.IsChanged.Should().BeTrue();
        watcher.Added.Should().ContainSingle().Which.Should().BeSameAs(added);
    }

    [Test]
    public void WorldLocationsTracker_AttachesToLocationsAddedToLiveList()
    {
        List<GameLocation> locations = [];
        var tracker = new WorldLocationsTracker(
            locations,
            new List<MineShaft>(),
            new List<VolcanoDungeon>()
        );
        var added = new GameLocation();

        tracker.Update();
        tracker.Reset();
        locations.Add(added);
        tracker.Update();

        tracker.HasLocationTracker(added).Should().BeTrue();

        tracker.Reset();
        added.netObjects.Add(new Vector2(1, 1), new StardewValley.Object());
        tracker.Update();

        tracker.Locations.Should().ContainSingle()
            .Which.ObjectsWatcher.IsChanged.Should().BeTrue();
    }
}
