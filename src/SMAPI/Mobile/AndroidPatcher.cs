using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
#if SMAPI_FOR_ANDROID
using Android.Util;
using StardewModdingAPI.Mobile.Patches;
#endif

namespace StardewModdingAPI.Mobile;

/// <summary>
/// Manages Harmony for Android runtime.
/// </summary>
internal static class AndroidPatcher
{
    private const string Tag = "AndroidPatcher";

    public static Harmony? Harmony { get; private set; }

    /// <summary>Log a message to Android logcat or debug output.</summary>
    private static void LogInfo(string message)
    {
#if SMAPI_FOR_ANDROID
        global::Android.Util.Log.Info(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] {message}");
#endif
    }

    /// <summary>Log an error to Android logcat or debug output.</summary>
    private static void LogError(string message)
    {
#if SMAPI_FOR_ANDROID
        global::Android.Util.Log.Error(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] ERROR: {message}");
#endif
    }

    /// <summary>Initialize the Android patcher. Called at program entry point.</summary>
    internal static void Setup()
    {
        LogInfo("Setup starting...");

        try
        {
            Harmony = new Harmony(nameof(AndroidPatcher));

            // report as Linux so PC mods use the correct code paths
            PatchOperatingSystemChecks();

            // prevent Console.ForegroundColor from throwing on Android
            PatchConsoleForegroundColor();

            LogInfo("Setup complete");
        }
        catch (Exception ex)
        {
            LogError($"Setup failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Patch OperatingSystem platform checks for PC compatibility.
    /// Without this, mods detect Android and try to patch mobile-only methods
    /// like IClickableMenu.drawMobileToolTip which don't exist in the PC DLLs.
    /// </summary>
    private static void PatchOperatingSystemChecks()
    {
        var isAndroidMethod = typeof(OperatingSystem).GetMethod(
            "IsAndroid",
            BindingFlags.Public | BindingFlags.Static
        );
        if (isAndroidMethod != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(
                nameof(ReturnFalse_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Harmony!.Patch(isAndroidMethod, prefix: new HarmonyMethod(prefix));
        }

        var isLinuxMethod = typeof(OperatingSystem).GetMethod(
            "IsLinux",
            BindingFlags.Public | BindingFlags.Static
        );
        if (isLinuxMethod != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(
                nameof(ReturnTrue_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Harmony!.Patch(isLinuxMethod, prefix: new HarmonyMethod(prefix));
        }

        var isWindowsMethod = typeof(OperatingSystem).GetMethod(
            "IsWindows",
            BindingFlags.Public | BindingFlags.Static
        );
        if (isWindowsMethod != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(
                nameof(ReturnFalse_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Harmony!.Patch(isWindowsMethod, prefix: new HarmonyMethod(prefix));
        }

        var isMacOSMethod = typeof(OperatingSystem).GetMethod(
            "IsMacOS",
            BindingFlags.Public | BindingFlags.Static
        );
        if (isMacOSMethod != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(
                nameof(ReturnFalse_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Harmony!.Patch(isMacOSMethod, prefix: new HarmonyMethod(prefix));
        }
    }

    /// <summary>
    /// Apply patches needed for mobile Stardew Valley before SCore construction.
    /// GameRunner's constructor calls Game1.InitializeRunner() which tries to
    /// migrate/delete Cinderbox's internal storage. Must be patched out.
    /// </summary>
    internal static void ApplyMobilePatches()
    {
        if (Harmony == null)
        {
            LogError("Cannot apply mobile patches - Harmony not initialized");
            return;
        }

        try
        {
            var game1Type = typeof(StardewValley.Game1);
            var gameRunnerType = typeof(StardewValley.GameRunner);

            // Patch InitializeRunner - set paths, skip storage migration
            var initRunnerMethod = HarmonyLib.AccessTools.Method(game1Type, "InitializeRunner");
            if (initRunnerMethod != null)
            {
                Harmony.Patch(
                    initRunnerMethod,
                    prefix: new HarmonyMethod(
                        typeof(AndroidPatcher),
                        nameof(InitializeRunner_Prefix)
                    )
                );
                LogInfo("Patched Game1.InitializeRunner");
            }

            // Set MainActivity.instance to a stub so game code doesn't NullRef on it.
            // Can't construct it normally (type mismatch), so use GetUninitializedObject.
            var mainActType = game1Type.Assembly.GetType("StardewValley.MainActivity");
            if (mainActType != null)
            {
                var instanceField = mainActType.GetField(
                    "instance",
                    BindingFlags.Public | BindingFlags.Static
                );
                if (instanceField != null && instanceField.GetValue(null) == null)
                {
                    try
                    {
                        var stub =
                            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                                mainActType
                            );
                        instanceField.SetValue(null, stub);
                        LogInfo("Set MainActivity.instance to stub object");
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to create MainActivity stub: {ex.Message}");
                    }
                }
            }

            var gameRunnerUpdate = HarmonyLib.AccessTools.Method(gameRunnerType, "Update");
            if (gameRunnerUpdate != null)
            {
                Harmony.Patch(
                    gameRunnerUpdate,
                    prefix: new HarmonyMethod(
                        typeof(AndroidPatcher),
                        nameof(EnsureGameInstance_Prefix)
                    )
                );
                LogInfo("Patched GameRunner.Update game instance recovery");
            }

            // Replace TitleMenu.createdNewCharacter - mobile version references TutorialManager
            // which crashes the JIT since it can't resolve mobile-only types through SMAPI's context
            var titleMenuType2 = game1Type.Assembly.GetType("StardewValley.Menus.TitleMenu");
            if (titleMenuType2 != null)
            {
                var createdNewChar = HarmonyLib.AccessTools.Method(
                    titleMenuType2,
                    "createdNewCharacter"
                );
                if (createdNewChar != null)
                {
                    Harmony.Patch(
                        createdNewChar,
                        prefix: new HarmonyMethod(
                            typeof(AndroidPatcher),
                            nameof(CreatedNewCharacter_Prefix)
                        )
                    );
                    LogInfo("Patched TitleMenu.createdNewCharacter (skip TutorialManager)");
                }
            }

            // Patch StartupPreferences.loadPreferences - force androidDoneStrorageMigration = true
            var startupPrefsType = game1Type.Assembly.GetType("StardewValley.StartupPreferences");
            var loadPrefsMethod =
                startupPrefsType != null
                    ? HarmonyLib.AccessTools.Method(startupPrefsType, "loadPreferences")
                    : null;
            if (loadPrefsMethod != null)
            {
                Harmony.Patch(
                    loadPrefsMethod,
                    postfix: new HarmonyMethod(
                        typeof(AndroidPatcher),
                        nameof(LoadPreferences_Postfix)
                    )
                );
                LogInfo("Patched StartupPreferences.loadPreferences");
            }

            // Patch MainActivity methods that TitleMenu accesses via null instance.
            // Since we can't set MainActivity.instance (type mismatch), patch the methods
            // to be safe when called on any instance (including null via Harmony).
            var mainActivityType = game1Type.Assembly.GetType("StardewValley.MainActivity");
            if (mainActivityType != null)
            {
                var checkMigration = HarmonyLib.AccessTools.Method(
                    mainActivityType,
                    "CheckStorageMigration"
                );
                if (checkMigration != null)
                {
                    Harmony.Patch(
                        checkMigration,
                        prefix: new HarmonyMethod(
                            typeof(AndroidPatcher),
                            nameof(ReturnFalse_Prefix)
                        )
                    );
                    LogInfo("Patched MainActivity.CheckStorageMigration (no-op)");
                }

                var isDoingMigration = HarmonyLib.AccessTools.PropertyGetter(
                    mainActivityType,
                    "IsDoingStorageMigration"
                );
                if (isDoingMigration != null)
                {
                    Harmony.Patch(
                        isDoingMigration,
                        prefix: new HarmonyMethod(
                            typeof(AndroidPatcher),
                            nameof(ReturnFalse_Prefix)
                        )
                    );
                    LogInfo("Patched MainActivity.IsDoingStorageMigration (always false)");
                }

                var hasPermissions = HarmonyLib.AccessTools.PropertyGetter(
                    mainActivityType,
                    "HasPermissions"
                );
                if (hasPermissions != null)
                {
                    Harmony.Patch(
                        hasPermissions,
                        prefix: new HarmonyMethod(typeof(AndroidPatcher), nameof(ReturnTrue_Prefix))
                    );
                    LogInfo("Patched MainActivity.HasPermissions (always true)");
                }

                // PromptForPermissionsIfNecessary - invoke callback immediately (we already have permissions)
                var promptMethod = HarmonyLib.AccessTools.Method(
                    mainActivityType,
                    "PromptForPermissionsIfNecessary"
                );
                if (promptMethod != null)
                {
                    Harmony.Patch(
                        promptMethod,
                        prefix: new HarmonyMethod(
                            typeof(AndroidPatcher),
                            nameof(PromptForPermissions_Prefix)
                        )
                    );
                    LogInfo(
                        "Patched MainActivity.PromptForPermissionsIfNecessary (invoke callback)"
                    );
                }

                // Patch remaining methods that crash on the stub object
                foreach (
                    var methodName in new[]
                    {
                        "LogPermissions",
                        "ShowDiskFullDialogue",
                        "SetImmersive",
                        "DoLicenseCheck",
                    }
                )
                {
                    var method = HarmonyLib.AccessTools.Method(mainActivityType, methodName);
                    if (method != null)
                    {
                        Harmony.Patch(
                            method,
                            prefix: new HarmonyMethod(
                                typeof(AndroidPatcher),
                                nameof(SkipMethod_Prefix)
                            )
                        );
                        LogInfo($"Patched MainActivity.{methodName} (no-op)");
                    }
                }

                var getBuild = HarmonyLib.AccessTools.Method(mainActivityType, "GetBuild");
                if (getBuild != null)
                {
                    Harmony.Patch(
                        getBuild,
                        prefix: new HarmonyMethod(typeof(AndroidPatcher), nameof(GetBuild_Prefix))
                    );
                    LogInfo("Patched MainActivity.GetBuild (returns 0)");
                }
            }

            // Patch LoadGameMenu.startListPopulation - ensure savesPath is set before save scan
            var loadGameMenuType = game1Type.Assembly.GetType("StardewValley.Menus.LoadGameMenu");
            var startListMethod =
                loadGameMenuType != null
                    ? HarmonyLib.AccessTools.Method(loadGameMenuType, "startListPopulation")
                    : null;

            if (startListMethod != null)
            {
                Harmony.Patch(
                    startListMethod,
                    prefix: new HarmonyMethod(typeof(AndroidPatcher), nameof(FindSaveGames_Prefix))
                );
                LogInfo("Patched LoadGameMenu.startListPopulation");
            }
            else
                LogError("LoadGameMenu.startListPopulation not found");

            // MenuWithInventory - implement desktop behavior for missing members
            Patches.MenuWithInventoryMobilePatches.Apply(Harmony);
            LogInfo("Applied MenuWithInventory mobile patches");

            // ShopMenu tabs - support Filter delegates on tab buttons
            Patches.ShopMenuTabMobilePatches.Apply(Harmony);
            LogInfo("Applied ShopMenu tab patches");

            // CraftingPage - populate heldItem with crafted item
            Patches.CraftingPageMobilePatches.Apply(Harmony);
            LogInfo("Applied CraftingPage mobile patches");
        }
        catch (Exception ex)
        {
            LogError($"Failed to apply mobile patches: {ex}");
        }
    }

    private static void FindSaveGames_Prefix()
    {
        // Ensure Game1.savesPath points to our external saves directory
        // before the load menu scans for saves
        try
        {
            var gameAssembly = AppDomain
                .CurrentDomain.GetAssemblies()
                .FirstOrDefault(a =>
                    a.GetName().Name == "StardewValley" || a.GetName().Name == "Stardew Valley"
                );
            var game1Type = gameAssembly?.GetType("StardewValley.Game1");
            var savesField = game1Type?.GetField(
                "savesPath",
                BindingFlags.Public | BindingFlags.Static
            );

            if (savesField != null)
            {
                var currentValue = savesField.GetValue(null) as string;
                var expectedValue = AndroidPaths.Saves;

                if (currentValue != expectedValue)
                {
                    savesField.SetValue(null, expectedValue);
                    LogInfo(
                        $"FindSaveGames_Prefix: fixed savesPath from '{currentValue}' to '{expectedValue}'"
                    );
                }
            }
            else
            {
                LogError("FindSaveGames_Prefix: Game1.savesPath field not found");
            }
        }
        catch (Exception ex)
        {
            LogError($"FindSaveGames_Prefix failed: {ex.Message}");
        }
    }

    private static bool CreatedNewCharacter_Prefix(object __instance, bool skipIntro)
    {
        try
        {
            LogInfo("CreatedNewCharacter_Prefix: running replacement");
            // Fire the event (SMAPI hooks into this)
            var titleMenuType = __instance.GetType();
            var onCreatedEvent = titleMenuType.GetField(
                "OnCreatedNewCharacter",
                BindingFlags.Public | BindingFlags.Static
            );
            var eventDelegate = onCreatedEvent?.GetValue(null) as Action;
            eventDelegate?.Invoke();

            StardewValley.Game1.playSound("smallSelect");

            // Set TitleMenu fields
            var subMenuField = titleMenuType.GetField(
                "subMenu",
                BindingFlags.Public | BindingFlags.Instance
            );
            subMenuField?.SetValue(__instance, null);

            var transField = titleMenuType.GetField(
                "transitioningCharacterCreationMenu",
                BindingFlags.Public | BindingFlags.Instance
            );
            transField?.SetValue(__instance, true);

            if (skipIntro)
            {
                StardewValley.Game1.game1.loadForNewGame();
                StardewValley.Game1.saveOnNewDay = true;
                StardewValley.Game1.player.eventsSeen.Add("60367");
                StardewValley.Game1.player.currentLocation = StardewValley.Utility.getHomeOfFarmer(
                    StardewValley.Game1.player
                );
                StardewValley.Game1.player.Position =
                    new Microsoft.Xna.Framework.Vector2(9f, 9f) * 64f;
                StardewValley.Game1.player.isInBed.Value = true;
                StardewValley.Game1.NewDay(0f);
                StardewValley.Game1.exitActiveMenu();
                StardewValley.Game1.setGameMode(3);

                // Skip TutorialManager.Instance.completeTutorial(TutorialType.DUMMY_PAST_INTRO)
                // - mobile-only type that crashes JIT when resolved through SMAPI's context.
                // Call via reflection instead.
                try
                {
                    var tutMgrType = typeof(StardewValley.Game1).Assembly.GetType(
                        "StardewValley.Menus.TutorialManager"
                    );
                    var instanceProp = tutMgrType?.GetProperty(
                        "Instance",
                        BindingFlags.Public | BindingFlags.Static
                    );
                    var completeTutorial = tutMgrType?.GetMethod(
                        "completeTutorial",
                        BindingFlags.Public | BindingFlags.Instance
                    );
                    var tutTypeEnum = typeof(StardewValley.Game1).Assembly.GetType(
                        "StardewValley.Menus.TutorialType"
                    );
                    if (instanceProp != null && completeTutorial != null && tutTypeEnum != null)
                    {
                        var instance = instanceProp.GetValue(null);
                        var dummyPastIntro = Enum.Parse(tutTypeEnum, "DUMMY_PAST_INTRO");
                        completeTutorial.Invoke(instance, new[] { dummyPastIntro });
                    }
                }
                catch (Exception ex)
                {
                    LogError($"TutorialManager reflection failed (non-fatal): {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"CreatedNewCharacter_Prefix failed: {ex}");
        }

        return false; // skip original (prevents JIT crash)
    }

    private static bool SkipMethod_Prefix() => false;

    private static bool PromptForPermissions_Prefix(Action callback)
    {
        callback?.Invoke();
        return false;
    }

    private static bool GetBuild_Prefix(ref int __result)
    {
        __result = 0;
        return false;
    }

    private static void LoadPreferences_Postfix(object __instance)
    {
        try
        {
            var field = __instance
                .GetType()
                .GetField(
                    "androidDoneStrorageMigration",
                    BindingFlags.Public | BindingFlags.Instance
                );
            if (field != null)
            {
                field.SetValue(__instance, true);
                LogInfo("Set StartupPreferences.androidDoneStrorageMigration = true");
            }
        }
        catch (Exception ex)
        {
            LogError($"LoadPreferences_Postfix failed: {ex.Message}");
        }
    }

    private static void EnsureGameInstance_Prefix()
    {
        if (StardewValley.Game1.game1 != null || StardewValley.GameRunner.instance == null)
            return;

        var instances = StardewValley.GameRunner.instance.gameInstances;
        if (instances.Count > 0)
            StardewValley.Game1.game1 = instances[0];
    }

    private static bool InitializeRunner_Prefix()
    {
        // Set the paths that InitializeRunner would set, but skip the storage migration
        // that tries to delete Cinderbox's data directory.
        try
        {
            var savesDir = System.IO.Path.Combine(AndroidPaths.Saves);
            var platformRoot = System.IO.Path.Combine(
                AndroidPaths.ExternalRoot,
                AndroidPaths.IsMobile ? "mobile" : "desktop"
            );
            var screenshotsDir = System.IO.Path.Combine(platformRoot, "Screenshots");

            System.IO.Directory.CreateDirectory(savesDir);
            try
            {
                System.IO.Directory.CreateDirectory(screenshotsDir);
            }
            catch { }

            // Use the runtime game assembly to find mobile-only fields
            // (typeof(Game1) may resolve to desktop type which lacks these fields)
            var gameAssembly = AppDomain
                .CurrentDomain.GetAssemblies()
                .FirstOrDefault(a =>
                    a.GetName().Name == "StardewValley" || a.GetName().Name == "Stardew Valley"
                );
            var game1Type =
                gameAssembly?.GetType("StardewValley.Game1") ?? typeof(StardewValley.Game1);

            var savesField = game1Type.GetField(
                "savesPath",
                BindingFlags.Public | BindingFlags.Static
            );
            if (savesField != null)
            {
                savesField.SetValue(null, savesDir);
                LogInfo($"Set Game1.savesPath = {savesField.GetValue(null)}");
            }
            else
                LogError("Game1.savesPath field NOT FOUND");

            var hiddenField = game1Type.GetField(
                "hiddenSavesPath",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic
            );
            if (hiddenField != null)
            {
                hiddenField.SetValue(null, savesDir);
                LogInfo($"Set Game1.hiddenSavesPath = {hiddenField.GetValue(null)}");
            }
            else
                LogError("Game1.hiddenSavesPath field NOT FOUND");

            var screenshotsField = game1Type.GetField(
                "screenshotsPath",
                BindingFlags.Public | BindingFlags.Static
            );
            if (screenshotsField != null)
            {
                screenshotsField.SetValue(null, screenshotsDir);
                LogInfo($"Set Game1.screenshotsPath = {screenshotsField.GetValue(null)}");
            }
            else
                LogError("Game1.screenshotsPath field NOT FOUND");
        }
        catch (Exception ex)
        {
            LogError($"InitializeRunner replacement failed: {ex.Message}");
        }

        return false; // skip original (storage migration)
    }

    internal static void ApplyGameInitializedPatches()
    {
        if (Harmony == null)
        {
            LogError("Cannot apply performance patches - Harmony not initialized");
            return;
        }

        try
        {
#if SMAPI_FOR_ANDROID
            ParallelAudioLoadPatch.Apply(Harmony);
#endif
        }
        catch (Exception ex)
        {
            LogError($"Error applying performance patches: {ex}");
        }
    }

    /// <summary>
    /// Patch Console.ForegroundColor getter/setter to no-op on Android.
    /// The getter returns ConsoleColor.Gray; the setter does nothing.
    /// </summary>
    private static void PatchConsoleForegroundColor()
    {
        var consoleType = typeof(Console);

        var getter = consoleType
            .GetProperty("ForegroundColor", BindingFlags.Public | BindingFlags.Static)
            ?.GetGetMethod();
        if (getter != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(
                nameof(ConsoleForegroundColor_Get_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Harmony!.Patch(getter, prefix: new HarmonyMethod(prefix));
        }

        var setter = consoleType
            .GetProperty("ForegroundColor", BindingFlags.Public | BindingFlags.Static)
            ?.GetSetMethod();
        if (setter != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(
                nameof(ReturnFalse_VoidPrefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Harmony!.Patch(setter, prefix: new HarmonyMethod(prefix));
        }
    }

    /// <summary>Prefix for Console.ForegroundColor getter - return ConsoleColor.Gray.</summary>
    private static bool ConsoleForegroundColor_Get_Prefix(ref ConsoleColor __result)
    {
        __result = ConsoleColor.Gray;
        return false;
    }

    /// <summary>Prefix that skips original void method.</summary>
    private static bool ReturnFalse_VoidPrefix() => false;

    /// <summary>Prefix that returns false and skips original method.</summary>
    private static bool ReturnFalse_Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    /// <summary>Prefix that returns true and skips original method.</summary>
    private static bool ReturnTrue_Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}
