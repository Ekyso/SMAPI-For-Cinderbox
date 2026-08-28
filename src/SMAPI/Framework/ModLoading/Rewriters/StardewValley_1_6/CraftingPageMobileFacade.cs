using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="CraftingPage"/> fields that exist on desktop but not mobile.
/// Desktop CraftingPage has an embedded InventoryMenu, heldItem for cursor-held items,
/// and a trashCan button. Mobile crafts directly to player inventory without these.
/// This facade provides real data-backed fields: inventory points to player items,
/// heldItem is facade-local compatibility state, and trashCan is a positioned component.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class CraftingPageMobileFacade : CraftingPage, IRewriteFacade
{
    private static readonly ConstructorInfo? DesktopConstructor = typeof(CraftingPage).GetConstructor(
        [
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(List<IInventory>),
        ]
    );

    private static readonly ConstructorInfo? MobileConstructor = typeof(CraftingPage).GetConstructor(
        [
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(List<IInventory>),
            typeof(int),
        ]
    );

    /*********
    ** Per-instance storage
    *********/
    internal class ExtraFields
    {
        public Item? HeldItem;
        public InventoryMenu? Inventory;
        public ClickableTextureComponent? TrashCan;
        public float TrashCanLidRotation;
    }

    internal static readonly ConditionalWeakTable<CraftingPage, ExtraFields> Storage = new();

    internal static ExtraFields GetOrCreate(CraftingPage page)
    {
        return Storage.GetOrCreateValue(page);
    }

    public static CraftingPage Constructor(
        int x,
        int y,
        int width,
        int height,
        bool cooking = false,
        bool standaloneMenu = false,
        List<IInventory>? materialContainers = null
    )
    {
        if (DesktopConstructor != null)
        {
            return (CraftingPage)
                DesktopConstructor.Invoke(
                    [x, y, width, height, cooking, standaloneMenu, materialContainers]
                );
        }
        if (MobileConstructor != null)
        {
            return (CraftingPage)
                MobileConstructor.Invoke(
                    [x, y, width, height, cooking, standaloneMenu, materialContainers, 300]
                );
        }

        throw new MissingMethodException(
            typeof(CraftingPage).FullName,
            ".ctor with desktop or mobile parameters"
        );
    }

    /*********
    ** Properties - MapFacade rewrites field references to property get/set calls
    *********/
    /// <summary>Desktop heldItem - the item currently held by the cursor.
    /// Mobile crafting places the real item directly into the player's inventory, so this
    /// facade state defaults to <c>null</c> and is only changed when rewritten mod code sets it.</summary>
    public new Item? heldItem
    {
        get => GetOrCreate((CraftingPage)(object)this).HeldItem;
        set => GetOrCreate((CraftingPage)(object)this).HeldItem = value;
    }

    /// <summary>Desktop inventory - an InventoryMenu showing player items below the crafting grid.
    /// On mobile, lazy-initialized to point at the player's actual inventory.</summary>
    public new InventoryMenu? inventory
    {
        get
        {
            var extra = GetOrCreate((CraftingPage)(object)this);
            if (extra.Inventory == null)
            {
                extra.Inventory = InventoryMenuMobileFacade.Create(
                    base.xPositionOnScreen
                        + IClickableMenu.spaceToClearSideBorder
                        + IClickableMenu.borderWidth,
                    base.yPositionOnScreen
                        + IClickableMenu.spaceToClearTopBorder
                        + IClickableMenu.borderWidth
                        + 320
                        - 16,
                    playerInventory: false
                );
                extra.Inventory.showGrayedOutSlots = true;
            }
            return extra.Inventory;
        }
        set => GetOrCreate((CraftingPage)(object)this).Inventory = value;
    }

    /// <summary>Desktop trashCan - a button for trashing held items.
    /// On mobile, lazy-initialized with the correct trash can texture.</summary>
    public new ClickableTextureComponent? trashCan
    {
        get
        {
            var extra = GetOrCreate((CraftingPage)(object)this);
            if (extra.TrashCan == null)
            {
                extra.TrashCan = new ClickableTextureComponent(
                    new Rectangle(
                        base.xPositionOnScreen + base.width + 4,
                        base.yPositionOnScreen
                            + base.height
                            - 192
                            - 32
                            - IClickableMenu.borderWidth
                            - 104,
                        64,
                        104
                    ),
                    Game1.mouseCursors,
                    new Rectangle(564 + Game1.player.trashCanLevel * 18, 102, 18, 26),
                    4f
                )
                {
                    myID = 106,
                };
            }
            return extra.TrashCan;
        }
        set => GetOrCreate((CraftingPage)(object)this).TrashCan = value;
    }

    /// <summary>Desktop trashCanLidRotation - animation state for the trash can lid.</summary>
    public new float trashCanLidRotation
    {
        get => GetOrCreate((CraftingPage)(object)this).TrashCanLidRotation;
        set => GetOrCreate((CraftingPage)(object)this).TrashCanLidRotation = value;
    }

    /*********
    ** Private methods
    *********/
    private CraftingPageMobileFacade()
        : base(0, 0, 0, 0)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
