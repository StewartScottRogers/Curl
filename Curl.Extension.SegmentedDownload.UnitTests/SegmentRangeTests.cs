namespace Curl.Extension.SegmentedDownload.UnitTests;

[TestClass]
public sealed class SegmentRangeTests
{
    [TestMethod]
    public void Split_ALengthThatDoesNotDivideEvenly_GivesConsecutiveRangesWithTheLongerOnesFirst_Rfc9110Section14_1_1()
    {
        var ranges = SegmentRange.Split(10, 3);

        CollectionAssert.AreEqual(new[] { new SegmentRange(0, 3), new SegmentRange(4, 6), new SegmentRange(7, 9) }, ranges.ToArray());
        Assert.AreEqual("Range: bytes=4-6", ranges[1].RangeHeader);
        Assert.AreEqual(3, ranges[1].Length);
    }

    [TestMethod]
    public void Split_FewerBytesThanSegments_GivesOneRangePerByte()
    {
        var ranges = SegmentRange.Split(2, 8);

        CollectionAssert.AreEqual(new[] { new SegmentRange(0, 0), new SegmentRange(1, 1) }, ranges.ToArray());
    }
}
