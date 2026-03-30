using System.Runtime.CompilerServices;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="MenuWithInventory"/> members missing on mobile.
/// Desktop has heldItem as property with onDetachedFromParent, HeldItemExitBehavior,
/// AllowExitWithHeldItem, hoverAmount, and different constructor params.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class MenuWithInventoryMobileFacade : MenuWithInventory, IRewriteFacade
{
    /*********
    ** Per-instance storage for missing fields
    *********/
    internal class ExtraFields
    {
        public bool AllowExitWithHeldItem;
        public ItemExitBehavior HeldItemExitBehavior = ItemExitBehavior.ReturnToPlayer;
        public int HoverAmount;
    }

    internal static readonly ConditionalWeakTable<MenuWithInventory, ExtraFields> Storage = new();

    internal static ExtraFields GetOrCreate(MenuWithInventory menu)
    {
        return Storage.GetOrCreateValue(menu);
    }

    /*********
    ** heldItem - desktop is property with onDetachedFromParent(), mobile is plain field
    *********/
    public new Item? heldItem
    {
        get => base.heldItem;
        set
        {
            value?.onDetachedFromParent();
            base.heldItem = value;
        }
    }

    /*********
    ** Missing fields - backed by ConditionalWeakTable
    *********/
    public new bool AllowExitWithHeldItem
    {
        get => GetOrCreate((MenuWithInventory)(object)this).AllowExitWithHeldItem;
        set => GetOrCreate((MenuWithInventory)(object)this).AllowExitWithHeldItem = value;
    }

    public new ItemExitBehavior HeldItemExitBehavior
    {
        get => GetOrCreate((MenuWithInventory)(object)this).HeldItemExitBehavior;
        set => GetOrCreate((MenuWithInventory)(object)this).HeldItemExitBehavior = value;
    }

    public new int hoverAmount
    {
        get => GetOrCreate((MenuWithInventory)(object)this).HoverAmount;
        set => GetOrCreate((MenuWithInventory)(object)this).HoverAmount = value;
    }

    /*********
    ** Constructor - desktop 8-param to mobile 3-param
    *********/
    public static MenuWithInventory Constructor(
        InventoryMenu.highlightThisItem? highlighterMethod = null,
        bool okButton = false,
        bool trashCan = false,
        int inventoryXOffset = 0,
        int inventoryYOffset = 0,
        int menuOffsetHack = 0,
        ItemExitBehavior heldItemExitBehavior = ItemExitBehavior.ReturnToPlayer,
        bool allowExitWithHeldItem = false
    )
    {
        var menu = new MenuWithInventory(highlighterMethod, okButton, trashCan);
        var extra = GetOrCreate(menu);
        extra.HeldItemExitBehavior = heldItemExitBehavior;
        extra.AllowExitWithHeldItem = allowExitWithHeldItem;
        return menu;
    }

    /*********
    ** Rescue logic - called by Harmony patches on cleanupBeforeExit/emergencyShutDown
    *********/
    internal static void RescueHeldItemOnExit(MenuWithInventory menu)
    {
        if (menu.heldItem == null)
            return;

        var extra = GetOrCreate(menu);
        switch (extra.HeldItemExitBehavior)
        {
            case ItemExitBehavior.ReturnToPlayer:
                menu.heldItem = Game1.player.addItemToInventory(menu.heldItem);
                break;
            case ItemExitBehavior.ReturnToMenu:
                menu.heldItem = menu.inventory.tryToAddItem(menu.heldItem);
                break;
            case ItemExitBehavior.Discard:
                menu.heldItem = null;
                break;
        }

        if (menu.heldItem != null)
        {
            Game1.playSound("throwDownITem");
            Game1.createItemDebris(
                menu.heldItem,
                Game1.player.getStandingPosition(),
                Game1.player.FacingDirection
            );
            menu.inventory.onAddItem?.Invoke(menu.heldItem, Game1.player);
            menu.heldItem = null;
        }
    }

    /*********
    ** Private
    *********/
    private MenuWithInventoryMobileFacade()
        : base(null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
