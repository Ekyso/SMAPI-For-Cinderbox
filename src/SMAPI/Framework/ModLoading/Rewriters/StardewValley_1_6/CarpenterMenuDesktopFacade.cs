using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Desktop-only facade for <see cref="CarpenterMenu"/> methods that reference members not present on mobile.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class CarpenterMenuDesktopFacade : CarpenterMenu, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    // Desktop 1.6 renamed setNewActiveBlueprint() to SetNewActiveBlueprint(BlueprintEntry).
    // Mobile still has the parameterless setNewActiveBlueprint() natively.
    public void setNewActiveBlueprint()
    {
        base.SetNewActiveBlueprint(base.Blueprint);
    }

    /*********
    ** Private methods
    *********/
    private CarpenterMenuDesktopFacade()
        : base(null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
