using System;
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
    private static readonly ConstructorInfo? DesktopBooleanConstructor = typeof(GameMenu).GetConstructor(
        [typeof(bool)]
    );

    private static readonly ConstructorInfo? MobileBooleanConstructor = typeof(GameMenu).GetConstructor(
        [typeof(bool), typeof(bool)]
    );

    private static readonly ConstructorInfo? DesktopTabConstructor = typeof(GameMenu).GetConstructor(
        [typeof(int), typeof(int), typeof(bool)]
    );

    private static readonly ConstructorInfo? MobileTabConstructor = typeof(GameMenu).GetConstructor(
        [typeof(int), typeof(int)]
    );

    /*********
    ** Public methods
    *********/
    // Desktop: GameMenu(bool playOpeningSound = true)
    // Mobile: GameMenu(bool standardTabs = true, bool optionsOnly = false)
    // Map: playOpeningSound is ignored, standardTabs = true, optionsOnly = false
    public static GameMenu Constructor(bool playOpeningSound = true)
    {
        if (DesktopBooleanConstructor != null)
            return (GameMenu)DesktopBooleanConstructor.Invoke([playOpeningSound]);
        if (MobileBooleanConstructor != null)
            return (GameMenu)MobileBooleanConstructor.Invoke([true, false]);

        throw new MissingMethodException(
            typeof(GameMenu).FullName,
            ".ctor with desktop or mobile boolean parameters"
        );
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
        if (DesktopTabConstructor != null)
        {
            return (GameMenu)
                DesktopTabConstructor.Invoke([startingTab, extra, playOpeningSound]);
        }
        if (MobileTabConstructor != null)
            return (GameMenu)MobileTabConstructor.Invoke([startingTab, extra]);

        throw new MissingMethodException(
            typeof(GameMenu).FullName,
            ".ctor with desktop or mobile tab parameters"
        );
    }

    /*********
    ** Private methods
    *********/
    private GameMenuMobileFacade()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
