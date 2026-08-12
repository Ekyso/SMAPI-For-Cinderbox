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

            // Trace patches - wrap key init methods to catch JIT/runtime errors
            var methods = new[]
            {
                HarmonyLib.AccessTools.Method(gameRunnerType, "Initialize"),
                HarmonyLib.AccessTools.Method(gameRunnerType, "InitializeMainInstance"),
                HarmonyLib.AccessTools.Method(gameRunnerType, "AddGameInstance"),
                HarmonyLib.AccessTools.Method(gameRunnerType, "Update"),
                HarmonyLib.AccessTools.Method(gameRunnerType, "Draw"),
                HarmonyLib.AccessTools.Method(gameRunnerType, "LoadContent"),
                HarmonyLib.AccessTools.Method(game1Type, "Initialize"),
                HarmonyLib.AccessTools.Method(game1Type, "InitializeSounds"),
                HarmonyLib.AccessTools.Method(game1Type, "Update"),
                HarmonyLib.AccessTools.Method(game1Type, "_update"),
                HarmonyLib.AccessTools.Method(game1Type, "_draw"),
                HarmonyLib.AccessTools.Method(game1Type, "loadForNewGame"),
                HarmonyLib.AccessTools.Method(game1Type, "UpdateTitleScreen"),
                HarmonyLib.AccessTools.Method(game1Type, "setGameMode"),
                HarmonyLib.AccessTools.Method(game1Type, "AfterLoadContent"),
                HarmonyLib.AccessTools.Method(game1Type, "AddLocations"),
                HarmonyLib.AccessTools.Method(game1Type, "ResetToolSpriteSheet"),
                HarmonyLib.AccessTools.Method(game1Type, "updateActiveMenu"),
                HarmonyLib.AccessTools.Method(game1Type, "refreshWindowSettings"),
                HarmonyLib.AccessTools.Method(game1Type, "AddNPCs"),
                HarmonyLib.AccessTools.Method(
                    typeof(StardewValley.GameLocation),
                    "AddDefaultBuildings"
                ),
                HarmonyLib.AccessTools.Method(game1Type, "setGraphicsForSeason"),
            };

            foreach (var method in methods)
            {
                if (method == null)
                    continue;
                Harmony.Patch(
                    method,
                    prefix: new HarmonyMethod(typeof(AndroidPatcher), nameof(TraceMethod_Prefix)),
                    finalizer: new HarmonyMethod(
                        typeof(AndroidPatcher),
                        nameof(TraceMethod_Finalizer)
                    )
                );
                LogInfo($"Trace patch: {method.DeclaringType?.Name}.{method.Name}");
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

            var farmChooserType = game1Type.Assembly.GetType(
                "StardewValley.Menus.MobileFarmChooser"
            );
            if (farmChooserType != null)
            {
                var optBtnClick = HarmonyLib.AccessTools.Method(
                    farmChooserType,
                    "optionButtonClick"
                );
                if (optBtnClick != null)
                {
                    Harmony.Patch(
                        optBtnClick,
                        prefix: new HarmonyMethod(
                            typeof(AndroidPatcher),
                            nameof(TraceMethod_Prefix)
                        ),
                        finalizer: new HarmonyMethod(
                            typeof(AndroidPatcher),
                            nameof(TraceMethod_Finalizer)
                        )
                    );
                    LogInfo("Trace patch: MobileFarmChooser.optionButtonClick");
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

            // Patch TitleMenu constructor to diagnose NullRef
            var titleMenuType = game1Type.Assembly.GetType("StardewValley.Menus.TitleMenu");
            var titleMenuCtor =
                titleMenuType != null ? HarmonyLib.AccessTools.Constructor(titleMenuType) : null;
            if (titleMenuCtor != null)
            {
                Harmony.Patch(
                    titleMenuCtor,
                    prefix: new HarmonyMethod(typeof(AndroidPatcher), nameof(TitleMenu_Prefix))
                );
                LogInfo("Diagnostic patch: TitleMenu constructor");
            }

            // Dedicated field-level diagnostic prefix for GameRunner.Update
            var gameRunnerUpdate = HarmonyLib.AccessTools.Method(gameRunnerType, "Update");
            if (gameRunnerUpdate != null)
            {
                Harmony.Patch(
                    gameRunnerUpdate,
                    prefix: new HarmonyMethod(
                        typeof(AndroidPatcher),
                        nameof(GameRunnerUpdate_Prefix)
                    )
                );
                LogInfo("Diagnostic patch: GameRunner.Update field-state logger");
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

    private static void TitleMenu_Prefix()
    {
#if SMAPI_FOR_ANDROID
        const string tag = "TitleMenuDebug";
        try
        {
            global::Android.Util.Log.Info(tag, "=== TitleMenu..ctor() about to run ===");

            // ---------------------------------------------------------------
            // 1. FIELD INITIALIZER: Game1.content (line 84 does Game1.content.CreateTemporary())
            //    This runs BEFORE the constructor body, so if content is null, we crash with
            //    no line number.
            // ---------------------------------------------------------------
            var content = StardewValley.Game1.content;
            global::Android.Util.Log.Info(tag, $"  Game1.content is null: {content == null}");
            if (content != null)
            {
                global::Android.Util.Log.Info(
                    tag,
                    $"  Game1.content type: {content.GetType().FullName}"
                );
                global::Android.Util.Log.Info(
                    tag,
                    $"  Game1.content.RootDirectory: {content.RootDirectory}"
                );
                global::Android.Util.Log.Info(
                    tag,
                    $"  Game1.content.ServiceProvider is null: {content.ServiceProvider == null}"
                );

                // Actually test CreateTemporary() - this is what line 84 calls
                try
                {
                    var temp = content.CreateTemporary();
                    global::Android.Util.Log.Info(
                        tag,
                        $"  content.CreateTemporary() succeeded: {temp != null}"
                    );
                    if (temp != null)
                    {
                        global::Android.Util.Log.Info(
                            tag,
                            $"  temp.RootDirectory: {temp.RootDirectory}"
                        );
                        temp.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Error(tag, $"  content.CreateTemporary() THREW: {ex}");
                }
            }

            // ---------------------------------------------------------------
            // 2. BASE CONSTRUCTOR: base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height)
            //    uiViewport is a value type (xTile.Dimensions.Rectangle) so can't be null,
            //    but Width/Height could be 0 which would be suspicious.
            // ---------------------------------------------------------------
            var uiVp = StardewValley.Game1.uiViewport;
            global::Android.Util.Log.Info(
                tag,
                $"  Game1.uiViewport: Width={uiVp.Width}, Height={uiVp.Height}, X={uiVp.X}, Y={uiVp.Y}"
            );

            var vp = StardewValley.Game1.viewport;
            global::Android.Util.Log.Info(
                tag,
                $"  Game1.viewport: Width={vp.Width}, Height={vp.Height}, X={vp.X}, Y={vp.Y}"
            );

            // ---------------------------------------------------------------
            // 3. CONSTRUCTOR BODY line 274-275: menuContent.Load<Texture2D>(...)
            //    If field initializer succeeded, menuContent should be non-null.
            //    But let's verify textures can load.
            // ---------------------------------------------------------------
            // (menuContent is an instance field - can't check from a static prefix,
            //  but the field initializer test above covers whether it would succeed)

            // ---------------------------------------------------------------
            // 4. CONSTRUCTOR BODY line 276: Program.sdk.IsJapaneseRegionRelease
            // ---------------------------------------------------------------
            var game1Type = typeof(StardewValley.Game1);
            var programType = game1Type.Assembly.GetType("StardewValley.Program");
            global::Android.Util.Log.Info(tag, $"  Program type found: {programType != null}");

            if (programType != null)
            {
                // Try field first (some versions use a field), then property
                var sdkField = programType.GetField(
                    "sdk",
                    System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Static
                );
                var sdkProp = programType.GetProperty(
                    "sdk",
                    System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Static
                );

                global::Android.Util.Log.Info(
                    tag,
                    $"  Program.sdk field exists: {sdkField != null}"
                );
                global::Android.Util.Log.Info(
                    tag,
                    $"  Program.sdk property exists: {sdkProp != null}"
                );

                object? sdkValue = null;
                try
                {
                    if (sdkField != null)
                        sdkValue = sdkField.GetValue(null);
                    else if (sdkProp != null)
                        sdkValue = sdkProp.GetValue(null);

                    global::Android.Util.Log.Info(
                        tag,
                        $"  Program.sdk is null: {sdkValue == null}"
                    );
                    if (sdkValue != null)
                    {
                        global::Android.Util.Log.Info(
                            tag,
                            $"  Program.sdk type: {sdkValue.GetType().FullName}"
                        );

                        // Check IsJapaneseRegionRelease
                        try
                        {
                            var jpProp = sdkValue.GetType().GetProperty("IsJapaneseRegionRelease");
                            if (jpProp != null)
                            {
                                var jpVal = jpProp.GetValue(sdkValue);
                                global::Android.Util.Log.Info(
                                    tag,
                                    $"  Program.sdk.IsJapaneseRegionRelease: {jpVal}"
                                );
                            }
                            else
                            {
                                global::Android.Util.Log.Warn(
                                    tag,
                                    "  Program.sdk.IsJapaneseRegionRelease property NOT FOUND"
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            global::Android.Util.Log.Error(
                                tag,
                                $"  Program.sdk.IsJapaneseRegionRelease THREW: {ex.Message}"
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Error(tag, $"  Program.sdk access THREW: {ex}");
                }
            }

            // ---------------------------------------------------------------
            // 5. CONSTRUCTOR BODY line 318: Game1.UnlockedMultiplayer
            //    Getter reads a static bool field - should be safe, but verify.
            // ---------------------------------------------------------------
            try
            {
                var prop = game1Type.GetProperty(
                    "UnlockedMultiplayer",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                );
                if (prop != null)
                    global::Android.Util.Log.Info(
                        tag,
                        $"  Game1.UnlockedMultiplayer: {prop.GetValue(null)}"
                    );
                else
                    global::Android.Util.Log.Warn(
                        tag,
                        "  Game1.UnlockedMultiplayer: property not found (desktop DLL)"
                    );
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error(tag, $"  Game1.UnlockedMultiplayer THREW: {ex}");
            }

            // ---------------------------------------------------------------
            // 6. CONSTRUCTOR BODY line 330: Game1.random.NextBool()
            // ---------------------------------------------------------------
            var random = StardewValley.Game1.random;
            global::Android.Util.Log.Info(tag, $"  Game1.random is null: {random == null}");

            // ---------------------------------------------------------------
            // 7. CONSTRUCTOR BODY line 306: Game1.viewport.Height (value type, safe but log it)
            // ---------------------------------------------------------------
            // Already logged above

            // ---------------------------------------------------------------
            // 8. CONSTRUCTOR BODY line 393-396: MainActivity.instance
            //    startupPreferences.androidDoneStrorageMigration check calls
            //    MainActivity.instance.CheckStorageMigration()
            // ---------------------------------------------------------------
            var mainActivityType = game1Type.Assembly.GetType("StardewValley.MainActivity");
            global::Android.Util.Log.Info(
                tag,
                $"  MainActivity type found: {mainActivityType != null}"
            );
            if (mainActivityType != null)
            {
                var instanceField = mainActivityType.GetField(
                    "instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                );
                global::Android.Util.Log.Info(
                    tag,
                    $"  MainActivity.instance field exists: {instanceField != null}"
                );
                if (instanceField != null)
                {
                    var instanceVal = instanceField.GetValue(null);
                    global::Android.Util.Log.Info(
                        tag,
                        $"  MainActivity.instance is null: {instanceVal == null}"
                    );
                }
            }

            // ---------------------------------------------------------------
            // 9. CONSTRUCTOR BODY line 402: Game1.setRichPresence("menus")
            //    May touch Game1.game1 or other fields internally.
            // ---------------------------------------------------------------
            global::Android.Util.Log.Info(
                tag,
                $"  Game1.game1 is null: {StardewValley.Game1.game1 == null}"
            );

            // ---------------------------------------------------------------
            // 10. OTHER FIELDS that could cause issues
            // ---------------------------------------------------------------
            global::Android.Util.Log.Info(
                tag,
                $"  Game1.spriteBatch is null: {StardewValley.Game1.spriteBatch == null}"
            );
            global::Android.Util.Log.Info(
                tag,
                $"  Game1.graphics is null: {StardewValley.Game1.graphics == null}"
            );

            if (StardewValley.Game1.graphics != null)
            {
                global::Android.Util.Log.Info(
                    tag,
                    $"  Game1.graphics.GraphicsDevice is null: {StardewValley.Game1.graphics.GraphicsDevice == null}"
                );
                if (StardewValley.Game1.graphics.GraphicsDevice != null)
                {
                    var gd = StardewValley.Game1.graphics.GraphicsDevice;
                    global::Android.Util.Log.Info(
                        tag,
                        $"  GraphicsDevice.Viewport: {gd.Viewport.Width}x{gd.Viewport.Height}"
                    );
                }
            }

            global::Android.Util.Log.Info(
                tag,
                $"  Game1.options is null: {StardewValley.Game1.options == null}"
            );
            if (StardewValley.Game1.options != null)
            {
                global::Android.Util.Log.Info(
                    tag,
                    $"  Game1.options.gamepadControls: {StardewValley.Game1.options.gamepadControls}"
                );
                global::Android.Util.Log.Info(
                    tag,
                    $"  Game1.options.snappyMenus: {StardewValley.Game1.options.snappyMenus}"
                );
            }

            global::Android.Util.Log.Info(
                tag,
                $"  Game1.soundBank is null: {StardewValley.Game1.soundBank == null}"
            );
            var vjField2 = game1Type.GetField(
                "virtualJoypad",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            );
            global::Android.Util.Log.Info(
                tag,
                $"  Game1.virtualJoypad is null: {vjField2?.GetValue(null) == null}"
            );
            global::Android.Util.Log.Info(
                tag,
                $"  Game1.activeClickableMenu is null: {StardewValley.Game1.activeClickableMenu == null}"
            );
            global::Android.Util.Log.Info(
                tag,
                $"  Game1.multiplayer is null: {StardewValley.Game1.multiplayer == null}"
            );
            global::Android.Util.Log.Info(
                tag,
                $"  Game1.input is null: {StardewValley.Game1.input == null}"
            );
            global::Android.Util.Log.Info(
                tag,
                $"  Game1.hooks is null: {StardewValley.Game1.hooks == null}"
            );

            // ---------------------------------------------------------------
            // 11. StartupPreferences constructor - called at line 375
            //     new StartupPreferences() might rely on Android context
            // ---------------------------------------------------------------
            try
            {
                var sp = new StardewValley.StartupPreferences();
                global::Android.Util.Log.Info(tag, $"  new StartupPreferences() succeeded");
                // Don't dispose - just testing construction
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error(tag, $"  new StartupPreferences() THREW: {ex}");
            }

            // ---------------------------------------------------------------
            // 12. LocalizedContentManager.OnLanguageChange event (line 273)
            //     Accessing the event should be safe, but log it.
            // ---------------------------------------------------------------
            global::Android.Util.Log.Info(
                tag,
                "  LocalizedContentManager.OnLanguageChange: checking access..."
            );
            try
            {
                // Just verify the type is accessible
                var lcmType = typeof(StardewValley.LocalizedContentManager);
                var langChangeEvent = lcmType.GetEvent(
                    "OnLanguageChange",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                );
                global::Android.Util.Log.Info(
                    tag,
                    $"  OnLanguageChange event found: {langChangeEvent != null}"
                );
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error(
                    tag,
                    $"  OnLanguageChange check THREW: {ex.Message}"
                );
            }

            global::Android.Util.Log.Info(tag, "=== TitleMenu_Prefix diagnostics complete ===");
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error(tag, $"TitleMenu_Prefix itself threw: {ex}");
        }
#endif
    }

    private static void TraceMethod_Prefix(MethodBase __originalMethod)
    {
        LogInfo($">>> ENTER {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");

        // Ensure game1 is set before Update runs - mobile AddGameInstance sets it to null at end
        if (
            __originalMethod.Name == "Update"
            && __originalMethod.DeclaringType == typeof(StardewValley.GameRunner)
        )
        {
            if (StardewValley.Game1.game1 == null && StardewValley.GameRunner.instance != null)
            {
                var instances = StardewValley.GameRunner.instance.gameInstances;
                if (instances.Count > 0)
                {
                    StardewValley.Game1.game1 = instances[0];
                    LogInfo("Fixed null Game1.game1 before Update");
                }
            }
        }
    }

    private static void TraceMethod_Finalizer(MethodBase __originalMethod, Exception? __exception)
    {
        if (__exception != null)
            LogError(
                $"<<< EXCEPTION in {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}: {__exception}"
            );
        else
            LogInfo($"<<< EXIT {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
    }

    /// <summary>
    /// Dedicated diagnostic prefix for GameRunner.Update() - logs the null-state of every
    /// field the method touches so we can identify the exact source of the SIGSEGV.
    /// Runs in addition to the generic TraceMethod_Prefix.
    /// </summary>
    private static void GameRunnerUpdate_Prefix()
    {
        const string tag = "MobileDebug";
        try
        {
            var game1 = StardewValley.Game1.game1;
            var runnerInstance = StardewValley.GameRunner.instance;

#if SMAPI_FOR_ANDROID
            Android.Util.Log.Info(tag, $"GameRunner.Update diagnostics ---");
            Android.Util.Log.Info(tag, $"  Game1.game1 is null: {game1 == null}");
            Android.Util.Log.Info(tag, $"  GameRunner.instance is null: {runnerInstance == null}");

            if (runnerInstance != null)
            {
                var instances = runnerInstance.gameInstances;
                Android.Util.Log.Info(
                    tag,
                    $"  GameRunner.instance.gameInstances is null: {instances == null}"
                );
                if (instances != null)
                    Android.Util.Log.Info(
                        tag,
                        $"  GameRunner.instance.gameInstances.Count: {instances.Count}"
                    );
            }
            else
            {
                Android.Util.Log.Info(
                    tag,
                    "  GameRunner.instance.gameInstances: SKIPPED (instance null)"
                );
            }

            // Game1.options accesses game1.instanceOptions - check without triggering NRE
            var instanceOptions = game1?.instanceOptions;
            Android.Util.Log.Info(
                tag,
                $"  Game1.game1?.instanceOptions is null: {instanceOptions == null}"
            );
            if (instanceOptions != null)
                Android.Util.Log.Info(
                    tag,
                    $"  instanceOptions.gamepadMode: {instanceOptions.gamepadMode}"
                );

            Android.Util.Log.Info(
                tag,
                $"  Game1.debugTimings is null: {StardewValley.Game1.debugTimings == null}"
            );
            Android.Util.Log.Info(
                tag,
                $"  Game1.content is null: {StardewValley.Game1.content == null}"
            );

            var graphics = StardewValley.Game1.graphics;
            Android.Util.Log.Info(tag, $"  Game1.graphics is null: {graphics == null}");
            if (graphics != null)
                Android.Util.Log.Info(
                    tag,
                    $"  Game1.graphics.GraphicsDevice is null: {graphics.GraphicsDevice == null}"
                );

            Android.Util.Log.Info(tag, $"  Game1.gameMode: {StardewValley.Game1.gameMode}");
            Android.Util.Log.Info(
                tag,
                $"  Game1.currentLocation is null: {StardewValley.Game1.currentLocation == null}"
            );
            Android.Util.Log.Info(
                tag,
                $"  Game1.activeClickableMenu is null: {StardewValley.Game1.activeClickableMenu == null}"
            );
            Android.Util.Log.Info(
                tag,
                $"  Game1.spriteBatch is null: {StardewValley.Game1.spriteBatch == null}"
            );
            Android.Util.Log.Info(
                tag,
                $"  Game1.input is null: {StardewValley.Game1.input == null}"
            );
            Android.Util.Log.Info(
                tag,
                $"  Game1.multiplayer is null: {StardewValley.Game1.multiplayer == null}"
            );
            Android.Util.Log.Info(
                tag,
                $"  Game1.hooks is null: {StardewValley.Game1.hooks == null}"
            );

            // Check mobile-specific fields via reflection
            var game1Type = typeof(StardewValley.Game1);
            var vjField = game1Type.GetField(
                "virtualJoypad",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            );
            Android.Util.Log.Info(
                tag,
                $"  Game1.virtualJoypad is null: {vjField?.GetValue(null) == null}"
            );
            var randomField = game1Type.GetField(
                "random",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            );
            Android.Util.Log.Info(
                tag,
                $"  Game1.random is null: {randomField?.GetValue(null) == null}"
            );
#else
            System.Diagnostics.Debug.WriteLine($"[{tag}] GameRunner.Update diagnostics ---");
            System.Diagnostics.Debug.WriteLine($"[{tag}]   Game1.game1 is null: {game1 == null}");
            System.Diagnostics.Debug.WriteLine(
                $"[{tag}]   GameRunner.instance is null: {runnerInstance == null}"
            );
#endif
        }
        catch (Exception ex)
        {
#if SMAPI_FOR_ANDROID
            Android.Util.Log.Error(tag, $"GameRunnerUpdate_Prefix itself threw: {ex}");
#else
            System.Diagnostics.Debug.WriteLine(
                $"[{tag}] GameRunnerUpdate_Prefix itself threw: {ex}"
            );
#endif
        }
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
