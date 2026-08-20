using StardewValley.Menus;

namespace StardewModdingAPI.Framework;

/// <summary>Provides the narrow runtime contract needed by Cinderbox's mobile facade hooks.</summary>
/// <remarks>This is a host integration contract. Mods should not reference it.</remarks>
public interface IMobileFacadeRuntime
{
    /// <summary>Get whether a menu may close while its facade-owned held item is populated.</summary>
    bool ShouldAllowExitWithHeldItem(MenuWithInventory menu);

    /// <summary>Return a facade-owned held item to the player before a menu closes.</summary>
    void RescueHeldItemOnExit(MenuWithInventory menu);

    /// <summary>Update the facade-owned trash-can hover amount.</summary>
    void UpdateHoverAmount(MenuWithInventory menu, int x, int y);

    /// <summary>Apply a facade-owned shop tab when present.</summary>
    /// <returns>Whether the original mobile shop-tab method should run.</returns>
    bool ShouldRunOriginalShopApplyTab(ShopMenu menu);
}
