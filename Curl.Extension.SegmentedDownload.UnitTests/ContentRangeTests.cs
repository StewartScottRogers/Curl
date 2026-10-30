namespace Curl.Extension.SegmentedDownload.UnitTests;

[TestClass]
public sealed class ContentRangeTests
{
    [TestMethod]
    public void Parse_ASatisfiedByteRange_GivesTheRangeAndCompleteLength_Rfc9110Section14_4()
    {
        Assert.AreEqual(new ContentRange(new SegmentRange(0, 0), 26), ContentRange.Parse("bytes 0-0/26"));
        Assert.AreEqual(new ContentRange(new SegmentRange(42, 1233), 1234), ContentRange.Parse("Bytes 42-1233/1234"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("items 0-0/26")]
    [DataRow("bytes */26")]
    [DataRow("bytes 0-0/*")]
    [DataRow("bytes 0-0")]
    [DataRow("bytes 0/26")]
    [DataRow("bytes 5/2-6")]
    [DataRow("bytes x-0/26")]
    [DataRow("bytes 0-x/26")]
    [DataRow("bytes 5-4/26")]
    [DataRow("bytes 0-26/26")]
    public void Parse_AnythingButASatisfiedByteRangeWithAKnownLength_GivesNone_Rfc9110Section14_4(string? value)
    {
        Assert.IsNull(ContentRange.Parse(value));
    }
}
