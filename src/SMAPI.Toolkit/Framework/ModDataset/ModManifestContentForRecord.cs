using System.Text.Json.Serialization;

namespace StardewModdingAPI.Toolkit.Framework.ModDataset;

/// <summary>The 'content pack for' data in a <see cref="ModManifestRecord"/>.</summary>
public class ModManifestContentForRecord
{
    /*********
    ** Accessors
    *********/
    /// <inheritdoc cref="IManifestContentPackFor.UniqueID" />
    public string? UniqueId { get; }

    /// <inheritdoc cref="IManifestContentPackFor.MinimumVersion" />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MinimumVersion { get; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="uniqueId"><inheritdoc cref="UniqueId" path="/summary"/></param>
    /// <param name="minimumVersion"><inheritdoc cref="MinimumVersion" path="/summary"/></param>
    [JsonConstructor]
    public ModManifestContentForRecord(string? uniqueId, string? minimumVersion)
    {
        this.UniqueId = uniqueId;
        this.MinimumVersion = minimumVersion;
    }

    /// <summary>Construct an instance.</summary>
    /// <param name="from">The record whose data to copy.</param>
    public ModManifestContentForRecord(IManifestContentPackFor from)
        : this(
            uniqueId: from.UniqueID,
            minimumVersion: from.MinimumVersion?.ToString()
        )
    { }
}
