using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace StardewModdingAPI.Web.Framework.Caching.ModDataset;

/// <inheritdoc cref="IModDatasetRepository" />
internal class ModDatasetRepository : IModDatasetRepository
{
    /*********
    ** Fields
    *********/
    /// <summary>The HTTPS URL of the mod dataset Git repo.</summary>
    private readonly string RepoUrl;

    /// <summary>The full path to the mod dataset repo.</summary>
    private readonly string LocalRepoPath;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="repoUrl"><inheritdoc cref="RepoUrl" path="/summary" /></param>
    /// <param name="localRepoPath">The path to the mod dataset repo.</param>
    public ModDatasetRepository(string repoUrl, string localRepoPath)
    {
        this.RepoUrl = repoUrl;
        this.LocalRepoPath = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(localRepoPath)
        );
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Action<string>? log = null)
    {
        if (!Directory.Exists(Path.Combine(this.LocalRepoPath, ".git")))
        {
            log?.Invoke("Cloning mod dataset repo...");
            Directory.CreateDirectory(Path.GetDirectoryName(this.LocalRepoPath)!);
            await this.RunGitAsync("clone", "--depth", "1", this.RepoUrl, this.LocalRepoPath);
        }
        else
        {
            log?.Invoke("Fetching latest changes from mod dataset repo...");
            await this.RunGitAsync("-C", this.LocalRepoPath, "fetch", "--depth", "1", "origin");
            await this.RunGitAsync("-C", this.LocalRepoPath, "reset", "--hard", "FETCH_HEAD");
        }
    }

    /// <inheritdoc />
    public string GetFilePath(string relativePath)
    {
        return Path.Combine(this.LocalRepoPath, "dataset", relativePath);
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Run a Git command and wait for it to complete.</summary>
    /// <param name="args">The git arguments.</param>
    /// <exception cref="InvalidOperationException">The Git command exited with a non-zero exit code.</exception>
    private async Task RunGitAsync(params string[] args)
    {
        ProcessStartInfo startInfo = new("git", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Git process.");

        Task readOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> readError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(readOutput, readError);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Git command failed with exit code {process.ExitCode}: {await readError}.");
    }
}
