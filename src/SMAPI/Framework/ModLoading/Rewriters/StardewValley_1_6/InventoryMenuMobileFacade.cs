using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="InventoryMenu.SetPosition"/> which exists on desktop but not mobile.
/// Implements it using mobile's existing <see cref="InventoryMenu.movePosition"/>.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class InventoryMenuMobileFacade : InventoryMenu, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public new void SetPosition(int x, int y)
    {
        base.movePosition(-base.xPositionOnScreen, -base.yPositionOnScreen);
        base.movePosition(x, y);
    }

    /*********
    ** Private methods
    *********/
    private InventoryMenuMobileFacade()
        : base(0, 0, false)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
