using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;

namespace SMAPI.Tests.Architecture;

[TestFixture]
internal class HarmonyOwnershipTests
{
    [Test]
    public void SmapiCoreDoesNotApplyFirstPartyHarmonyPatches()
    {
        string sourceRoot = FindSourceRoot();
        string[] violations = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return Regex.IsMatch(source, @"\bnew\s+Harmony\s*\(")
                    || Regex.IsMatch(source, @"\.Patch\s*\(");
            })
            .Select(path => Path.GetRelativePath(sourceRoot, path))
            .ToArray();

        violations.Should().BeEmpty(
            "SMAPI supplies Harmony to mods but Cinderbox owns first-party runtime and game patches"
        );
    }

    private static string FindSourceRoot()
    {
        for (DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "src", "SMAPI", "SMAPI.csproj");
            if (File.Exists(candidate))
                return Path.GetDirectoryName(candidate)!;
        }

        throw new InvalidOperationException("Could not locate the SMAPI source root.");
    }
}
