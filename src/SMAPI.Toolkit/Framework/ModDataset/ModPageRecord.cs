using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StardewModdingAPI.Toolkit.Framework.ModDataset;

/// <summary>The metadata for a mod page within the open mod dataset.</summary>
public class ModPageRecord
{
    /*********
    ** Accessors
    *********/
    /// <summary>The mod site which has the mod.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModSite Site { get; }

    /// <summary>The mod ID within the site.</summary>
    public long Id { get; }

    /// <summary>The mod name based on the mod page.</summary>
    public string? Name { get; }

    /// <summary>The author's canonical name based on the mod page (usually the username). This is identical for all mods uploaded to the same site by a user.</summary>
    public string? Author { get; }

    /// <summary>The author's display name based on the mod page, if different from <see cref="Author"/>. This may be different across mods by the same author. It may include free text entered by the uploader on Nexus, or include multiple names on CurseForge.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthorLabel { get; }

    /// <summary>A short blurb which describes the mod page, if available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TagLine { get; }

    /// <summary>The full mod page description body. The format depends on the mod site (e.g. ModDrop uses Markdown and Nexus uses BBCode).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; }

    /// <summary>The full URL to the mod's web page.</summary>
    public string PageUrl { get; }

    /// <summary>The version number based on the mod page. This is the version as shown on the mod page, which may not be a valid semantic version.</summary>
    public string? Version { get; }

    /// <summary>When the mod metadata last changed. To the extent supported by each mod site, this covers any change to the mod page (including mod page edits, file uploads, hiding or republishing, etc).</summary>
    public DateTimeOffset Updated { get; }

    /// <summary>The active downloads on the mod page.</summary>
    public ModPageDownloadRecord[] Downloads { get; }

    /// <summary>The remaining fields which weren't mapped to one of the other fields.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonNode?>? OtherFields { get; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="site"><inheritdoc cref="Site" path="/summary"/></param>
    /// <param name="id"><inheritdoc cref="Id" path="/summary"/></param>
    /// <param name="name"><inheritdoc cref="Name" path="/summary"/></param>
    /// <param name="author"><inheritdoc cref="Author" path="/summary"/></param>
    /// <param name="authorLabel"><inheritdoc cref="AuthorLabel" path="/summary"/></param>
    /// <param name="tagLine"><inheritdoc cref="TagLine" path="/summary"/></param>
    /// <param name="description"><inheritdoc cref="Description" path="/summary"/></param>
    /// <param name="pageUrl"><inheritdoc cref="PageUrl" path="/summary"/></param>
    /// <param name="version"><inheritdoc cref="Version" path="/summary"/></param>
    /// <param name="updated"><inheritdoc cref="Updated" path="/summary"/></param>
    /// <param name="downloads"><inheritdoc cref="Downloads" path="/summary"/></param>
    /// <param name="otherFields"><inheritdoc cref="OtherFields" path="/summary"/></param>
    public ModPageRecord(ModSite site, long id, string? name, string? author, string? authorLabel, string? tagLine, string? description, string pageUrl, string? version, DateTimeOffset updated, ModPageDownloadRecord[]? downloads, Dictionary<string, JsonNode?>? otherFields)
    {
        this.Site = site;
        this.Id = id;
        this.Name = name;
        this.Author = author;
        this.AuthorLabel = authorLabel;
        this.TagLine = tagLine;
        this.Description = description;
        this.PageUrl = pageUrl;
        this.Version = version;
        this.Updated = updated;
        this.Downloads = downloads ?? [];
        this.OtherFields = otherFields;
    }
}
