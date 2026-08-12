using NUnit.Framework;
using StardewModdingAPI.Framework.Threading;

namespace SMAPI.Tests.Core;

[TestFixture]
internal class AudioDecodeWorkerPolicyTests
{
    [TestCase(-1, false, ExpectedResult = 1)]
    [TestCase(0, false, ExpectedResult = 1)]
    [TestCase(1, false, ExpectedResult = 1)]
    [TestCase(2, false, ExpectedResult = 1)]
    [TestCase(3, false, ExpectedResult = 1)]
    [TestCase(4, false, ExpectedResult = 2)]
    [TestCase(5, false, ExpectedResult = 2)]
    [TestCase(6, false, ExpectedResult = 3)]
    [TestCase(7, false, ExpectedResult = 3)]
    [TestCase(8, false, ExpectedResult = 4)]
    [TestCase(64, false, ExpectedResult = 4)]
    [TestCase(1, true, ExpectedResult = 1)]
    [TestCase(8, true, ExpectedResult = 1)]
    [TestCase(64, true, ExpectedResult = 1)]
    public int CalculateWorkerCount_ReturnsExpectedResult(
        int processorCount,
        bool limitForMemory
    )
    {
        return AudioDecodeWorkerPolicy.CalculateWorkerCount(
            processorCount,
            limitForMemory
        );
    }
}
