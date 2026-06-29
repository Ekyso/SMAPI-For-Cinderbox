using System.Text.Json.Serialization;
using StardewModdingAPI.Toolkit.Framework.ModScanning;

namespace StardewModdingAPI.Toolkit.Framework.ModDataset;

/// <summary>A scanned SMAPI mod folder entry within a download.</summary>
public class ModFolderRecord
{
    /*********
    ** Accessors
    *********/
    /// <summary>The normalized mod ID from the mod manifest, if it was found.</summary>
    public string? Id { get; }

    /// <summary>The display name for the mod. This is usually the normalized name from the manifest (if it was parseable), else generated based on the folder path in the download.</summary>
    public string DisplayName { get; }

    /// <summary>The detected mod type.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModType Type { get; }

    /// <summary>The path to this mod within the download, or <c>null</c> if it's the entire download.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RelativePath { get; }

    /// <summary>The mod's parsed manifest file, if it could be parsed. This is the normalized JSON structure, not the raw text (e.g. JSON comments aren't included).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModManifestRecord? Manifest { get; }

    /// <summary>If the mod's manifest couldn't be parsed, the error type.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModParseError? ManifestParseError { get; }

    /// <summary>If the mod's manifest couldn't be parsed, a human-readable message indicating why (e.g. the message shown in the SMAPI console window).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ManifestParseErrorText { get; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="id"><inheritdoc cref="Id" path="/summary"/></param>
    /// <param name="displayName"><inheritdoc cref="DisplayName" path="/summary"/></param>
    /// <param name="type"><inheritdoc cref="Type" path="/summary"/></param>
    /// <param name="relativePath"><inheritdoc cref="RelativePath" path="/summary"/></param>
    /// <param name="manifest"><inheritdoc cref="Manifest" path="/summary"/></param>
    /// <param name="manifestParseError"><inheritdoc cref="ManifestParseError" path="/summary"/></param>
    /// <param name="manifestParseErrorText"><inheritdoc cref="ManifestParseErrorText" path="/summary"/></param>
    public ModFolderRecord(string? id, string displayName, ModType type, string? relativePath, ModManifestRecord? manifest, ModParseError? manifestParseError, string? manifestParseErrorText)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.Type = type;
        this.RelativePath = relativePath;
        this.Manifest = manifest;
        this.ManifestParseError = manifestParseError;
        this.ManifestParseErrorText = manifestParseErrorText;
    }
}
