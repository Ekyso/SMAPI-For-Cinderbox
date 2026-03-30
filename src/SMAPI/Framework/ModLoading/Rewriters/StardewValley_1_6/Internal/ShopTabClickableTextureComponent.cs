using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6.Internal;

/// <summary>Standalone replacement for desktop's <c>ShopMenu.ShopTabClickableTextureComponent</c>
/// nested type. Extends <see cref="ClickableTextureComponent"/> with a <see cref="Filter"/>
/// delegate for item filtering, matching the desktop API.</summary>
public class ShopTabClickableTextureComponent : ClickableTextureComponent
{
    public Func<ISalable, bool>? Filter;

    public ShopTabClickableTextureComponent(
        string name,
        Rectangle bounds,
        string? label,
        string? hoverText,
        Texture2D texture,
        Rectangle sourceRect,
        float scale,
        bool drawShadow = false
    )
        : base(name, bounds, label, hoverText, texture, sourceRect, scale, drawShadow) { }

    public ShopTabClickableTextureComponent(
        Rectangle bounds,
        Texture2D texture,
        Rectangle sourceRect,
        float scale,
        bool drawShadow = false
    )
        : base(bounds, texture, sourceRect, scale, drawShadow) { }
}
