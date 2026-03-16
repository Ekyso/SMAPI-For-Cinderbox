#if SMAPI_FOR_ANDROID
using System;
using System.IO;
using System.Reflection;
using Android.App;
using Android.Content;
using Android.Provider;
using Android.Util;
using HarmonyLib;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Fixes DailyScreenshot mod on Android. Redirects the screenshot directory to the correct
/// path and replaces the Process.Start folder-open with Android's file manager intent.
/// </summary>
internal static class DailyScreenshotPatch
{
    private const string Tag = "DailyScreenshotPatch";
    private static DirectoryInfo _cachedDir;
    private static string _cachedPath;

    public static void Apply(Harmony harmony)
    {
        try
        {
            Assembly assembly = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "DailyScreenshot")
                {
                    assembly = asm;
                    break;
                }
            }

            if (assembly == null)
            {
                Log.Info(Tag, "DailyScreenshot not found, skipping patches");
                return;
            }

            var modEntryType = assembly.GetType("DailyScreenshot.ModEntry");
            if (modEntryType == null)
            {
                Log.Error(Tag, "ModEntry type not found");
                return;
            }

            var getter = modEntryType.GetProperty("DefaultSSdirectory",
                BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod();
            if (getter != null)
            {
                harmony.Patch(getter,
                    postfix: new HarmonyMethod(typeof(DailyScreenshotPatch), nameof(DefaultSSdirectory_Postfix)));
                Log.Info(Tag, "Patched DefaultSSdirectory getter");
            }
            else
                Log.Error(Tag, "DefaultSSdirectory getter not found");

            var onMenuChanged = modEntryType.GetMethod("OnMenuChanged",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (onMenuChanged != null)
            {
                harmony.Patch(onMenuChanged,
                    prefix: new HarmonyMethod(typeof(DailyScreenshotPatch), nameof(OnMenuChanged_Prefix)));
                Log.Info(Tag, "Patched OnMenuChanged (Show config.json button)");
            }
            else
                Log.Warn(Tag, "OnMenuChanged not found");

            Log.Info(Tag, "All DailyScreenshot patches applied");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to apply patches: {ex.Message}");
            Log.Error(Tag, ex.StackTrace ?? "");
        }
    }

    /// <summary>
    /// Postfix: override DefaultSSdirectory to use our patched Game1.GetScreenshotFolder() path.
    /// </summary>
    private static void DefaultSSdirectory_Postfix(ref DirectoryInfo __result)
    {
        var correctPath = Game1.game1?.GetScreenshotFolder();
        if (correctPath == null)
            return;

        if (_cachedPath != correctPath)
        {
            _cachedDir = new DirectoryInfo(correctPath);
            _cachedPath = correctPath;
        }

        __result = _cachedDir;
    }

    /// <summary>Replaces OnMenuChanged to open the mod folder via Android file manager instead of Process.Start.</summary>
    private static bool OnMenuChanged_Prefix(object __instance, object sender, MenuChangedEventArgs e)
    {
        try
        {
            if (e.NewMenu is not GameMenu gameMenu)
                return false;

            if (gameMenu.pages[GameMenu.optionsTab] is not OptionsPage optionsPage)
                return false;

            string modDir = (__instance as IMod)?.Helper?.DirectoryPath;

            optionsPage.options.Add(new OptionsElement("DailyScreenshot Mod:"));
            optionsPage.options.Add(new OptionsButton("Show config.json", () =>
            {
                if (modDir != null)
                    OpenFolderOnAndroid(modDir);
            }));
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"OnMenuChanged_Prefix failed: {ex.Message}");
        }

        return false;
    }

    private static void OpenFolderOnAndroid(string folderPath)
    {
        try
        {
            var extRoot = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
            if (extRoot != null && folderPath.StartsWith(extRoot))
            {
                var relativePath = folderPath.Substring(extRoot.Length).TrimStart('/');
                var uri = DocumentsContract.BuildDocumentUri(
                    "com.android.externalstorage.documents",
                    "primary:" + relativePath);

                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(uri, "vnd.android.document/directory");
                intent.AddFlags(ActivityFlags.NewTask);
                Application.Context.StartActivity(intent);
                Log.Info(Tag, $"Opened folder: {folderPath}");
                return;
            }

            var fileUri = Android.Net.Uri.Parse("file://" + folderPath);
            var fallbackIntent = new Intent(Intent.ActionView, fileUri);
            fallbackIntent.AddFlags(ActivityFlags.NewTask);
            Application.Context.StartActivity(fallbackIntent);
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to open folder: {ex.Message}");
        }
    }
}
#endif
