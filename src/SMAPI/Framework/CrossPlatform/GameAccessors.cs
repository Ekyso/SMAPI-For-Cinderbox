using System;
using System.Collections.Generic;
using System.Reflection;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.CrossPlatform;

/// <summary>
/// Provides cross-platform access to Game1 members whose signatures differ
/// between mobile and desktop Stardew Valley. Uses cached delegates with
/// return-type covariance for near-zero overhead after first call.
/// </summary>
internal static class GameAccessors
{
    private static Func<IEnumerable<GameLocation>>? _getLocations;
    private static Func<IList<IClickableMenu>>? _getOnScreenMenus;

    /// <summary>Get Game1.locations as IEnumerable, regardless of whether the runtime returns List or ObservableCollection.</summary>
    public static IEnumerable<GameLocation> GetLocations()
    {
        _getLocations ??= BuildStaticPropertyGetter<IEnumerable<GameLocation>>(
            typeof(Game1),
            "locations"
        );
        return _getLocations();
    }

    /// <summary>Get Game1.onScreenMenus as IList, regardless of whether the runtime returns List or IList.</summary>
    public static IList<IClickableMenu> GetOnScreenMenus()
    {
        _getOnScreenMenus ??= BuildStaticFieldGetter<IList<IClickableMenu>>(
            typeof(Game1),
            "onScreenMenus"
        );
        return _getOnScreenMenus();
    }

    private static Func<T> BuildStaticFieldGetter<T>(Type type, string fieldName)
    {
        var field =
            type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.Name}.{fieldName} field not found");

        return () => (T)field.GetValue(null)!;
    }

    private static Func<T> BuildStaticPropertyGetter<T>(Type type, string propertyName)
    {
        var prop =
            type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"{type.Name}.{propertyName} property not found"
            );
        var getter =
            prop.GetGetMethod()
            ?? throw new InvalidOperationException($"{type.Name}.{propertyName} has no getter");

        // Delegate.CreateDelegate supports return-type covariance:
        // A method returning List<T> can be wrapped as Func<IEnumerable<T>>
        return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), getter);
    }
}
