#if SMAPI_FOR_ANDROID
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Android.Util;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Reflection-based integration with the QuickSave mod (DLX.QuickSave).
/// Detects the mod, hooks its save event, inserts Quick Save/Load buttons
/// into OptionsPage, and parses the quicksave file for date + time display.
/// </summary>
public static class QuickSavePatch
{
    private const string Tag = "QuickSavePatch";
    private const string CustomDataKey = "smapi/mod-data/dlx.quicksave/dlx.quicksave_extradata";
    private const BindingFlags AnyStatic =
        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    private static MethodInfo _trySave = null!;
    private static MethodInfo _tryLoad = null!;

    private static bool _quicksaveExists;
    private static string _quicksaveDateString = null!;
    private static DateTime _lastSnapshotFileTime;

    public static bool IsAvailable { get; private set; }
    public static bool QuicksaveExists => _quicksaveExists;
    public static string QuicksaveDateString => _quicksaveDateString;

    public static void Apply(Harmony harmony)
    {
        var optionsPageCtor = typeof(OptionsPage).GetConstructor(
            new[] { typeof(int), typeof(int), typeof(int), typeof(int) }
        );
        if (optionsPageCtor != null)
        {
            harmony.Patch(
                optionsPageCtor,
                postfix: new HarmonyMethod(
                    typeof(QuickSavePatch).GetMethod(nameof(OptionsPage_Postfix), AnyStatic)
                )
            );
            Log.Verbose(Tag, "Patched OptionsPage.ctor for QuickSave buttons");
        }

        ResolveQuickSave(harmony);
    }

    /// <summary>Harmony postfix on OptionsPage ctor - inserts Quick Save/Load buttons after "General" header.</summary>
    public static void OptionsPage_Postfix(OptionsPage __instance)
    {
        try
        {
            if (Game1.gameMode != 3 || !IsAvailable)
                return;

            RefreshQuicksaveState();

            var optionsList = __instance.options;
            for (int i = 0; i < optionsList.Count; i++)
            {
                if (optionsList[i].whichOption == -1)
                {
                    optionsList.Insert(i + 1, new QuickSaveOptionButton(isSaveButton: true));
                    optionsList.Insert(i + 2, new QuickSaveOptionButton(isSaveButton: false));
                    Log.Verbose(Tag, "Inserted QuickSave/QuickLoad buttons");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"OptionsPage_Postfix failed: {ex.Message}");
        }
    }

    /// <summary>Harmony postfix on QuickSaveAPI.RaiseSaved - live cache update.</summary>
    public static void OnSaveCompleted()
    {
        _quicksaveExists = true;
        _quicksaveDateString = FormatGameDate(
            Game1.Date?.Season.ToString(),
            Game1.Date?.DayOfMonth,
            Game1.Date?.Year,
            Game1.timeOfDay
        )!;
        Log.Verbose(Tag, $"Save completed: cached date={_quicksaveDateString}");
    }

    public static void InvokeSave() => InvokeDelayed(_trySave, "TrySave");

    public static void InvokeLoad() => InvokeDelayed(_tryLoad, "TryLoad");

    /// <summary>
    /// Reads quicksave file state from disk. Called when the options page opens.
    /// Skips XML re-parse if file modification time hasn't changed.
    /// </summary>
    public static void RefreshQuicksaveState()
    {
        try
        {
            string path = GetQuicksavePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _quicksaveExists = false;
                _quicksaveDateString = null!;
                return;
            }

            _quicksaveExists = true;

            DateTime fileTime = File.GetLastWriteTimeUtc(path);
            if (fileTime != _lastSnapshotFileTime)
            {
                _lastSnapshotFileTime = fileTime;
                _quicksaveDateString = ParseQuicksaveFile(path)!;
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"RefreshQuicksaveState failed: {ex.Message}");
        }
    }

    /// <summary>Streams XML for season/day/year + CustomData for TimeOfDay.</summary>
    private static string? ParseQuicksaveFile(string path)
    {
        string? season = null;
        int? day = null;
        int? year = null;
        int? timeOfDay = null;

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true }
            );

            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Depth == 1)
                {
                    switch (reader.Name)
                    {
                        case "currentSeason":
                            season = reader.ReadElementContentAsString();
                            break;
                        case "dayOfMonth":
                            day = reader.ReadElementContentAsInt();
                            break;
                        case "year":
                            year = reader.ReadElementContentAsInt();
                            break;
                        case "CustomData":
                            timeOfDay = ParseTimeOfDayFromCustomData(reader);
                            reader.Read(); // advance past EndElement
                            break;
                        default:
                            reader.Skip();
                            continue;
                    }

                    if (season != null && day != null && year != null && timeOfDay != null)
                        break;
                    continue; // ReadElementContent already advanced
                }

                reader.Read();
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Failed to parse quicksave: {ex.Message}");
            return null;
        }

        if (season == null || day == null || year == null)
            return null;

        return FormatGameDate(season, day, year, timeOfDay);
    }

    /// <summary>Parses TimeOfDay from the QuickSave mod's CustomData entry via ReadSubtree.</summary>
    private static int? ParseTimeOfDayFromCustomData(XmlReader parentReader)
    {
        using var sub = parentReader.ReadSubtree();

        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element || sub.Name != "item")
                continue;

            
            string? key = null;
            string? value = null;
            int itemDepth = sub.Depth;

            while (sub.Read() && sub.Depth > itemDepth)
            {
                if (sub.NodeType == XmlNodeType.Element && sub.Name == "string")
                {
                    if (key == null)
                        key = sub.ReadElementContentAsString();
                    else
                    {
                        value = sub.ReadElementContentAsString();
                        break;
                    }
                }
            }

            if (key != CustomDataKey || value == null)
                continue;

            try
            {
                var obj = JObject.Parse(value);
                if (obj.TryGetValue("TimeOfDay", out var tok))
                    return tok.Value<int>();
            }
            catch
            {
                
            }

            return null;
        }

        return null;
    }

    private static string? FormatGameDate(string? season, int? day, int? year, int? timeOfDay)
    {
        if (season == null || day == null || year == null)
            return null;

        string titleSeason = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(season);
        string date = $"{titleSeason} {day.Value}, Y{year.Value}";

        if (timeOfDay is > 0)
        {
            int hours24 = timeOfDay.Value / 100;
            int minutes = timeOfDay.Value % 100;
            string period = hours24 >= 12 ? "PM" : "AM";
            int hours12 = hours24 % 12;
            if (hours12 == 0)
                hours12 = 12;
            date += $" {hours12}:{minutes:D2} {period}";
        }

        return date;
    }

    /// <summary>Closes the menu and calls the given method on the next tick.</summary>
    private static void InvokeDelayed(MethodInfo method, string name)
    {
        if (method == null)
            return;

        try
        {
            // build args to match the current QuickSave version's signature
            var parameters = method.GetParameters();
            object[]? args = null;
            if (parameters.Length > 0)
            {
                args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType == typeof(bool))
                        args[i] = false; // showRedMessage = false
                    else if (parameters[i].ParameterType == typeof(string))
                        args[i] = "Quicksave"; // default save file name
                    else if (parameters[i].HasDefaultValue)
                        args[i] = parameters[i].DefaultValue!;
                    else
                        args[i] = parameters[i].ParameterType.IsValueType
                            ? Activator.CreateInstance(parameters[i].ParameterType)!
                            : null!;
                }
            }

            Game1.exitActiveMenu();
            DelayedAction.functionAfterDelay(
                () =>
                {
                    try
                    {
                        method.Invoke(null, args);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(Tag, $"{name} failed: {ex.Message}");
                    }
                },
                50
            );
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Invoke {name} failed: {ex.Message}");
        }
    }

    private static string GetQuicksavePath()
    {
        string saveName = SaveGame.FilterFileName(Game1.GetSaveGameName(false));
        string folder = saveName + "_" + Game1.uniqueIDForThisGame;
        return Path.Combine(Constants.SavesPath, folder, "Quicksave");
    }

    /// <summary>Finds the QuickSave assembly and resolves TrySave/TryLoad methods.</summary>
    private static void ResolveQuickSave(Harmony harmony)
    {
        try
        {
            var assembly = AppDomain
                .CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "QuickSave");

            if (assembly == null)
            {
                Log.Verbose(Tag, "QuickSave assembly not loaded");
                return;
            }

            var mainType = assembly.GetType("QuickSave.Lib.Main");
            if (mainType == null)
            {
                Log.Warn(Tag, "QuickSave.Lib.Main type not found");
                return;
            }

            _trySave = mainType.GetMethod("TrySave", AnyStatic)!;
            _tryLoad = mainType.GetMethod("TryLoad", AnyStatic)!;

            if (_trySave != null && _tryLoad != null)
            {
                IsAvailable = true;
                Log.Info(Tag, "QuickSave integration resolved successfully");
            }
            else
            {
                Log.Warn(
                    Tag,
                    $"Partial resolution: TrySave={_trySave != null}, TryLoad={_tryLoad != null}"
                );
                return;
            }

            HookSaveEvent(harmony, assembly);
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to resolve QuickSave: {ex.Message}");
        }
    }

    /// <summary>Patches QuickSaveAPI.RaiseSaved so we update cached state after any save.</summary>
    private static void HookSaveEvent(Harmony harmony, Assembly assembly)
    {
        try
        {
            var modApiField = assembly
                .GetType("QuickSave.ModEntry")
                ?.GetField("ModAPI", BindingFlags.Public | BindingFlags.Static);

            if (modApiField == null)
            {
                Log.Warn(Tag, "Could not find ModEntry.ModAPI field");
                return;
            }

            var raiseSaved = modApiField.FieldType.GetMethod(
                "RaiseSaved",
                BindingFlags.Public | BindingFlags.Instance
            );

            if (raiseSaved == null)
            {
                Log.Warn(Tag, "Could not find QuickSaveAPI.RaiseSaved method");
                return;
            }

            harmony.Patch(
                raiseSaved,
                postfix: new HarmonyMethod(
                    typeof(QuickSavePatch).GetMethod(nameof(OnSaveCompleted), AnyStatic)
                )
            );
            Log.Info(Tag, "Hooked QuickSaveAPI.RaiseSaved for cache updates");
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Could not hook save event (non-fatal): {ex.Message}");
        }
    }
}
#endif
