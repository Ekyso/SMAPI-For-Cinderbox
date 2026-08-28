using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="CharacterCustomization"/> fields that exist on desktop
/// but not on mobile's CharacterCustomization class directly. On mobile, these fields live on the
/// <c>MobileCustomizer</c> child page (pages[0]). This facade delegates to that page.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class CharacterCustomizationMobileFacade : CharacterCustomization, IRewriteFacade
{
    /*********
    ** Fields - cached reflection for MobileCustomizer fields
    *********/
    private static readonly FieldInfo? LabelsField = GetCustomizerField("labels");
    private static readonly FieldInfo? GenderButtonsField = GetCustomizerField("genderButtons");
    private static readonly FieldInfo? ColorPickerCCsField = GetCustomizerField("colorPickerCCs");
    private static readonly FieldInfo? OkButtonField = GetCustomizerField("okButton");
    private static readonly FieldInfo? RandomButtonField = GetCustomizerField("randomButton");
    private static readonly FieldInfo? NameBoxCCField = GetCustomizerField("nameBoxCC");
    private static readonly FieldInfo? FarmnameBoxCCField = GetCustomizerField("farmnameBoxCC");
    private static readonly FieldInfo? FavThingBoxCCField = GetCustomizerField("favThingBoxCC");
    private static readonly FieldInfo? EyeColorPickerField = GetCustomizerField("eyeColorPicker");

    private static readonly ConstructorInfo? DesktopConstructor = typeof(CharacterCustomization).GetConstructor(
        [typeof(CharacterCustomization.Source), typeof(bool)]
    );

    private static readonly ConstructorInfo? MobileConstructor = typeof(CharacterCustomization).GetConstructor(
        [typeof(CharacterCustomization.Source), typeof(bool), typeof(Clothing)]
    );

    private class EyePickerHolder
    {
        public ColorPicker? Picker;
    }

    private static readonly ConditionalWeakTable<
        CharacterCustomization,
        EyePickerHolder
    > EyeColorPickerStorage = new();

    /*********
    ** Properties - delegate to MobileCustomizer page
    *********/
    public new List<ClickableComponent>? labels
    {
        get => GetCustomizerFieldValue<List<ClickableComponent>>(this, LabelsField);
        set => SetCustomizerFieldValue(this, LabelsField, value);
    }

    public new List<ClickableComponent>? genderButtons
    {
        get => GetCustomizerFieldValue<List<ClickableComponent>>(this, GenderButtonsField);
        set => SetCustomizerFieldValue(this, GenderButtonsField, value);
    }

    public new List<ClickableComponent>? colorPickerCCs
    {
        get => GetCustomizerFieldValue<List<ClickableComponent>>(this, ColorPickerCCsField);
        set => SetCustomizerFieldValue(this, ColorPickerCCsField, value);
    }

    public new ClickableTextureComponent? okButton
    {
        get => GetCustomizerFieldValue<ClickableTextureComponent>(this, OkButtonField);
        set => SetCustomizerFieldValue(this, OkButtonField, value);
    }

    public new ClickableTextureComponent? randomButton
    {
        get => GetCustomizerFieldValue<ClickableTextureComponent>(this, RandomButtonField);
        set => SetCustomizerFieldValue(this, RandomButtonField, value);
    }

    public new ClickableComponent? nameBoxCC
    {
        get => GetCustomizerFieldValue<ClickableComponent>(this, NameBoxCCField);
        set => SetCustomizerFieldValue(this, NameBoxCCField, value);
    }

    public new ClickableComponent? farmnameBoxCC
    {
        get => GetCustomizerFieldValue<ClickableComponent>(this, FarmnameBoxCCField);
        set => SetCustomizerFieldValue(this, FarmnameBoxCCField, value);
    }

    public new ClickableComponent? favThingBoxCC
    {
        get => GetCustomizerFieldValue<ClickableComponent>(this, FavThingBoxCCField);
        set => SetCustomizerFieldValue(this, FavThingBoxCCField, value);
    }

    // Desktop: ColorPicker, Mobile: MobileColorPicker (different unrelated types).
    // Provide a ColorPicker instance backed by ConditionalWeakTable since the
    // mobile MobileCustomizer's MobileColorPicker is not assignment-compatible.
    public new ColorPicker? eyeColorPicker
    {
        get
        {
            var extra = EyeColorPickerStorage.GetOrCreateValue(
                (CharacterCustomization)(object)this
            );
            if (extra.Picker == null)
                extra.Picker = new ColorPicker("Eyes", 0, 0);
            return extra.Picker;
        }
        set
        {
            var extra = EyeColorPickerStorage.GetOrCreateValue(
                (CharacterCustomization)(object)this
            );
            extra.Picker = value;
        }
    }

    // Mobile doesn't have selection buttons - provide empty lists
    public new List<ClickableComponent> leftSelectionButtons
    {
        get => new();
        set { }
    }

    public new List<ClickableComponent> rightSelectionButtons
    {
        get => new();
        set { }
    }

    /*********
    ** Methods
    *********/
    public static CharacterCustomization Constructor(
        CharacterCustomization.Source source,
        bool multiplayerServer = false
    )
    {
        if (DesktopConstructor != null)
        {
            return (CharacterCustomization)
                DesktopConstructor.Invoke([source, multiplayerServer]);
        }
        if (MobileConstructor != null)
            return (CharacterCustomization)MobileConstructor.Invoke([source, false, null]);

        throw new MissingMethodException(
            typeof(CharacterCustomization).FullName,
            ".ctor with desktop or mobile parameters"
        );
    }

    public new bool canLeaveMenu()
    {
        if (
            base.source != CharacterCustomization.Source.ClothesDye
            && base.source != CharacterCustomization.Source.DyePots
        )
        {
            return Game1.player.Name.Length > 0
                && Game1.player.farmName.Length > 0
                && Game1.player.favoriteThing.Length > 0;
        }
        return true;
    }

    /*********
    ** Private methods
    *********/
    private CharacterCustomizationMobileFacade()
        : base(null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }

    /// <summary>Get the MobileCustomizer page (pages[0]) from a CharacterCustomization menu.</summary>
    private static IClickableMenu? GetCustomizerPage(CharacterCustomization menu)
    {
        // Mobile's CharacterCustomization extends GameMenu (desktop extends IClickableMenu).
        // Cast through object to avoid compile-time type check failure.
        if ((object)menu is GameMenu gameMenu && gameMenu.pages.Count > 0)
            return gameMenu.pages[0];
        return null;
    }

    /// <summary>Get a FieldInfo for a field on the MobileCustomizer type (resolved at runtime).</summary>
    private static FieldInfo? GetCustomizerField(string name)
    {
        var customizerType = typeof(CharacterCustomization).Assembly.GetType(
            "StardewValley.Menus.MobileCustomizer"
        );
        return customizerType?.GetField(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        );
    }

    private static T? GetCustomizerFieldValue<T>(CharacterCustomization menu, FieldInfo? field)
        where T : class
    {
        var page = GetCustomizerPage((CharacterCustomization)(object)menu);
        return page != null && field != null ? field.GetValue(page) as T : null;
    }

    private static void SetCustomizerFieldValue(
        CharacterCustomization menu,
        FieldInfo? field,
        object? value
    )
    {
        var page = GetCustomizerPage((CharacterCustomization)(object)menu);
        if (page != null && field != null)
            field.SetValue(page, value);
    }
}
