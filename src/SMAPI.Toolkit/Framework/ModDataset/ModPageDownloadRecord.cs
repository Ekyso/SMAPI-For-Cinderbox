using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StardewModdingAPI.Toolkit.Framework.ModDataset;

/// <summary>A file entry within a mod record.</summary>
public class ModPageDownloadRecord
{
    /*********
    ** Accessors
    *********/
    /// <summary>The unique ID for this file within the mod site.</summary>
    public long Id { get; }

    /// <summary>The download type.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModDownloadType Type { get; }

    /// <summary>The download's title as entered by the uploader.</summary>
    public string? DisplayName { get; }

    /// <summary>The name of the file (usually a <c>.zip</c> file) downloaded when the player downloads the file.</summary>
    public string? FileName { get; }

    /// <summary>The description text for the file. This may be BBCode on Nexus.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; }

    /// <summary>The version number based on the download info. This is the version as shown on the mod page, which may not be a valid semantic version.</summary>
    public string? Version { get; }

    /// <summary>The size of the downloaded file in bytes.</summary>
    public long SizeInBytes { get; }

    /// <summary>When the file was uploaded.</summary>
    public DateTimeOffset Uploaded { get; }

    /// <summary>The mods detected within this downloaded, based on analysis using the SMAPI toolkit.</summary>
    public List<ModFolderRecord> Mods { get; }

    /// <summary>The error message indicating why the file could not be downloaded, if applicable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DownloadError { get; }

    /// <summary>The error message indicating why the mods could not be unpacked from the download, if applicable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UnpackError { get; }

    /// <summary>The remaining fields which weren't mapped to one of the other fields.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonNode?>? OtherFields { get; }

    /// <summary>Whether the mods were successfully downloaded and unpacked.</summary>
    [JsonIgnore]
    public bool FullyAnalyzed => this.DownloadError is null && this.UnpackError is null && this.Mods.Count > 0;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="id"><inheritdoc cref="Id" path="/summary"/></param>
    /// <param name="type"><inheritdoc cref="Type" path="/summary"/></param>
    /// <param name="displayName"><inheritdoc cref="DisplayName" path="/summary"/></param>
    /// <param name="fileName"><inheritdoc cref="FileName" path="/summary"/></param>
    /// <param name="description"><inheritdoc cref="Description" path="/summary"/></param>
    /// <param name="version"><inheritdoc cref="Version" path="/summary"/></param>
    /// <param name="sizeInBytes"><inheritdoc cref="SizeInBytes" path="/summary"/></param>
    /// <param name="uploaded"><inheritdoc cref="Uploaded" path="/summary"/></param>
    /// <param name="otherFields"><inheritdoc cref="OtherFields" path="/summary"/></param>
    /// <param name="mods"><inheritdoc cref="Mods" path="/summary"/> This should be omitted in API client code, since it'll be set based on the returned data.</param>
    /// <param name="downloadError"><inheritdoc cref="DownloadError" path="/summary"/></param>
    /// <param name="unpackError"><inheritdoc cref="UnpackError" path="/summary"/></param>
    public ModPageDownloadRecord(long id, ModDownloadType type, string? displayName, string? fileName, string? description, string? version, long sizeInBytes, DateTimeOffset uploaded, Dictionary<string, JsonNode?>? otherFields, List<ModFolderRecord>? mods = null, string? downloadError = null, string? unpackError = null)
    {
        this.Id = id;
        this.Type = type;
        this.DisplayName = displayName;
        this.FileName = fileName;
        this.Description = description;
        this.Version = version;
        this.SizeInBytes = sizeInBytes;
        this.Uploaded = uploaded;
        this.Mods = mods ?? [];
        this.DownloadError = downloadError;
        this.UnpackError = unpackError;
        this.OtherFields = otherFields;
    }
}
