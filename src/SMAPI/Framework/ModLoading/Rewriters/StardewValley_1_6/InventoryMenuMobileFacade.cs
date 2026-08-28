using System;
using System.Collections.Generic;
using System.Reflection;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="InventoryMenu"/> members whose signatures differ between platforms.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class InventoryMenuMobileFacade : InventoryMenu, IRewriteFacade
{
    private static readonly ConstructorInfo? DesktopConstructor = typeof(InventoryMenu).GetConstructor(
        [
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(IList<Item>),
            typeof(InventoryMenu.highlightThisItem),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(bool),
        ]
    );

    private static readonly ConstructorInfo? MobileConstructor = typeof(InventoryMenu).GetConstructor(
        [
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(IList<Item>),
            typeof(InventoryMenu.highlightThisItem),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(int),
            typeof(bool),
            typeof(int),
            typeof(int),
            typeof(int),
        ]
    );

    private static readonly MethodInfo? DesktopTryToAddItem = typeof(InventoryMenu).GetMethod(
        nameof(InventoryMenu.tryToAddItem),
        BindingFlags.Public | BindingFlags.Instance,
        null,
        [typeof(Item), typeof(string)],
        null
    );

    private static readonly MethodInfo? MobileTryToAddItem = typeof(InventoryMenu).GetMethod(
        nameof(InventoryMenu.tryToAddItem),
        BindingFlags.Public | BindingFlags.Instance,
        null,
        [typeof(Item), typeof(string), typeof(bool)],
        null
    );

    /*********
    ** Public methods
    *********/
    public static InventoryMenu Constructor(
        int xPosition,
        int yPosition,
        bool playerInventory,
        IList<Item>? actualInventory = null,
        InventoryMenu.highlightThisItem? highlightMethod = null,
        int capacity = -1,
        int rows = 3,
        int horizontalGap = 0,
        int verticalGap = 0,
        bool drawSlots = true
    )
    {
        return Create(
            xPosition,
            yPosition,
            playerInventory,
            actualInventory,
            highlightMethod,
            capacity,
            rows,
            horizontalGap,
            verticalGap,
            drawSlots
        );
    }

    public new void SetPosition(int x, int y)
    {
        base.movePosition(-base.xPositionOnScreen, -base.yPositionOnScreen);
        base.movePosition(x, y);
    }

    internal static InventoryMenu Create(
        int xPosition,
        int yPosition,
        bool playerInventory,
        IList<Item>? actualInventory = null,
        InventoryMenu.highlightThisItem? highlightMethod = null,
        int capacity = -1,
        int rows = 3,
        int horizontalGap = 0,
        int verticalGap = 0,
        bool drawSlots = true
    )
    {
        if (DesktopConstructor != null)
        {
            return (InventoryMenu)
                DesktopConstructor.Invoke(
                    [
                        xPosition,
                        yPosition,
                        playerInventory,
                        actualInventory,
                        highlightMethod,
                        capacity,
                        rows,
                        horizontalGap,
                        verticalGap,
                        drawSlots,
                    ]
                );
        }

        if (MobileConstructor != null)
        {
            return (InventoryMenu)
                MobileConstructor.Invoke(
                    [
                        xPosition,
                        yPosition,
                        playerInventory,
                        actualInventory,
                        highlightMethod,
                        capacity,
                        rows,
                        horizontalGap,
                        verticalGap,
                        drawSlots,
                        0,
                        0,
                        true,
                        true,
                        0,
                        false,
                        -1,
                        -1,
                        -1,
                    ]
                );
        }

        throw new MissingMethodException(
            typeof(InventoryMenu).FullName,
            ".ctor with desktop or mobile parameters"
        );
    }

    internal static Item? TryToAddItem(
        InventoryMenu menu,
        Item? item,
        string sound = "coin"
    )
    {
        if (DesktopTryToAddItem != null)
            return DesktopTryToAddItem.Invoke(menu, [item, sound]) as Item;
        if (MobileTryToAddItem != null)
            return MobileTryToAddItem.Invoke(menu, [item, sound, true]) as Item;

        throw new MissingMethodException(
            typeof(InventoryMenu).FullName,
            nameof(InventoryMenu.tryToAddItem)
        );
    }

    /*********
    ** Private methods
    *********/
    private InventoryMenuMobileFacade()
        : base(0, 0, false)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
