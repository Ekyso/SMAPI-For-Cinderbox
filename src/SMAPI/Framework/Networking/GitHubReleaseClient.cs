using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Pathoschild.Http.Client;

namespace StardewModdingAPI.Framework.Networking;

/// <summary>An HTTP client for checking a GitHub repository's latest stable release.</summary>
internal sealed class GitHubReleaseClient : IDisposable
{
    /*********
    ** Fields
    *********/
    /// <summary>The underlying HTTP client.</summary>
    private readonly IClient Client;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="userAgentVersion">The SMAPI version to include in the user agent.</param>
    public GitHubReleaseClient(ISemanticVersion userAgentVersion)
    {
        this.Client = new FluentClient("https://api.github.com/")
            .SetUserAgent($"SMAPI/{userAgentVersion}")
            .AddDefault(
                request =>
                    request
                        .WithHeader("Accept", "application/vnd.github+json")
                        .WithHeader("X-GitHub-Api-Version", "2022-11-28")
            );
    }

    /// <summary>Get the latest stable release for a GitHub repository.</summary>
    /// <param name="repository">The repository key, like <c>Ekyso/SMAPI-For-Cinderbox</c>.</param>
    /// <returns>The latest release metadata.</returns>
    public async Task<GitHubReleaseInfo> GetLatestReleaseAsync(string repository)
    {
        this.AssertRepositoryFormat(repository);

        GitHubReleaseModel? release = await this.Client
            .GetAsync($"repos/{repository}/releases/latest")
            .As<GitHubReleaseModel?>();
        if (release == null)
            throw new InvalidOperationException($"GitHub returned no latest release for '{repository}'.");

        return new GitHubReleaseInfo(
            GitHubReleaseClient.ParseVersionTag(release.Tag),
            $"https://github.com/{repository}/releases/latest"
        );
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.Client.Dispose();
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Parse a GitHub release tag as a SMAPI version.</summary>
    /// <param name="tag">The raw release tag.</param>
    /// <returns>The parsed version.</returns>
    internal static ISemanticVersion ParseVersionTag(string? tag)
    {
        string versionString = tag?.Trim() ?? string.Empty;
        if (versionString.StartsWith('v') || versionString.StartsWith('V'))
            versionString = versionString[1..];

        if (
            !Toolkit.SemanticVersion.TryParse(
                versionString,
                allowNonStandard: true,
                out ISemanticVersion? version
            )
        )
            throw new FormatException($"GitHub's latest release tag '{tag}' isn't a valid SMAPI version.");

        return version;
    }

    /// <summary>Assert that a repository key is formatted correctly.</summary>
    /// <param name="repository">The repository key.</param>
    private void AssertRepositoryFormat(string repository)
    {
        if (
            string.IsNullOrWhiteSpace(repository)
            || repository.IndexOf('/') <= 0
            || repository.IndexOf('/') != repository.LastIndexOf('/')
            || repository.EndsWith('/')
        )
            throw new ArgumentException(
                $"The value '{repository}' isn't a valid GitHub repository key; expected a value like 'Ekyso/SMAPI-For-Cinderbox'.",
                nameof(repository)
            );
    }


    /*********
    ** Private models
    *********/
    /// <summary>The relevant fields from GitHub's release response.</summary>
    private sealed class GitHubReleaseModel
    {
        /// <summary>The release's Git tag.</summary>
        [JsonProperty("tag_name")]
        public string? Tag { get; set; }
    }
}

/// <summary>Metadata for the latest GitHub release.</summary>
/// <param name="Version">The release version.</param>
/// <param name="Url">The public URL for the release.</param>
internal sealed record GitHubReleaseInfo(ISemanticVersion Version, string Url);
