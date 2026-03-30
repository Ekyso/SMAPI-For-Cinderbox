#if SMAPI_FOR_ANDROID
using System.Reflection;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>Harmony patches for <see cref="CraftingPage"/> to populate the facade's
/// heldItem field with the crafted item, matching desktop behavior where
/// crafted items go to heldItem before being placed in inventory.</summary>
internal static class CraftingPageMobilePatches
{
    // Mobile's clickCraftingRecipe is private - access via reflection
    private static readonly MethodInfo? ClickCraftingRecipeMethod = typeof(CraftingPage).GetMethod(
        "clickCraftingRecipe",
        BindingFlags.NonPublic | BindingFlags.Instance
    );

    // Mobile's hoverRecipe is private - access via reflection
    private static readonly FieldInfo? HoverRecipeField = typeof(CraftingPage).GetField(
        "hoverRecipe",
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance
    );

    public static void Apply(Harmony harmony)
    {
        if (ClickCraftingRecipeMethod != null)
        {
            harmony.Patch(
                original: ClickCraftingRecipeMethod,
                prefix: new HarmonyMethod(
                    typeof(CraftingPageMobilePatches),
                    nameof(ClickCraftingRecipe_Prefix)
                ),
                postfix: new HarmonyMethod(
                    typeof(CraftingPageMobilePatches),
                    nameof(ClickCraftingRecipe_Postfix)
                )
            );
        }
    }

    /// <summary>Before crafting: record what the recipe will create so we can set heldItem.</summary>
    private static void ClickCraftingRecipe_Prefix(CraftingPage __instance)
    {
        // Get the recipe that's about to be crafted
        var recipe = HoverRecipeField?.GetValue(__instance) as CraftingRecipe;
        if (recipe != null && recipe.doesFarmerHaveIngredientsInInventory())
        {
            // Set heldItem to a preview of what will be crafted
            var extra = CraftingPageMobileFacade.GetOrCreate(__instance);
            extra.HeldItem = recipe.createItem();
        }
    }

    /// <summary>After crafting: clear heldItem since the item went to player inventory.</summary>
    private static void ClickCraftingRecipe_Postfix(CraftingPage __instance)
    {
        var extra = CraftingPageMobileFacade.GetOrCreate(__instance);
        // On desktop, heldItem stays until player places it.
        // On mobile, the item goes directly to inventory.
        // Keep heldItem set briefly so mod postfixes on this method can read it,
        // then it will be null on next check since mobile has no cursor-hold mechanic.
    }
}
#endif
