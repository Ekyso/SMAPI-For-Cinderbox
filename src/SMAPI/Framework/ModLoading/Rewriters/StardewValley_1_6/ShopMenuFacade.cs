using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Maps Stardew Valley 1.5.6's <see cref="ShopMenu"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class ShopMenuFacade : ShopMenu, IRewriteFacade
{
    /*********
    ** Accessors
    *********/
    public string storeContext
    {
        get => base.ShopId;
        set => base.ShopId = value;
    }

    /*********
    ** Public methods
    *********/
    /// <remarks>Changed in 1.6.0. Uses Dict overload which has same params on both platforms.</remarks>
    public static ShopMenu Constructor(
        Dictionary<ISalable, int[]> itemPriceAndStock,
        int currency = 0,
        string? who = null,
        Func<ISalable, Farmer, int, bool>? on_purchase = null,
        Func<ISalable, bool>? on_sell = null,
        string? context = null
    )
    {
        return new ShopMenu(
            ShopMenuFacade.GetShopId(context),
            ShopMenuFacade.ToItemStockInformation(itemPriceAndStock),
            currency,
            who,
            ToOnPurchaseDelegate(on_purchase),
            on_sell
        );
    }

    /// <remarks>Changed in 1.6.9. ShopData overload has same signature on both platforms.</remarks>
    public static ShopMenu Constructor(
        string shopId,
        ShopData shopData,
        ShopOwnerData ownerData,
        NPC? owner = null,
        Func<ISalable, Farmer, int, bool>? onPurchase = null,
        Func<ISalable, bool>? onSell = null,
        bool playOpenSound = true
    )
    {
        return new ShopMenu(
            shopId,
            shopData,
            ownerData,
            owner,
            ToOnPurchaseDelegate(onPurchase),
            onSell,
            playOpenSound
        );
    }

    /// <remarks>Changed in 1.6.9. Dict overload has same params on both platforms (mobile adds optional context).</remarks>
    public static ShopMenu Constructor(
        string shopId,
        Dictionary<ISalable, ItemStockInformation> itemPriceAndStock,
        int currency = 0,
        string? who = null,
        Func<ISalable, Farmer, int, bool>? on_purchase = null,
        Func<ISalable, bool>? on_sell = null,
        bool playOpenSound = true
    )
    {
        return new ShopMenu(
            shopId,
            itemPriceAndStock,
            currency,
            who,
            ToOnPurchaseDelegate(on_purchase),
            on_sell,
            playOpenSound
        );
    }

    /*********
    ** Internal helpers (shared with desktop facade)
    *********/
    internal static string GetShopId(string? context)
    {
        return string.IsNullOrWhiteSpace(context)
            ? "legacy_mod_code_" + Guid.NewGuid().ToString("N")
            : context;
    }

    internal static Dictionary<ISalable, ItemStockInformation> ToItemStockInformation(
        Dictionary<ISalable, int[]>? itemPriceAndStock
    )
    {
        Dictionary<ISalable, ItemStockInformation> stock = new();

        if (itemPriceAndStock != null)
        {
            foreach (var pair in itemPriceAndStock)
                stock[pair.Key] = new ItemStockInformation(pair.Value[0], pair.Value[1]);
        }

        return stock;
    }

    /// <summary>Convert a pre-1.6.9 <see cref="ShopMenu.onPurchase"/> callback into its new delegate type.</summary>
    [return: NotNullIfNotNull(nameof(onPurchase))]
    internal static ShopMenu.OnPurchaseDelegate? ToOnPurchaseDelegate(
        Func<ISalable, Farmer, int, bool>? onPurchase
    )
    {
        return onPurchase != null
            ? (item, who, countTaken, _) => onPurchase(item, who, countTaken)
            : null;
    }

    /*********
    ** Private methods
    *********/
    private ShopMenuFacade()
        : base(null, null, null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
