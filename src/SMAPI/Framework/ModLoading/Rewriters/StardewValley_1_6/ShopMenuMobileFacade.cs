using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6.Internal;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile-only facade for <see cref="ShopMenu"/> constructors and tab methods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class ShopMenuMobileFacade : ShopMenu, IRewriteFacade
{
    /*********
    ** Fields
    *********/
    // tabButtons is List<ShopTabClickableTextureComponent> on desktop, List<ClickableTextureComponent> on mobile.
    // Use reflection to set the field since the generic type differs at compile time vs runtime.
    private static readonly FieldInfo TabButtonsField = typeof(ShopMenu).GetField(
        "tabButtons",
        BindingFlags.Public | BindingFlags.Instance
    )!;

    private static readonly ConstructorInfo? DesktopListConstructor = typeof(ShopMenu).GetConstructor(
        [
            typeof(string),
            typeof(List<ISalable>),
            typeof(int),
            typeof(string),
            typeof(ShopMenu.OnPurchaseDelegate),
            typeof(Func<ISalable, bool>),
            typeof(bool),
        ]
    );

    private static readonly ConstructorInfo? MobileListConstructor = typeof(ShopMenu).GetConstructor(
        [
            typeof(string),
            typeof(List<ISalable>),
            typeof(int),
            typeof(string),
            typeof(ShopMenu.OnPurchaseDelegate),
            typeof(Func<ISalable, bool>),
            typeof(string),
        ]
    );

    private static readonly ConstructorInfo? DesktopDictionaryConstructor = typeof(ShopMenu).GetConstructor(
        [
            typeof(string),
            typeof(Dictionary<ISalable, ItemStockInformation>),
            typeof(int),
            typeof(string),
            typeof(ShopMenu.OnPurchaseDelegate),
            typeof(Func<ISalable, bool>),
            typeof(bool),
        ]
    );

    private static readonly ConstructorInfo? MobileDictionaryConstructor = typeof(ShopMenu).GetConstructor(
        [
            typeof(string),
            typeof(Dictionary<ISalable, ItemStockInformation>),
            typeof(int),
            typeof(string),
            typeof(ShopMenu.OnPurchaseDelegate),
            typeof(Func<ISalable, bool>),
            typeof(bool),
            typeof(string),
        ]
    );

    /*********
    ** Tab methods - static helpers called via MapMethod.
    ** Desktop has these as instance methods with ShopTabClickableTextureComponent + Filter delegates.
    ** The ShopMenu instance (this) becomes the first parameter via IL rewriting.
    *********/
    public static void UseNoTabs(ShopMenu menu)
    {
        TabButtonsField.SetValue(menu, new List<ClickableTextureComponent>());
        menu.repositionTabs();
    }

    public static void UseCatalogueTabs(ShopMenu menu)
    {
        TabButtonsField.SetValue(
            menu,
            new List<ClickableTextureComponent>
            {
                MakeTab(99999, 96, 48, _ => true),
                MakeTab(100000, 48, 64, item => item is Wallpaper w && w.isFloor.Value),
                MakeTab(100001, 32, 64, item => item is Wallpaper w && !w.isFloor.Value),
            }
        );
        menu.repositionTabs();
    }

    public static void UseFurnitureCatalogueTabs(ShopMenu menu)
    {
        TabButtonsField.SetValue(
            menu,
            new List<ClickableTextureComponent>
            {
                MakeTab(99999, 96, 48, _ => true),
                MakeTab(
                    100000,
                    80,
                    48,
                    item => item is Furniture f && (f.IsTable() || f.furniture_type.Value == 4)
                ),
                MakeTab(
                    100001,
                    64,
                    48,
                    item =>
                        item is Furniture f
                        && (
                            f.furniture_type.Value == 0
                            || f.furniture_type.Value == 1
                            || f.furniture_type.Value == 2
                            || f.furniture_type.Value == 3
                        )
                ),
                MakeTab(
                    100002,
                    64,
                    64,
                    item =>
                        item is Furniture f
                        && (f.furniture_type.Value == 6 || f.furniture_type.Value == 13)
                ),
                MakeTab(
                    100003,
                    96,
                    64,
                    item => item is Furniture f && f.furniture_type.Value == 12
                ),
                MakeTab(
                    100004,
                    80,
                    64,
                    item =>
                        item is Furniture f
                        && (
                            f.furniture_type.Value == 7
                            || f.furniture_type.Value == 17
                            || f.furniture_type.Value == 10
                            || f.furniture_type.Value == 8
                            || f.furniture_type.Value == 9
                            || f.furniture_type.Value == 14
                        )
                ),
            }
        );
        menu.repositionTabs();
    }

    public static void UseDresserTabs(ShopMenu menu)
    {
        TabButtonsField.SetValue(
            menu,
            new List<ClickableTextureComponent>
            {
                MakeTab(99999, 0, 48, _ => true),
                MakeTab(100000, 16, 48, item => item is Item i && i.Category == -95),
                MakeTab(
                    100001,
                    32,
                    48,
                    item => item is Clothing c && c.clothesType.Value == Clothing.ClothesType.SHIRT
                ),
                MakeTab(
                    100002,
                    48,
                    48,
                    item => item is Clothing c && c.clothesType.Value == Clothing.ClothesType.PANTS
                ),
                MakeTab(100003, 0, 64, item => item is Item i && i.Category == -97),
                MakeTab(100004, 16, 64, item => item is Item i && i.Category == -96),
            }
        );
        menu.repositionTabs();
    }

    /*********
    ** Constructor methods
    *********/
    /// <remarks>Changed in 1.6.0. Adapts old List constructor for mobile.</remarks>
    public static ShopMenu Constructor(
        List<ISalable> itemsForSale,
        int currency = 0,
        string? who = null,
        Func<ISalable, Farmer, int, bool>? on_purchase = null,
        Func<ISalable, bool>? on_sell = null,
        string? context = null
    )
    {
        return CreateList(
            ShopMenuFacade.GetShopId(context),
            itemsForSale,
            currency,
            who,
            ShopMenuFacade.ToOnPurchaseDelegate(on_purchase),
            on_sell,
            true,
            context
        );
    }

    /// <remarks>Changed in 1.6.9. Adapts desktop List+playOpenSound constructor for mobile.</remarks>
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
        return CreateList(
            shopId,
            itemsForSale,
            currency,
            who,
            ShopMenuFacade.ToOnPurchaseDelegate(on_purchase),
            on_sell,
            playOpenSound,
            null
        );
    }

    internal static ShopMenu CreateDictionary(
        string shopId,
        Dictionary<ISalable, ItemStockInformation> itemPriceAndStock,
        int currency,
        string? who,
        ShopMenu.OnPurchaseDelegate? onPurchase,
        Func<ISalable, bool>? onSell,
        bool playOpenSound,
        string? context
    )
    {
        if (DesktopDictionaryConstructor != null)
        {
            return (ShopMenu)
                DesktopDictionaryConstructor.Invoke(
                    [shopId, itemPriceAndStock, currency, who, onPurchase, onSell, playOpenSound]
                );
        }
        if (MobileDictionaryConstructor != null)
        {
            return (ShopMenu)
                MobileDictionaryConstructor.Invoke(
                    [
                        shopId,
                        itemPriceAndStock,
                        currency,
                        who,
                        onPurchase,
                        onSell,
                        playOpenSound,
                        context,
                    ]
                );
        }

        throw new MissingMethodException(
            typeof(ShopMenu).FullName,
            ".ctor with desktop or mobile dictionary parameters"
        );
    }

    /*********
    ** Private methods
    *********/
    private ShopMenuMobileFacade()
        : base(null, null, null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }

    private static ShopMenu CreateList(
        string shopId,
        List<ISalable> itemsForSale,
        int currency,
        string? who,
        ShopMenu.OnPurchaseDelegate? onPurchase,
        Func<ISalable, bool>? onSell,
        bool playOpenSound,
        string? context
    )
    {
        if (DesktopListConstructor != null)
        {
            return (ShopMenu)
                DesktopListConstructor.Invoke(
                    [shopId, itemsForSale, currency, who, onPurchase, onSell, playOpenSound]
                );
        }
        if (MobileListConstructor != null)
        {
            return (ShopMenu)
                MobileListConstructor.Invoke(
                    [shopId, itemsForSale, currency, who, onPurchase, onSell, context]
                );
        }

        throw new MissingMethodException(
            typeof(ShopMenu).FullName,
            ".ctor with desktop or mobile list parameters"
        );
    }

    private static ShopTabClickableTextureComponent MakeTab(
        int myID,
        int srcX,
        int srcY,
        Func<ISalable, bool> filter
    )
    {
        return new ShopTabClickableTextureComponent(
            new Rectangle(0, 0, 64, 64),
            Game1.mouseCursors2,
            new Rectangle(srcX, srcY, 16, 16),
            4f
        )
        {
            myID = myID,
            upNeighborID = -99998,
            downNeighborID = -99998,
            rightNeighborID = 3546,
            Filter = filter,
        };
    }
}
