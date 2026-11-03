using Curl.Extended.Protocol.DnsQuery.Wire;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;

/// <summary>Checks the EDNS(0) Padding option (RFC 7830) and the 128-octet query block length (RFC 8467 section 4.1).</summary>
[TestClass]
public sealed class DnsEdnsPaddingTests
{
    [TestMethod]
    public void Pad_Rfc7830_AppendsAnOptRecordWithOnePaddingOptionToABlockOf128()
    {
        byte[] query = DnsQueryEncoder.Encode(0xABCD, "example.com", 1, 1, recursionDesired: true).Value!;

        byte[] padded = DnsEdnsPadding.Pad(query);

        int padding = 128 - query.Length - 15;
        byte[] expectedRecord = DnsWire.Join([0, 0, 41, 0x04, 0xD0, 0, 0, 0, 0], DnsWire.UInt16(4 + padding), [0, 12], DnsWire.UInt16(padding), new byte[padding]);
        Assert.HasCount(128, padded);
        CollectionAssert.AreEqual(query[..10], padded[..10]);
        CollectionAssert.AreEqual(new byte[] { 0, 1 }, padded[10..12]);
        CollectionAssert.AreEqual(query[12..], padded[12..query.Length]);
        CollectionAssert.AreEqual(expectedRecord, padded[query.Length..]);
    }

    [TestMethod]
    public void Pad_QueryEndingOnABlockBoundary_AddsAnEmptyPaddingOption()
    {
        byte[] query = new byte[113];

        byte[] padded = DnsEdnsPadding.Pad(query);

        Assert.HasCount(128, padded);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 12, 0, 0 }, padded[122..]);
    }

    [TestMethod]
    public void Pad_LongQuery_PadsToTheNextMultipleOf128()
    {
        byte[] query = DnsQueryEncoder.Encode(1, new string('a', 63) + "." + new string('b', 63), 1, 1, recursionDesired: true).Value!;

        Assert.HasCount(256, DnsEdnsPadding.Pad(query));
    }
}
