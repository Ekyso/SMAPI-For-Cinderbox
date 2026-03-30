using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile-only facade for <see cref="IClickableMenu"/> methods where desktop has extra parameters that mobile lacks.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class IClickableMenuMobileFacade : IClickableMenu, IRewriteFacade
{
    /*********
    ** Fields
    *********/
    private static readonly Func<Item, ObjectData, string[]?>? GetBuffIconsDelegate =
        BuildGetBuffIconsDelegate();

    /*********
    ** Public methods
    *********/
    /// <summary>Desktop 1.6 drawToolTip with additionalCraftMaterials param that mobile lacks.
    /// Calls mobile's drawHoverText directly (which accepts the param) using GetBuffIcons for buff computation.</summary>
    public new static void drawToolTip(
        SpriteBatch b,
        string hoverText,
        string hoverTitle,
        Item hoveredItem,
        bool heldItem = false,
        int healAmountToDisplay = -1,
        int currencySymbol = 0,
        string? extraItemToShowIndex = null,
        int extraItemToShowAmount = -1,
        CraftingRecipe? craftingIngredients = null,
        int moneyAmountToShowAtBottom = -1,
        IList<Item>? additionalCraftMaterials = null
    )
    {
        if (additionalCraftMaterials == null)
        {
            IClickableMenu.drawToolTip(
                b,
                hoverText,
                hoverTitle,
                hoveredItem,
                heldItem,
                healAmountToDisplay,
                currencySymbol,
                extraItemToShowIndex,
                extraItemToShowAmount,
                craftingIngredients,
                moneyAmountToShowAtBottom
            );
            return;
        }

        // Compute buff icons via mobile's public GetBuffIcons API
        string[]? buffIcons = null;
        if (
            hoveredItem is StardewValley.Object obj
            && obj.edibility.Value != -300
            && Game1.objectData.TryGetValue(hoveredItem.ItemId, out var rawData)
            && GetBuffIconsDelegate != null
        )
        {
            buffIcons = GetBuffIconsDelegate(hoveredItem, rawData);
        }

        int edibility =
            hoveredItem is StardewValley.Object edObj && edObj.edibility.Value != -300
                ? edObj.edibility.Value
                : -1;

        IClickableMenu.drawHoverText(
            b,
            hoverText,
            Game1.smallFont,
            heldItem ? 40 : 0,
            heldItem ? 40 : 0,
            moneyAmountToShowAtBottom,
            hoverTitle,
            edibility,
            buffIcons,
            hoveredItem,
            currencySymbol,
            extraItemToShowIndex,
            extraItemToShowAmount,
            -1,
            -1,
            1f,
            craftingIngredients,
            additionalCraftMaterials
        );
    }

    /*********
    ** Private methods
    *********/
    private IClickableMenuMobileFacade()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }

    private static Func<Item, ObjectData, string[]?>? BuildGetBuffIconsDelegate()
    {
        var method = typeof(IClickableMenu).GetMethod(
            "GetBuffIcons",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(Item), typeof(ObjectData)],
            null
        );
        if (method == null)
            return null;

        return (Func<Item, ObjectData, string[]?>)
            Delegate.CreateDelegate(typeof(Func<Item, ObjectData, string[]?>), method);
    }
}
