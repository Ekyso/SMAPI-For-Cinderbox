using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
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
    private static readonly Lazy<MobileDrawToolTipDelegate> DrawToolTipImpl = new(() =>
        GetRequiredStaticDelegate<MobileDrawToolTipDelegate>(
            typeof(IClickableMenu),
            nameof(IClickableMenu.drawToolTip)
        )
    );

    private static readonly Lazy<MobileDrawHoverTextDelegate> DrawHoverTextImpl = new(() =>
        GetRequiredStaticDelegate<MobileDrawHoverTextDelegate>(
            typeof(IClickableMenu),
            nameof(IClickableMenu.drawHoverText)
        )
    );

    private static readonly Lazy<MobileDrawHoverTextBuilderDelegate> DrawHoverTextBuilderImpl =
        new(() =>
            GetRequiredStaticDelegate<MobileDrawHoverTextBuilderDelegate>(
                typeof(IClickableMenu),
                nameof(IClickableMenu.drawHoverText)
            )
        );

    private static readonly Lazy<MobileDrawTextureBoxDelegate> DrawTextureBoxImpl = new(() =>
        GetRequiredStaticDelegate<MobileDrawTextureBoxDelegate>(
            typeof(IClickableMenu),
            nameof(IClickableMenu.drawTextureBox)
        )
    );

    private static readonly Func<Item, ObjectData, string[]?>? GetBuffIconsDelegate =
        BuildGetBuffIconsDelegate();

    /*********
    ** Delegates
    *********/
    private delegate void MobileDrawToolTipDelegate(
        SpriteBatch b,
        string hoverText,
        string hoverTitle,
        Item hoveredItem,
        bool heldItem,
        int healAmountToDisplay,
        int currencySymbol,
        string? extraItemToShowIndex,
        int extraItemToShowAmount,
        CraftingRecipe? craftingIngredients,
        int moneyAmountToShowAtBottom
    );

    private delegate void MobileDrawHoverTextDelegate(
        SpriteBatch b,
        string text,
        SpriteFont font,
        int xOffset,
        int yOffset,
        int moneyAmountToDisplayAtBottom,
        string? boldTitleText,
        int healAmountToDisplay,
        string[]? buffIconsToDisplay,
        Item? hoveredItem,
        int currencySymbol,
        string? extraItemToShowIndex,
        int extraItemToShowAmount,
        int overrideX,
        int overrideY,
        float alpha,
        CraftingRecipe? craftingIngredients,
        IList<Item>? additionalCraftMaterials,
        Texture2D? boxTexture,
        Rectangle? boxSourceRect,
        Color? textColor,
        Color? textShadowColor,
        float boxScale,
        int boxWidthOverride,
        int boxHeightOverride,
        int stackNumber
    );

    private delegate void MobileDrawHoverTextBuilderDelegate(
        SpriteBatch b,
        StringBuilder text,
        SpriteFont font,
        int xOffset,
        int yOffset,
        int moneyAmountToDisplayAtBottom,
        string? boldTitleText,
        int healAmountToDisplay,
        string[]? buffIconsToDisplay,
        Item? hoveredItem,
        int currencySymbol,
        string? extraItemToShowIndex,
        int extraItemToShowAmount,
        int overrideX,
        int overrideY,
        float alpha,
        CraftingRecipe? craftingIngredients,
        IList<Item>? additionalCraftMaterials,
        Texture2D? boxTexture,
        Rectangle? boxSourceRect,
        Color? textColor,
        Color? textShadowColor,
        float boxScale,
        int boxWidthOverride,
        int boxHeightOverride,
        int stackNumber
    );

    private delegate void MobileDrawTextureBoxDelegate(
        SpriteBatch b,
        Texture2D texture,
        Rectangle sourceRect,
        int x,
        int y,
        int width,
        int height,
        Color color,
        float scale,
        bool drawShadow,
        float drawLayer,
        bool ignoreBorder
    );

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
            DrawToolTipImpl.Value(
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

        DrawHoverTextImpl.Value(
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
            additionalCraftMaterials,
            null,
            null,
            null,
            null,
            1f,
            -1,
            -1,
            -1
        );
    }

    /// <summary>Older desktop tooltip signature which used a numeric extra-item ID.</summary>
    public static void drawToolTip(
        SpriteBatch b,
        string hoverText,
        string hoverTitle,
        Item hoveredItem,
        bool heldItem = false,
        int healAmountToDisplay = -1,
        int currencySymbol = 0,
        int extraItemToShowIndex = -1,
        int extraItemToShowAmount = -1,
        CraftingRecipe? craftingIngredients = null,
        int moneyAmountToShowAtBottom = -1
    )
    {
        drawToolTip(
            b,
            hoverText,
            hoverTitle,
            hoveredItem,
            heldItem,
            healAmountToDisplay,
            currencySymbol,
            extraItemToShowIndex != -1 ? extraItemToShowIndex.ToString() : null,
            extraItemToShowAmount,
            craftingIngredients,
            moneyAmountToShowAtBottom,
            null
        );
    }

    /// <summary>Desktop hover-text signature; mobile adds a final stack-number parameter.</summary>
    public new static void drawHoverText(
        SpriteBatch b,
        string text,
        SpriteFont font,
        int xOffset = 0,
        int yOffset = 0,
        int moneyAmountToDisplayAtBottom = -1,
        string? boldTitleText = null,
        int healAmountToDisplay = -1,
        string[]? buffIconsToDisplay = null,
        Item? hoveredItem = null,
        int currencySymbol = 0,
        string? extraItemToShowIndex = null,
        int extraItemToShowAmount = -1,
        int overrideX = -1,
        int overrideY = -1,
        float alpha = 1f,
        CraftingRecipe? craftingIngredients = null,
        IList<Item>? additionalCraftMaterials = null,
        Texture2D? boxTexture = null,
        Rectangle? boxSourceRect = null,
        Color? textColor = null,
        Color? textShadowColor = null,
        float boxScale = 1f,
        int boxWidthOverride = -1,
        int boxHeightOverride = -1
    )
    {
        DrawHoverTextImpl.Value(
            b,
            text,
            font,
            xOffset,
            yOffset,
            moneyAmountToDisplayAtBottom,
            boldTitleText,
            healAmountToDisplay,
            buffIconsToDisplay,
            hoveredItem,
            currencySymbol,
            extraItemToShowIndex,
            extraItemToShowAmount,
            overrideX,
            overrideY,
            alpha,
            craftingIngredients,
            additionalCraftMaterials,
            boxTexture,
            boxSourceRect,
            textColor,
            textShadowColor,
            boxScale,
            boxWidthOverride,
            boxHeightOverride,
            -1
        );
    }

    /// <summary>Desktop hover-text signature; mobile adds a final stack-number parameter.</summary>
    public new static void drawHoverText(
        SpriteBatch b,
        StringBuilder text,
        SpriteFont font,
        int xOffset = 0,
        int yOffset = 0,
        int moneyAmountToDisplayAtBottom = -1,
        string? boldTitleText = null,
        int healAmountToDisplay = -1,
        string[]? buffIconsToDisplay = null,
        Item? hoveredItem = null,
        int currencySymbol = 0,
        string? extraItemToShowIndex = null,
        int extraItemToShowAmount = -1,
        int overrideX = -1,
        int overrideY = -1,
        float alpha = 1f,
        CraftingRecipe? craftingIngredients = null,
        IList<Item>? additionalCraftMaterials = null,
        Texture2D? boxTexture = null,
        Rectangle? boxSourceRect = null,
        Color? textColor = null,
        Color? textShadowColor = null,
        float boxScale = 1f,
        int boxWidthOverride = -1,
        int boxHeightOverride = -1
    )
    {
        DrawHoverTextBuilderImpl.Value(
            b,
            text,
            font,
            xOffset,
            yOffset,
            moneyAmountToDisplayAtBottom,
            boldTitleText,
            healAmountToDisplay,
            buffIconsToDisplay,
            hoveredItem,
            currencySymbol,
            extraItemToShowIndex,
            extraItemToShowAmount,
            overrideX,
            overrideY,
            alpha,
            craftingIngredients,
            additionalCraftMaterials,
            boxTexture,
            boxSourceRect,
            textColor,
            textShadowColor,
            boxScale,
            boxWidthOverride,
            boxHeightOverride,
            -1
        );
    }

    /// <summary>Older desktop hover-text signature which used a numeric extra-item ID.</summary>
    public static void drawHoverText(
        SpriteBatch b,
        string text,
        SpriteFont font,
        int xOffset = 0,
        int yOffset = 0,
        int moneyAmountToDisplayAtBottom = -1,
        string? boldTitleText = null,
        int healAmountToDisplay = -1,
        string[]? buffIconsToDisplay = null,
        Item? hoveredItem = null,
        int currencySymbol = 0,
        int extraItemToShowIndex = -1,
        int extraItemToShowAmount = -1,
        int overrideX = -1,
        int overrideY = -1,
        float alpha = 1f,
        CraftingRecipe? craftingIngredients = null,
        IList<Item>? additionalCraftMaterials = null
    )
    {
        drawHoverText(
            b,
            text,
            font,
            xOffset,
            yOffset,
            moneyAmountToDisplayAtBottom,
            boldTitleText,
            healAmountToDisplay,
            buffIconsToDisplay,
            hoveredItem,
            currencySymbol,
            extraItemToShowIndex != -1 ? extraItemToShowIndex.ToString() : null,
            extraItemToShowAmount,
            overrideX,
            overrideY,
            alpha,
            craftingIngredients,
            additionalCraftMaterials
        );
    }

    /// <summary>Older desktop hover-text signature which used a numeric extra-item ID.</summary>
    public static void drawHoverText(
        SpriteBatch b,
        StringBuilder text,
        SpriteFont font,
        int xOffset = 0,
        int yOffset = 0,
        int moneyAmountToDisplayAtBottom = -1,
        string? boldTitleText = null,
        int healAmountToDisplay = -1,
        string[]? buffIconsToDisplay = null,
        Item? hoveredItem = null,
        int currencySymbol = 0,
        int extraItemToShowIndex = -1,
        int extraItemToShowAmount = -1,
        int overrideX = -1,
        int overrideY = -1,
        float alpha = 1f,
        CraftingRecipe? craftingIngredients = null,
        IList<Item>? additionalCraftMaterials = null
    )
    {
        drawHoverText(
            b,
            text,
            font,
            xOffset,
            yOffset,
            moneyAmountToDisplayAtBottom,
            boldTitleText,
            healAmountToDisplay,
            buffIconsToDisplay,
            hoveredItem,
            currencySymbol,
            extraItemToShowIndex != -1 ? extraItemToShowIndex.ToString() : null,
            extraItemToShowAmount,
            overrideX,
            overrideY,
            alpha,
            craftingIngredients,
            additionalCraftMaterials
        );
    }

    /// <summary>Desktop textured-box signature; mobile adds a final ignore-border parameter.</summary>
    public new static void drawTextureBox(
        SpriteBatch b,
        Texture2D texture,
        Rectangle sourceRect,
        int x,
        int y,
        int width,
        int height,
        Color color,
        float scale = 1f,
        bool drawShadow = true,
        float drawLayer = -1f
    )
    {
        DrawTextureBoxImpl.Value(
            b,
            texture,
            sourceRect,
            x,
            y,
            width,
            height,
            color,
            scale,
            drawShadow,
            drawLayer,
            false
        );
    }

    /*********
    ** Private methods
    *********/
    private IClickableMenuMobileFacade()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }

    /// <summary>Get a delegate for an exact public static runtime method signature.</summary>
    /// <remarks>The mobile game methods must be resolved at runtime because SMAPI is compiled against the desktop game assembly.</remarks>
    internal static TDelegate GetRequiredStaticDelegate<TDelegate>(
        Type declaringType,
        string methodName
    )
        where TDelegate : Delegate
    {
        MethodInfo invokeMethod = typeof(TDelegate).GetMethod("Invoke")!;
        ParameterInfo[] delegateParameters = invokeMethod.GetParameters();
        Type[] parameterTypes = new Type[delegateParameters.Length];
        for (int i = 0; i < delegateParameters.Length; i++)
            parameterTypes[i] = delegateParameters[i].ParameterType;

        MethodInfo? method = declaringType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            null,
            parameterTypes,
            null
        );
        if (method == null || method.ReturnType != invokeMethod.ReturnType)
        {
            throw new MissingMethodException(
                $"Required mobile method {declaringType.FullName}.{methodName} with the expected signature was not found."
            );
        }

        return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), method);
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
