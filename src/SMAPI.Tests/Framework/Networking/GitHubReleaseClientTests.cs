using System;
using FluentAssertions;
using NUnit.Framework;
using StardewModdingAPI.Framework.Networking;

namespace SMAPI.Tests.Framework.Networking;

/// <summary>Unit tests for <see cref="GitHubReleaseClient"/>.</summary>
[TestFixture]
internal class GitHubReleaseClientTests
{
    /// <summary>Assert that supported GitHub release tags are parsed correctly.</summary>
    /// <param name="tag">The raw GitHub tag.</param>
    [TestCase("4.5.2", ExpectedResult = "4.5.2")]
    [TestCase("4.5.2.5", ExpectedResult = "4.5.2.5")]
    [TestCase("v4.5.2.6", ExpectedResult = "4.5.2.6")]
    [TestCase(" V4.5.2.7 ", ExpectedResult = "4.5.2.7")]
    public string ParseVersionTag_WithValidTag(string tag)
    {
        return GitHubReleaseClient.ParseVersionTag(tag).ToString();
    }

    /// <summary>Assert that invalid GitHub release tags are rejected.</summary>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("latest")]
    [TestCase("v")]
    public void ParseVersionTag_WithInvalidTag(string? tag)
    {
        FluentActions
            .Invoking(() => GitHubReleaseClient.ParseVersionTag(tag))
            .Should()
            .Throw<FormatException>();
    }
}
