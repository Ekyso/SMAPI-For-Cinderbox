using System;
using System.Collections.Generic;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Desktop-only facade for <see cref="ShopMenu"/> constructors that pass <c>playOpenSound</c>,
/// which doesn't exist on the mobile <c>List&lt;ISalable&gt;</c> overload (mobile has <c>string context</c> instead).</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class ShopMenuDesktopFacade : ShopMenu, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    /// <remarks>Changed in 1.6.0. List overload - desktop has playOpenSound, mobile has context.</remarks>
    public static ShopMenu Constructor(
        List<ISalable> itemsForSale,
        int currency = 0,
        string? who = null,
        Func<ISalable, Farmer, int, bool>? on_purchase = null,
        Func<ISalable, bool>? on_sell = null,
        string? context = null
    )
    {
        return new ShopMenu(
            ShopMenuFacade.GetShopId(context),
            itemsForSale,
            currency,
            who,
            ShopMenuFacade.ToOnPurchaseDelegate(on_purchase),
            on_sell,
            playOpenSound: true
        );
    }

    /// <remarks>Changed in 1.6.9. List overload - desktop has playOpenSound, mobile has context.</remarks>
    public static ShopMenu Constructor(
        string shopId,
        List<ISalable> itemsForSale,
        int currency = 0,
        string? who = null,
        Func<ISalable, Farmer, int, bool>? on_purchase = null,
        Func<ISalable, bool>? on_sell = null,
        bool playOpenSound = true
    )
    {
        return new ShopMenu(
            shopId,
            itemsForSale,
            currency,
            who,
            ShopMenuFacade.ToOnPurchaseDelegate(on_purchase),
            on_sell,
            playOpenSound
        );
    }

    /*********
    ** Private methods
    *********/
    private ShopMenuDesktopFacade()
        : base(null, null, null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
