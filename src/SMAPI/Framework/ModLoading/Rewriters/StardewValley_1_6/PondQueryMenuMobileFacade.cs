using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="PondQueryMenu"/> which has <c>okButton</c>
/// on desktop but not mobile. Mobile uses <c>upperRightCloseButton</c> instead.
/// This facade maps okButton to the close button so mods can access it.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class PondQueryMenuMobileFacade : PondQueryMenu, IRewriteFacade
{
    /*********
    ** Properties
    *********/
    // Desktop has okButton for closing the menu. Mobile uses upperRightCloseButton instead.
    public new ClickableTextureComponent? okButton
    {
        get => base.upperRightCloseButton;
        set => base.upperRightCloseButton = value;
    }

    /*********
    ** Private methods
    *********/
    private PondQueryMenuMobileFacade()
        : base(null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
