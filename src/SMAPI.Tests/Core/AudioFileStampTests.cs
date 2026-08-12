using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using StardewModdingAPI.Framework.Content;

namespace SMAPI.Tests.Core;

[TestFixture]
internal class AudioFileStampTests
{
    [Test]
    public void TryRead_ReturnsSameStampWhenSourceIsUnchanged()
    {
        string path = CreateTempFile([1, 2, 3]);
        try
        {
            AudioFileStamp.TryRead(path, out var first).Should().BeTrue();
            AudioFileStamp.TryRead(path, out var second).Should().BeTrue();

            second.Should().Be(first);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void TryRead_DetectsLengthChangeWhenTimestampIsUnchanged()
    {
        string path = CreateTempFile([1, 2, 3]);
        try
        {
            DateTime timestamp = File.GetLastWriteTimeUtc(path);
            AudioFileStamp.TryRead(path, out var first).Should().BeTrue();

            File.WriteAllBytes(path, [1, 2, 3, 4]);
            File.SetLastWriteTimeUtc(path, timestamp);
            AudioFileStamp.TryRead(path, out var second).Should().BeTrue();

            second.Should().NotBe(first);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void TryRead_DetectsTimestampChangeWhenLengthIsUnchanged()
    {
        string path = CreateTempFile([1, 2, 3]);
        try
        {
            AudioFileStamp.TryRead(path, out var first).Should().BeTrue();

            File.SetLastWriteTimeUtc(path, new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            AudioFileStamp.TryRead(path, out var second).Should().BeTrue();

            second.Should().NotBe(first);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void TryRead_ReturnsFalseForMissingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"smapi-audio-{Guid.NewGuid():N}.ogg");

        AudioFileStamp.TryRead(path, out _).Should().BeFalse();
    }

    private static string CreateTempFile(byte[] contents)
    {
        string path = Path.Combine(Path.GetTempPath(), $"smapi-audio-{Guid.NewGuid():N}.ogg");
        File.WriteAllBytes(path, contents);
        return path;
    }
}
