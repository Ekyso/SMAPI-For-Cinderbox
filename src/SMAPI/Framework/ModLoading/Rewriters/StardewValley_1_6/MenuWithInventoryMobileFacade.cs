using System;
using System.Reflection;
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
    private static readonly ConstructorInfo? DesktopConstructor = typeof(MenuWithInventory).GetConstructor(
        [
            typeof(InventoryMenu.highlightThisItem),
            typeof(bool),
            typeof(bool),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(ItemExitBehavior),
            typeof(bool),
        ]
    );

    private static readonly ConstructorInfo? MobileConstructor = typeof(MenuWithInventory).GetConstructor(
        [
            typeof(InventoryMenu.highlightThisItem),
            typeof(bool),
            typeof(bool),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
        ]
    );

    private static readonly FieldInfo? HeldItemField = typeof(MenuWithInventory).GetField(
        "heldItem",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );

    private static readonly PropertyInfo? HeldItemProperty = typeof(MenuWithInventory).GetProperty(
        "heldItem",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );

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
        get => GetHeldItem((MenuWithInventory)(object)this);
        set => SetHeldItem((MenuWithInventory)(object)this, value);
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
    ** Constructor
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
        MenuWithInventory menu;
        if (DesktopConstructor != null)
        {
            menu = (MenuWithInventory)
                DesktopConstructor.Invoke(
                    [
                        highlighterMethod,
                        okButton,
                        trashCan,
                        inventoryXOffset,
                        inventoryYOffset,
                        menuOffsetHack,
                        heldItemExitBehavior,
                        allowExitWithHeldItem,
                    ]
                );
        }
        else if (MobileConstructor != null)
        {
            menu = (MenuWithInventory)
                MobileConstructor.Invoke(
                    [highlighterMethod, okButton, trashCan, 0, 0, 1280, 720]
                );
        }
        else
        {
            throw new MissingMethodException(
                typeof(MenuWithInventory).FullName,
                ".ctor with desktop or mobile parameters"
            );
        }

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
        Item? heldItem = GetHeldItem(menu);
        if (heldItem == null)
            return;

        var extra = GetOrCreate(menu);
        switch (extra.HeldItemExitBehavior)
        {
            case ItemExitBehavior.ReturnToPlayer:
                heldItem = Game1.player.addItemToInventory(heldItem);
                break;
            case ItemExitBehavior.ReturnToMenu:
                heldItem = InventoryMenuMobileFacade.TryToAddItem(menu.inventory, heldItem);
                break;
            case ItemExitBehavior.Discard:
                heldItem = null;
                break;
        }

        SetHeldItem(menu, heldItem);
        if (heldItem != null)
        {
            Game1.playSound("throwDownITem");
            Game1.createItemDebris(
                heldItem,
                Game1.player.getStandingPosition(),
                Game1.player.FacingDirection
            );
            menu.inventory.onAddItem?.Invoke(heldItem, Game1.player);
            SetHeldItem(menu, null);
        }
    }

    internal static Item? GetHeldItem(MenuWithInventory menu)
    {
        if (HeldItemField != null)
            return HeldItemField.GetValue(menu) as Item;
        if (HeldItemProperty != null)
            return HeldItemProperty.GetValue(menu) as Item;

        throw new MissingMemberException(typeof(MenuWithInventory).FullName, "heldItem");
    }

    internal static void SetHeldItem(MenuWithInventory menu, Item? value)
    {
        if (HeldItemField != null)
        {
            value?.onDetachedFromParent();
            HeldItemField.SetValue(menu, value);
            return;
        }
        if (HeldItemProperty != null)
        {
            HeldItemProperty.SetValue(menu, value);
            return;
        }

        throw new MissingMemberException(typeof(MenuWithInventory).FullName, "heldItem");
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
