using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="ItemGrabMenu"/> constructors that differ between desktop and mobile.
/// Desktop has 18-param and copy constructors; mobile has 25-param constructor with different parameter layout.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class ItemGrabMenuMobileFacade : ItemGrabMenu, IRewriteFacade
{
    /*********
    ** Fields
    *********/
    // Cache the mobile 25-param constructor (resolved at runtime since we compile against desktop)
    private static readonly ConstructorInfo? MobileLongCtor = FindMobileLongConstructor();

    // Private fields on mobile that the copy constructor needs to read
    private static readonly FieldInfo? BehaviorFunctionField = typeof(ItemGrabMenu).GetField(
        "behaviorFunction",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );

    private static readonly FieldInfo? SourceItemField = typeof(ItemGrabMenu).GetField(
        "sourceItem",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );

    private static readonly FieldInfo? MessageField = typeof(ItemGrabMenu).GetField(
        "message",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );

    private static readonly FieldInfo? EssentialField = typeof(ItemGrabMenu).GetField(
        "essential",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );

    /*********
    ** Public methods
    *********/
    /// <summary>Desktop 18-param constructor mapped to mobile's 25-param constructor via reflection.</summary>
    public static ItemGrabMenu Constructor(
        IList<Item> inventory,
        bool reverseGrab,
        bool showReceivingMenu,
        InventoryMenu.highlightThisItem? highlightFunction,
        behaviorOnItemSelect? behaviorOnItemSelectFunction,
        string? message,
        behaviorOnItemSelect? behaviorOnItemGrab = null,
        bool snapToBottom = false,
        bool canBeExitedWithKey = false,
        bool playRightClickSound = true,
        bool allowRightClick = true,
        bool showOrganizeButton = false,
        int source = 0,
        Item? sourceItem = null,
        int whichSpecialButton = -1,
        object? context = null,
        ItemExitBehavior heldItemExitBehavior = ItemExitBehavior.ReturnToPlayer,
        bool allowExitWithHeldItem = false
    )
    {
        ItemGrabMenu menu;

        if (MobileLongCtor != null)
        {
            // Call mobile's 25-param constructor:
            // Params 1-15: direct mapping
            // Param 16: specialObject = null (mobile-only, unused)
            // Params 17-23: mobile-only defaults
            // Param 24: context (moved from desktop param 16)
            // Param 25: allowExitWithHeldItem (moved from desktop param 18)
            var args = new object?[]
            {
                inventory,
                reverseGrab,
                showReceivingMenu,
                highlightFunction,
                behaviorOnItemSelectFunction,
                message,
                behaviorOnItemGrab,
                snapToBottom,
                canBeExitedWithKey,
                playRightClickSound,
                allowRightClick,
                showOrganizeButton,
                source,
                sourceItem,
                whichSpecialButton,
                null, // specialObject
                -1, // storageCapacity
                3, // numRows
                null, // itemChangeBehavior
                true, // allowStack
                null, // behaviorOnAddtoTop
                false, // rearrangeGrangeOnExit
                null, // behaviorOnTapClose
                context,
                allowExitWithHeldItem,
            };
            menu = (ItemGrabMenu)MobileLongCtor.Invoke(args);
        }
        else
        {
            // Fallback: use the 2-param constructor
            menu = new ItemGrabMenu(inventory, context);
        }

        // Store HeldItemExitBehavior in ConditionalWeakTable (same as MenuWithInventory facade)
        var extra = MenuWithInventoryMobileFacade.GetOrCreate(menu);
        extra.HeldItemExitBehavior = heldItemExitBehavior;
        extra.AllowExitWithHeldItem = allowExitWithHeldItem;

        return menu;
    }

    /// <summary>Desktop copy constructor - doesn't exist on mobile.</summary>
    public static ItemGrabMenu Constructor(ItemGrabMenu source)
    {
        var behaviorFunc = BehaviorFunctionField?.GetValue(source) as behaviorOnItemSelect;
        var sourceItem = SourceItemField?.GetValue(source) as Item;
        var message = MessageField?.GetValue(source) as string;

        // Read HeldItemExitBehavior/AllowExitWithHeldItem from ConditionalWeakTable
        var sourceExtra = MenuWithInventoryMobileFacade.GetOrCreate(source);

        var menu = Constructor(
            source.ItemsToGrabMenu.actualInventory,
            source.reverseGrab,
            source.showReceivingMenu,
            source.inventory.highlightMethod,
            behaviorFunc,
            message,
            source.behaviorOnItemGrab,
            snapToBottom: false,
            source.canExitOnKey,
            source.playRightClickSound,
            source.allowRightClick,
            source.organizeButton != null,
            source.source,
            sourceItem,
            source.whichSpecialButton,
            source.context,
            sourceExtra.HeldItemExitBehavior,
            sourceExtra.AllowExitWithHeldItem
        );

        // Copy remaining state
        var essentialVal = EssentialField?.GetValue(source);
        if (essentialVal is bool ess)
            menu.setEssential(ess);

        if (source.currentlySnappedComponent != null)
        {
            menu.setCurrentlySnappedComponentTo(source.currentlySnappedComponent.myID);
            if (Game1.options.SnappyMenus)
                menu.snapCursorToCurrentSnappedComponent();
        }

        MenuWithInventoryMobileFacade.SetHeldItem(
            menu,
            MenuWithInventoryMobileFacade.GetHeldItem(source)
        );

        return menu;
    }

    /*********
    ** Private methods
    *********/
    private ItemGrabMenuMobileFacade()
        : base(null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }

    private static ConstructorInfo? FindMobileLongConstructor()
    {
        // Find the constructor with the most parameters (mobile's 25-param version)
        return typeof(ItemGrabMenu)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault(c => c.GetParameters().Length > 18);
    }
}
