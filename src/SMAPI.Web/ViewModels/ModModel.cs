using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StardewModdingAPI.Toolkit.Framework.Clients.CompatibilityRepo;
using StardewModdingAPI.Toolkit.Framework.UpdateData;

namespace StardewModdingAPI.Web.ViewModels;

/// <summary>Metadata about a mod.</summary>
public class ModModel
{
    /*********
    ** Accessors
    *********/
    /// <summary>The mod IDs.</summary>
    public string[] Id { get; }

    /// <summary>The mod name.</summary>
    public string? Name { get; }

    /// <summary>The mod's alternative names, if any.</summary>
    public string? AlternateNames { get; }

    /// <summary>The mod author's name.</summary>
    public string? Author { get; }

    /// <summary>The mod author's alternative names, if any.</summary>
    public string? AlternateAuthors { get; }

    /// <summary>The GitHub repo, if any.</summary>
    public string? GitHubRepo { get; }

    /// <summary>The URL to the mod's source code, if any.</summary>
    public string? SourceUrl { get; }

    /// <summary>The compatibility status for the stable version of the game, or <c>null</c> if it's compatible with no notes.</summary>
    public ModCompatibilityModel? Compatibility { get; }

    /// <summary>Links to the available mod pages.</summary>
    public ModLinkModel[] ModPages { get; }

    /// <summary>The human-readable warnings for players about this mod.</summary>
    public string[]? Warnings { get; }

    /// <summary>Special notes intended for developers who maintain unofficial updates or submit pull requests.</summary>
    public string? DevNote { get; }

    /// <summary>A unique identifier for the mod that can be used in an anchor URL.</summary>
    public string? Slug { get; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="id"><inheritdoc cref="Id" path="/summary"/></param>
    /// <param name="name"><inheritdoc cref="Name" path="/summary"/></param>
    /// <param name="alternateNames"><inheritdoc cref="AlternateNames" path="/summary"/></param>
    /// <param name="author"><inheritdoc cref="Author" path="/summary"/></param>
    /// <param name="alternateAuthors"><inheritdoc cref="AlternateAuthors" path="/summary"/></param>
    /// <param name="gitHubRepo"><inheritdoc cref="GitHubRepo" path="/summary"/></param>
    /// <param name="sourceUrl"><inheritdoc cref="SourceUrl" path="/summary"/></param>
    /// <param name="compatibility"><inheritdoc cref="Compatibility" path="/summary"/></param>
    /// <param name="modPages"><inheritdoc cref="ModPages" path="/summary"/></param>
    /// <param name="warnings"><inheritdoc cref="Warnings" path="/summary"/></param>
    /// <param name="devNote"><inheritdoc cref="DevNote" path="/summary"/></param>
    /// <param name="slug"><inheritdoc cref="Slug" path="/summary"/></param>
    [JsonConstructor]
    public ModModel(string[] id, string? name, string? alternateNames, string? author, string? alternateAuthors, string? gitHubRepo, string? sourceUrl, ModCompatibilityModel? compatibility, ModLinkModel[] modPages, string[]? warnings, string? devNote, string? slug)
    {
        this.Id = id;
        this.Name = name;
        this.AlternateNames = alternateNames;
        this.Author = author;
        this.AlternateAuthors = alternateAuthors;
        this.GitHubRepo = gitHubRepo;
        this.SourceUrl = sourceUrl;
        this.Compatibility = compatibility;
        this.ModPages = modPages;
        this.Warnings = warnings;
        this.DevNote = devNote;
        this.Slug = slug;
    }

    /// <summary>Construct an instance.</summary>
    /// <param name="entry">The mod metadata.</param>
    public ModModel(ModCompatibilityEntry entry)
    {
        // basic info
        this.Id = entry.ID.ToArray();
        this.Name = entry.Name.FirstOrDefault();
        this.AlternateNames = entry.Name.Length > 1 ? string.Join(", ", entry.Name.Skip(1)) : null;
        this.Author = entry.Author.FirstOrDefault();
        this.AlternateAuthors = entry.Author.Length > 1 ? string.Join(", ", entry.Author.Skip(1)) : null;
        this.GitHubRepo = entry.GitHubRepo;
        this.SourceUrl = this.GetSourceUrl(entry);
        this.Compatibility = !entry.Compatibility.IsDefault() ? new ModCompatibilityModel(entry.Compatibility) : null;
        this.ModPages = this.GetModPageUrls(entry).ToArray();
        this.Warnings = entry.HtmlWarnings ?? entry.Warnings;
        this.DevNote = entry.DevNote;
        this.Slug = entry.Anchor;

        if (this.Warnings.Length == 0)
            this.Warnings = null;
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Get the web URL for the mod's source code repository, if any.</summary>
    /// <param name="entry">The mod metadata.</param>
    private string? GetSourceUrl(ModCompatibilityEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.GitHubRepo))
            return $"https://github.com/{entry.GitHubRepo}";
        if (!string.IsNullOrWhiteSpace(entry.CustomSourceUrl))
            return entry.CustomSourceUrl;
        return null;
    }

    /// <summary>Get the web URLs for the mod pages, if any.</summary>
    /// <param name="entry">The mod metadata.</param>
    private IEnumerable<ModLinkModel> GetModPageUrls(ModCompatibilityEntry entry)
    {
        foreach ((ModSiteKey modSite, string url) in entry.GetModPageUrls())
        {
            string linkText = modSite switch
            {
                ModSiteKey.Unknown => "custom",
                _ => modSite.ToString()
            };

            yield return new ModLinkModel(url, linkText);
        }
    }
}
