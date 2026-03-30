using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="InventoryPage.ShouldShowJunimoNoteIcon"/>
/// which exists on desktop but not mobile.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class InventoryPageMobileFacade : InventoryPage, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public static bool ShouldShowJunimoNoteIcon()
    {
        if (
            Game1.player.hasOrWillReceiveMail("canReadJunimoText")
            && !Game1.player.hasOrWillReceiveMail("JojaMember")
        )
        {
            if (Game1.MasterPlayer.hasCompletedCommunityCenter())
            {
                if (Game1.player.hasOrWillReceiveMail("hasSeenAbandonedJunimoNote"))
                    return !Game1.MasterPlayer.hasOrWillReceiveMail("ccMovieTheater");
                return false;
            }
            return true;
        }
        return false;
    }

    /*********
    ** Private methods
    *********/
    private InventoryPageMobileFacade()
        : base(0, 0, 0, 0)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
