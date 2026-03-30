#if SMAPI_FOR_ANDROID
using System;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>Harmony patches for <see cref="MenuWithInventory"/> to implement desktop behavior
/// (AllowExitWithHeldItem, HeldItemExitBehavior, hoverAmount) on mobile using
/// per-instance storage from <see cref="MenuWithInventoryMobileFacade"/>.</summary>
internal static class MenuWithInventoryMobilePatches
{
    public static void Apply(Harmony harmony)
    {
        var menuType = typeof(MenuWithInventory);

        harmony.Patch(
            original: AccessTools.Method(menuType, nameof(MenuWithInventory.readyToClose)),
            postfix: new HarmonyMethod(
                typeof(MenuWithInventoryMobilePatches),
                nameof(ReadyToClose_Postfix)
            )
        );

        // cleanupBeforeExit and emergencyShutDown are declared on IClickableMenu,
        // not overridden by mobile's MenuWithInventory. Patch the base class with instance type check.
        var baseType = typeof(IClickableMenu);

        harmony.Patch(
            original: AccessTools.Method(baseType, "cleanupBeforeExit"),
            prefix: new HarmonyMethod(
                typeof(MenuWithInventoryMobilePatches),
                nameof(CleanupBeforeExit_Prefix)
            )
        );

        harmony.Patch(
            original: AccessTools.Method(baseType, nameof(IClickableMenu.emergencyShutDown)),
            prefix: new HarmonyMethod(
                typeof(MenuWithInventoryMobilePatches),
                nameof(EmergencyShutDown_Prefix)
            )
        );

        harmony.Patch(
            original: AccessTools.Method(menuType, nameof(MenuWithInventory.performHoverAction)),
            postfix: new HarmonyMethod(
                typeof(MenuWithInventoryMobilePatches),
                nameof(PerformHoverAction_Postfix)
            )
        );
    }

    /// <summary>If AllowExitWithHeldItem is set, override readyToClose to return true.</summary>
    private static void ReadyToClose_Postfix(MenuWithInventory __instance, ref bool __result)
    {
        if (
            !__result
            && MenuWithInventoryMobileFacade.Storage.TryGetValue(__instance, out var extra)
        )
        {
            if (extra.AllowExitWithHeldItem)
                __result = true;
        }
    }

    /// <summary>Rescue held item before exit using HeldItemExitBehavior.</summary>
    private static void CleanupBeforeExit_Prefix(IClickableMenu __instance)
    {
        if (__instance is MenuWithInventory menu)
            MenuWithInventoryMobileFacade.RescueHeldItemOnExit(menu);
    }

    /// <summary>Rescue held item on emergency shutdown.</summary>
    private static void EmergencyShutDown_Prefix(IClickableMenu __instance)
    {
        if (__instance is MenuWithInventory menu)
            MenuWithInventoryMobileFacade.RescueHeldItemOnExit(menu);
    }

    /// <summary>Calculate hoverAmount for trash can sale price display.</summary>
    private static void PerformHoverAction_Postfix(MenuWithInventory __instance, int x, int y)
    {
        var extra = MenuWithInventoryMobileFacade.GetOrCreate(__instance);
        extra.HoverAmount = 0;

        if (
            __instance.trashCan != null
            && __instance.trashCan.containsPoint(x, y)
            && __instance.heldItem != null
        )
        {
            int price = Utility.getTrashReclamationPrice(__instance.heldItem, Game1.player);
            if (price > 0)
                extra.HoverAmount = price;
        }
    }
}
#endif
