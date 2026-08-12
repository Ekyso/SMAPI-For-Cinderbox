using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="LetterViewerMenu.OnPageChange"/>
/// which exists on desktop but not mobile.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class LetterViewerMenuMobileFacade : LetterViewerMenu, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public new void OnPageChange()
    {
        base.forwardButton.visible = base.page < base.mailMessage.Count - 1;
        base.backButton.visible = base.page > 0;

        bool showInteractable = base.ShouldShowInteractable();
        foreach (var item in base.itemsToGrab)
            item.visible = showInteractable;
        if (base.acceptQuestButton != null)
            base.acceptQuestButton.visible = showInteractable;

        if (
            Game1.options.SnappyMenus
            && (base.currentlySnappedComponent == null || !base.currentlySnappedComponent.visible)
        )
        {
            base.snapToDefaultClickableComponent();
        }
    }

    /*********
    ** Private methods
    *********/
    private LetterViewerMenuMobileFacade()
        : base("")
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
