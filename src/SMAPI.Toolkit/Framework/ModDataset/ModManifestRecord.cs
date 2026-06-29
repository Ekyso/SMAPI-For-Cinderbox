using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewModdingAPI.Toolkit.Serialization;

namespace StardewModdingAPI.Toolkit.Framework.ModDataset;

/// <summary>The manifest data for a scanned mod.</summary>
public class ModManifestRecord
{
    /*********
    ** Accessors
    *********/
    /// <inheritdoc cref="IManifest.Name" />
    public string? Name { get; }

    /// <inheritdoc cref="IManifest.Description" />
    public string? Description { get; }

    /// <inheritdoc cref="IManifest.Author" />
    public string? Author { get; }

    /// <inheritdoc cref="IManifest.Version" />
    public string? Version { get; }

    /// <inheritdoc cref="IManifest.MinimumApiVersion" />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MinimumApiVersion { get; }

    /// <inheritdoc cref="IManifest.MinimumApiVersion" />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MinimumGameVersion { get; }

    /// <inheritdoc cref="IManifest.EntryDll" />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EntryDll { get; }

    /// <inheritdoc cref="IManifest.ContentPackFor" />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModManifestContentForRecord? ContentPackFor { get; }

    /// <inheritdoc cref="IManifest.Dependencies" />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModManifestDependencyRecord?[]? Dependencies { get; }

    /// <inheritdoc cref="IManifest.UpdateKeys" />
    public string[] UpdateKeys { get; }

    /// <inheritdoc cref="IManifest.UniqueID" />
    public string UniqueId { get; }

    /// <summary>The remaining fields which weren't mapped to one of the other fields.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonNode?>? OtherFields { get; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="name"><inheritdoc cref="Name" path="/summary"/></param>
    /// <param name="description"><inheritdoc cref="Description" path="/summary"/></param>
    /// <param name="author"><inheritdoc cref="Author" path="/summary"/></param>
    /// <param name="version"><inheritdoc cref="Version" path="/summary"/></param>
    /// <param name="minimumApiVersion"><inheritdoc cref="MinimumApiVersion" path="/summary"/></param>
    /// <param name="minimumGameVersion"><inheritdoc cref="MinimumGameVersion" path="/summary"/></param>
    /// <param name="entryDll"><inheritdoc cref="EntryDll" path="/summary"/></param>
    /// <param name="contentPackFor"><inheritdoc cref="ContentPackFor" path="/summary"/></param>
    /// <param name="dependencies"><inheritdoc cref="Dependencies" path="/summary"/></param>
    /// <param name="updateKeys"><inheritdoc cref="UpdateKeys" path="/summary"/></param>
    /// <param name="uniqueId"><inheritdoc cref="UniqueId" path="/summary"/></param>
    /// <param name="otherFields"><inheritdoc cref="OtherFields" path="/summary"/></param>
    [JsonConstructor]
    public ModManifestRecord(string? name, string? description, string? author, string? version, string? minimumApiVersion, string? minimumGameVersion, string? entryDll, ModManifestContentForRecord? contentPackFor, ModManifestDependencyRecord?[]? dependencies, string[] updateKeys, string uniqueId, Dictionary<string, JsonNode?>? otherFields)
    {
        this.Name = name;
        this.Description = description;
        this.Author = author;
        this.Version = version;
        this.MinimumApiVersion = minimumApiVersion;
        this.MinimumGameVersion = minimumGameVersion;
        this.EntryDll = entryDll;
        this.ContentPackFor = contentPackFor;
        this.Dependencies = dependencies;
        this.UpdateKeys = updateKeys;
        this.UniqueId = uniqueId;
        this.OtherFields = otherFields;
    }

    /// <summary>Construct an instance.</summary>
    /// <param name="from">The manifest whose data to copy.</param>
    public ModManifestRecord(IManifest from)
        : this(
            name: from.Name,
            description: from.Description,
            author: from.Author,
            version: from.Version?.ToString(),
            minimumApiVersion: from.MinimumApiVersion?.ToString(),
            minimumGameVersion: from.MinimumGameVersion?.ToString(),
            entryDll: from.EntryDll,
            contentPackFor: from.ContentPackFor is not null
                ? new ModManifestContentForRecord(from.ContentPackFor)
                : null,
            dependencies: from.Dependencies
                ?.Select(dependency => dependency is not null
                    ? new ModManifestDependencyRecord(dependency)
                    : null
                )
                .ToArray(),
            updateKeys: from.UpdateKeys,
            uniqueId: from.UniqueID,
            otherFields: from.ExtraFields?.Count > 0
                ? from.ExtraFields.ToDictionary(
                    p => p.Key,
                    p => JsonHelper.ConvertToSystemTextJsonNode(p.Value)
                )
                : null
        )
    { }
}
