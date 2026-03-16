using System;
using System.IO;
using System.Reflection;
using System.Xml;
using HarmonyLib;
#if SMAPI_FOR_ANDROID
using Android.Util;
#endif

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Fixes Custom Farm Loader on Android. The mod uses Environment.GetFolderPath to find
/// saves, which resolves to the wrong location. Patches FarmTypeCache methods to use
/// Constants.SavesPath instead.
/// </summary>
internal static class CustomFarmLoaderPatch
{
    private const string Tag = "CustomFarmLoaderPatch";

    private static void LogInfo(string message)
    {
#if SMAPI_FOR_ANDROID
        Log.Info(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] {message}");
#endif
    }

    private static void LogError(string message)
    {
#if SMAPI_FOR_ANDROID
        Log.Error(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] ERROR: {message}");
#endif
    }

    /// <summary>Apply the patches if Custom Farm Loader is loaded.</summary>
    public static void Apply(Harmony harmony)
    {
        try
        {
            Type? farmTypeCacheType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Custom Farm Loader")
                {
                    farmTypeCacheType = assembly.GetType("Custom_Farm_Loader.Lib.FarmTypeCache");
                    break;
                }
            }

            if (farmTypeCacheType == null)
            {
                LogInfo("Custom Farm Loader not found, skipping patch");
                return;
            }

            var readFarmTypeQuickly = farmTypeCacheType.GetMethod(
                "readFarmTypeQuickly",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null
            );
            if (readFarmTypeQuickly != null)
            {
                harmony.Patch(readFarmTypeQuickly, prefix: new HarmonyMethod(
                    typeof(CustomFarmLoaderPatch).GetMethod(nameof(ReadFarmTypeQuickly_Prefix),
                        BindingFlags.Public | BindingFlags.Static)));
                LogInfo("Patched readFarmTypeQuickly");
            }
            else
            {
                LogError("Could not find FarmTypeCache.readFarmTypeQuickly");
            }

            var readFarmType = farmTypeCacheType.GetMethod(
                "readFarmType",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null
            );
            if (readFarmType != null)
            {
                harmony.Patch(readFarmType, prefix: new HarmonyMethod(
                    typeof(CustomFarmLoaderPatch).GetMethod(nameof(ReadFarmType_Prefix),
                        BindingFlags.Public | BindingFlags.Static)));
                LogInfo("Patched readFarmType");
            }
            else
            {
                LogError("Could not find FarmTypeCache.readFarmType");
            }

            var loadInitialCache = farmTypeCacheType.GetMethod(
                "LoadInitialCache",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null
            );
            if (loadInitialCache != null)
            {
                harmony.Patch(loadInitialCache, prefix: new HarmonyMethod(
                    typeof(CustomFarmLoaderPatch).GetMethod(nameof(LoadInitialCache_Prefix),
                        BindingFlags.Public | BindingFlags.Static)));
                LogInfo("Patched LoadInitialCache");
            }
            else
            {
                LogError("Could not find FarmTypeCache.LoadInitialCache");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to patch Custom Farm Loader: {ex.Message}");
        }
    }

    /// <summary>Replacement for FarmTypeCache.readFarmTypeQuickly using Constants.SavesPath.</summary>
    public static bool ReadFarmTypeQuickly_Prefix(string saveFile, ref string __result)
    {
        try
        {
            var savePath = Path.Combine(Constants.SavesPath, saveFile, saveFile);

            if (!File.Exists(savePath))
            {
                __result = "";
                return false;
            }

            var fileInfo = new FileInfo(savePath);
            long seekPos = Math.Max(0, fileInfo.Length - 25000);

            string searchTag = "<whichFarm>";
            int matchIndex = 0;
            string result = "";

            using var stream = File.OpenRead(savePath);
            stream.Seek(seekPos, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);

            int ch;
            while ((ch = reader.Read()) >= 0)
            {
                if (matchIndex < searchTag.Length)
                {
                    if (ch == searchTag[matchIndex])
                        matchIndex++;
                    else
                        matchIndex = 0;
                }
                else
                {
                    if (ch == '<')
                        break;
                    result += (char)ch;
                }
            }

            __result = result;
        }
        catch (Exception)
        {
            __result = "";
        }

        return false; // always skip original
    }

    /// <summary>Replacement for FarmTypeCache.readFarmType using Constants.SavesPath.</summary>
    public static bool ReadFarmType_Prefix(string saveFile, ref string __result)
    {
        try
        {
            var savePath = Path.Combine(Constants.SavesPath, saveFile, saveFile);

            if (!File.Exists(savePath))
            {
                __result = "";
                return false;
            }

            var doc = new XmlDocument();
            doc.Load(savePath);
            var node = doc.DocumentElement?.SelectSingleNode("/SaveGame/whichFarm");
            __result = node?.InnerText ?? "";
        }
        catch (Exception)
        {
            __result = "";
        }

        return false; // always skip original
    }

    /// <summary>Replacement for FarmTypeCache.LoadInitialCache using Constants.SavesPath.</summary>
    public static bool LoadInitialCache_Prefix()
    {
        try
        {
            var farmTypeCacheType = GetFarmTypeCacheType();
            if (farmTypeCacheType == null)
                return true; // fallback to original

            var cacheField = farmTypeCacheType.GetField("Cache",
                BindingFlags.NonPublic | BindingFlags.Static);
            var helperField = farmTypeCacheType.GetField("Helper",
                BindingFlags.NonPublic | BindingFlags.Static);
            var monitorField = farmTypeCacheType.GetField("Monitor",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (cacheField == null || helperField == null || monitorField == null)
                return true; // fallback to original

            var helper = helperField.GetValue(null) as IModHelper;
            var monitor = monitorField.GetValue(null) as IMonitor;
            if (helper == null || monitor == null)
                return true;

            var cache = helper.Data.ReadJsonFile<System.Collections.Generic.Dictionary<string, string>>(
                "FarmTypeCache.json");
            bool isNewCache = cache == null;
            cache ??= new System.Collections.Generic.Dictionary<string, string>();
            cacheField.SetValue(null, cache);

            var savesDir = Constants.SavesPath;
            if (!Directory.Exists(savesDir))
                return false;

            var saveDirs = Directory.GetDirectories(savesDir);
            monitor.Log("Generating FarmTypeCache, this might take a while initially",
                isNewCache ? LogLevel.Warn : LogLevel.Trace);

            var readQuickly = farmTypeCacheType.GetMethod("readFarmTypeQuickly",
                BindingFlags.NonPublic | BindingFlags.Static);
            var readFull = farmTypeCacheType.GetMethod("readFarmType",
                BindingFlags.NonPublic | BindingFlags.Static);

            int count = 0;
            foreach (var saveDir in saveDirs)
            {
                var saveName = Path.GetFileName(saveDir);
                if (cache.ContainsKey(saveName))
                    continue;

                var saveFilePath = Path.Combine(saveDir, saveName);
                if (!File.Exists(saveFilePath))
                    continue;

                monitor.Log($"Reading Farmtype for: {saveName}", LogLevel.Trace);

                string farmType = "";
                if (readQuickly != null)
                    farmType = (string)(readQuickly.Invoke(null, new object[] { saveName }) ?? "");
                if (farmType == "" && readFull != null)
                    farmType = (string)(readFull.Invoke(null, new object[] { saveName }) ?? "");

                if (farmType != "")
                {
                    cache[saveName] = farmType;
                    count++;
                    monitor.Log($"  FarmType: {farmType}", LogLevel.Trace);
                }
            }

            if (count > 0)
                helper.Data.WriteJsonFile("FarmTypeCache.json", cache);
        }
        catch (Exception ex)
        {
            LogError($"LoadInitialCache replacement failed: {ex.Message}");
            return true; // fallback to original on error
        }

        return false; // skip original
    }

    private static Type? GetFarmTypeCacheType()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name == "Custom Farm Loader")
                return assembly.GetType("Custom_Farm_Loader.Lib.FarmTypeCache");
        }
        return null;
    }
}
