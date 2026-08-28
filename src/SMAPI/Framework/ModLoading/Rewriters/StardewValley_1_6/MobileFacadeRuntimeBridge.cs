using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6.Internal;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Exposes the runtime semantics needed by Cinderbox's mobile facade patches.</summary>
internal sealed class MobileFacadeRuntimeBridge : IMobileFacadeRuntime
{
    internal static MobileFacadeRuntimeBridge Instance { get; } = new();

    private static readonly FieldInfo? ShopCurrentTabField = typeof(ShopMenu).GetField(
        "currentTab",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );

    private static readonly FieldInfo? ShopTabButtonsField = typeof(ShopMenu).GetField(
        "tabButtons",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );

    private static readonly MethodInfo? ShopSetScrollBarMethod = typeof(ShopMenu).GetMethod(
        "setScrollBarToCurrentIndex",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );

    private MobileFacadeRuntimeBridge() { }

    public bool ShouldAllowExitWithHeldItem(MenuWithInventory menu)
    {
        return MenuWithInventoryMobileFacade.Storage.TryGetValue(menu, out var extra)
            && extra.AllowExitWithHeldItem;
    }

    public void RescueHeldItemOnExit(MenuWithInventory menu)
    {
        MenuWithInventoryMobileFacade.RescueHeldItemOnExit(menu);
    }

    public void UpdateHoverAmount(MenuWithInventory menu, int x, int y)
    {
        var extra = MenuWithInventoryMobileFacade.GetOrCreate(menu);
        extra.HoverAmount = 0;
        Item? heldItem = MenuWithInventoryMobileFacade.GetHeldItem(menu);

        if (menu.trashCan?.containsPoint(x, y) == true && heldItem != null)
        {
            int price = Utility.getTrashReclamationPrice(heldItem, Game1.player);
            if (price > 0)
                extra.HoverAmount = price;
        }
    }

    /// <summary>Apply a rewritten desktop-style shop tab, if the active tab uses SMAPI's facade type.</summary>
    /// <returns>Whether the original mobile <c>ShopMenu.applyTab</c> should run.</returns>
    public bool ShouldRunOriginalShopApplyTab(ShopMenu menu)
    {
        if (
            ShopCurrentTabField?.GetValue(menu) is not int currentTab
            || ShopTabButtonsField?.GetValue(menu) is not IList tabButtons
        )
        {
            return true;
        }

        if (currentTab < 0 || currentTab >= tabButtons.Count)
        {
            menu.forSale = menu.itemPriceAndStock.Keys.ToList();
            return false;
        }

        if (tabButtons[currentTab] is not ShopTabClickableTextureComponent tabButton)
            return true;

        Func<ISalable, bool> filter = tabButton.Filter ?? (_ => true);
        menu.forSale.Clear();
        foreach (ISalable item in menu.itemPriceAndStock.Keys)
        {
            if (filter(item))
                menu.forSale.Add(item);
        }

        menu.currentItemIndex = 0;
        ShopSetScrollBarMethod?.Invoke(menu, null);
        return false;
    }
}
