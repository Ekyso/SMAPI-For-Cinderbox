#if SMAPI_FOR_ANDROID
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>Harmony patch on <see cref="ShopMenu.applyTab"/> to support
/// <c>ShopTabClickableTextureComponent.Filter</c> delegates on mobile.
/// When a tab button is our standalone type with a Filter, use the delegate
/// instead of mobile's hardcoded category logic.</summary>
internal static class ShopMenuTabMobilePatches
{
    private static Type? ShopTabType;
    private static FieldInfo? FilterField;

    private static readonly FieldInfo CurrentTabField = typeof(ShopMenu).GetField(
        "currentTab",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    private static readonly MethodInfo? SetScrollBarMethod = typeof(ShopMenu).GetMethod(
        "setScrollBarToCurrentIndex",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
    );

    public static void Apply(Harmony harmony)
    {
        // Resolve our standalone ShopTabClickableTextureComponent type from loaded assemblies
        ShopTabType = AppDomain
            .CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            })
            .FirstOrDefault(t => t.Name == "ShopTabClickableTextureComponent");

        if (ShopTabType != null)
            FilterField = ShopTabType.GetField("Filter");

        harmony.Patch(
            original: AccessTools.Method(typeof(ShopMenu), "applyTab"),
            prefix: new HarmonyMethod(typeof(ShopMenuTabMobilePatches), nameof(ApplyTab_Prefix))
        );
    }

    /// <summary>If the current tab is a ShopTabClickableTextureComponent with a Filter delegate,
    /// use it for filtering instead of mobile's hardcoded logic.</summary>
    private static bool ApplyTab_Prefix(ShopMenu __instance)
    {
        if (ShopTabType == null || FilterField == null)
            return true;

        int currentTab = (int)(CurrentTabField.GetValue(__instance) ?? 0);

        if (currentTab < 0 || currentTab >= __instance.tabButtons.Count)
        {
            __instance.forSale = __instance.itemPriceAndStock.Keys.ToList();
            return false;
        }

        var tabButton = __instance.tabButtons[currentTab];

        // Only intercept if this is our type with Filter support
        if (!ShopTabType.IsInstanceOfType(tabButton))
            return true; // fall through to mobile's hardcoded logic

        var filter = FilterField.GetValue(tabButton) as Func<ISalable, bool>;
        filter ??= _ => true;

        __instance.forSale.Clear();
        foreach (var item in __instance.itemPriceAndStock.Keys)
        {
            if (filter(item))
                __instance.forSale.Add(item);
        }

        __instance.currentItemIndex = 0;
        SetScrollBarMethod?.Invoke(__instance, null);
        return false;
    }
}
#endif
