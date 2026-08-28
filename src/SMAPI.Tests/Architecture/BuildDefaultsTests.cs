using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace SMAPI.Tests.Architecture;

[TestFixture]
internal class BuildDefaultsTests
{
    [Test]
    public void SmapiBuildDoesNotDeployToGameByDefault()
    {
        string repositoryRoot = FindRepositoryRoot();
        XDocument document = XDocument.Load(Path.Combine(repositoryRoot, "build", "common.targets"));
        XElement[] definitions = document
            .Descendants()
            .Where(element => element.Name.LocalName == "CopyToGameFolder")
            .ToArray();

        definitions.Should().ContainSingle();
        definitions[0].Value.Trim().Should().Be("false");
        XAttribute? condition = definitions[0].Attribute("Condition");
        condition.Should().NotBeNull();
        condition!.Value.Should().Be("'$(CopyToGameFolder)' == ''");
    }

    private static string FindRepositoryRoot()
    {
        for (
            DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
            directory != null;
            directory = directory.Parent
        )
        {
            if (
                File.Exists(Path.Combine(directory.FullName, "build", "common.targets"))
                && File.Exists(
                    Path.Combine(directory.FullName, "src", "SMAPI", "SMAPI.csproj")
                )
            )
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the SMAPI repository root.");
    }
}
