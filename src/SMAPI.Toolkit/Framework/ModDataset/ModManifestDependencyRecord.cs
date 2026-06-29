using System.Text.Json.Serialization;

namespace StardewModdingAPI.Toolkit.Framework.ModDataset;

/// <summary>A dependency reference in a <see cref="ModManifestRecord"/>.</summary>
public class ModManifestDependencyRecord
{
    /*********
    ** Accessors
    *********/
    /// <inheritdoc cref="IManifestDependency.UniqueID" />
    public string? UniqueId { get; }

    /// <inheritdoc cref="IManifestDependency.MinimumVersion" />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MinimumVersion { get; }

    /// <inheritdoc cref="IManifestDependency.IsRequired" />
    public bool IsRequired { get; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="uniqueId"><inheritdoc cref="UniqueId" path="/summary"/></param>
    /// <param name="minimumVersion"><inheritdoc cref="MinimumVersion" path="/summary"/></param>
    /// <param name="isRequired"><inheritdoc cref="IsRequired" path="/summary"/></param>
    [JsonConstructor]
    public ModManifestDependencyRecord(string? uniqueId, string? minimumVersion, bool isRequired)
    {
        this.UniqueId = uniqueId;
        this.MinimumVersion = minimumVersion;
        this.IsRequired = isRequired;
    }

    /// <summary>Construct an instance.</summary>
    /// <param name="from">The record whose data to copy.</param>
    public ModManifestDependencyRecord(IManifestDependency from)
        : this(
            uniqueId: from.UniqueID,
            minimumVersion: from.MinimumVersion?.ToString(),
            isRequired: from.IsRequired
        )
    { }
}
