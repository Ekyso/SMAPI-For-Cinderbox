using Microsoft.Xna.Framework;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Desktop-only facade for <see cref="DiscreteColorPicker"/> methods that became static in desktop 1.6 but remain instance on mobile.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class DiscreteColorPickerDesktopFacade : DiscreteColorPicker, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public new int getSelectionFromColor(Color c)
    {
        return DiscreteColorPicker.getSelectionFromColor(c);
    }

    public new Color getColorFromSelection(int selection)
    {
        return DiscreteColorPicker.getColorFromSelection(selection);
    }

    /*********
    ** Private methods
    *********/
    private DiscreteColorPickerDesktopFacade()
        : base(0, 0)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
