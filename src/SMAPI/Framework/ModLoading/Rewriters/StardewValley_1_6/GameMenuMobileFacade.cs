using System.Linq;
using System.Reflection;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="GameMenu"/> constructors that differ between platforms.
/// Desktop: (bool playOpeningSound=true) and (int startingTab, int extra=-1, bool playOpeningSound=true).
/// Mobile: (bool standardTabs=true, bool optionsOnly=false) and (int startingTab, int extra=-1).</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class GameMenuMobileFacade : GameMenu, IRewriteFacade
{
    /*********
    ** Fields
    *********/
    // Mobile's 2-param constructor: (int startingTab, int extra)
    // Desktop's 3-param: (int startingTab, int extra, bool playOpeningSound)
    // The extra playOpeningSound param doesn't exist on mobile.
    private static readonly ConstructorInfo? MobileTwoParamCtor = typeof(GameMenu)
        .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
        .FirstOrDefault(c =>
        {
            var p = c.GetParameters();
            return p.Length == 2 && p[0].ParameterType == typeof(int);
        });

    /*********
    ** Public methods
    *********/
    // Desktop: GameMenu(bool playOpeningSound = true)
    // Mobile: GameMenu(bool standardTabs = true, bool optionsOnly = false)
    // Map: playOpeningSound is ignored, standardTabs = true, optionsOnly = false
    public static GameMenu Constructor(bool playOpeningSound = true)
    {
        return new GameMenu();
    }

    // Desktop: GameMenu(int startingTab, int extra = -1, bool playOpeningSound = true)
    // Mobile: GameMenu(int startingTab, int extra = -1)
    // Map: drop playOpeningSound
    public static GameMenu Constructor(
        int startingTab,
        int extra = -1,
        bool playOpeningSound = true
    )
    {
        if (MobileTwoParamCtor != null)
            return (GameMenu)MobileTwoParamCtor.Invoke(new object[] { startingTab, extra });
        return new GameMenu();
    }

    /*********
    ** Private methods
    *********/
    private GameMenuMobileFacade()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
