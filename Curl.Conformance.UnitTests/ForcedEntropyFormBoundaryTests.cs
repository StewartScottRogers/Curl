namespace Curl.Conformance;

[TestClass]
public sealed class ForcedEntropyFormBoundaryTests
{
    [TestMethod]
    public void Of_UpstreamCasesEntropy_GivesTest1972sBoundary()
    {
        Assert.AreEqual("------------------------qrstuvwxyz0123456789AB", ForcedEntropyFormBoundary.Of("12345678"));
    }

    [TestMethod]
    public void Of_ValuesAtTheTopOfTheRange_SkipsTheRejectedOnesAndWrapsToZero()
    {
        // "\xff\xff\xff\xfc" is uint.MaxValue - 3, past the largest multiple of 62, so the draws skip to 0, 1, 2 and on.
        Assert.AreEqual("------------------------ABCDEFGHIJKLMNOPQRSTUV", ForcedEntropyFormBoundary.Of("ÿÿÿü"));
    }
}
