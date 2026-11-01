using Curl.Extended.Protocol.DnsQuery.Wire;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;

[TestClass]
public sealed class DnsQueryEncoderTests
{
    [TestMethod]
    public void Encode_Rfc1035Sections411And412_WritesHeaderWithRdAndOneQuestion()
    {
        DnsOutcome<byte[]> outcome = DnsQueryEncoder.Encode(0xABCD, "example.com", 28, 1, recursionDesired: true);

        byte[] expected = DnsWire.Join([0xAB, 0xCD, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0], DnsWire.Name("example.com"), [0, 28, 0, 1]);
        Assert.IsNull(outcome.Failure);
        CollectionAssert.AreEqual(expected, outcome.Value);
    }

    [TestMethod]
    public void Encode_AnyTypeAndClassWithoutRecursion_WritesThemAndClearsRd()
    {
        byte[] query = DnsQueryEncoder.Encode(1, "example.com.", 65, 3, recursionDesired: false).Value!;

        byte[] expected = DnsWire.Join([0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0], DnsWire.Name("example.com"), [0, 65, 0, 3]);
        CollectionAssert.AreEqual(expected, query);
    }

    [TestMethod]
    [DataRow(".")]
    [DataRow("")]
    public void Encode_RootName_WritesTheZeroOctetOnly(string name)
    {
        byte[] query = DnsQueryEncoder.Encode(0, name, 2, 1, recursionDesired: true).Value!;

        CollectionAssert.AreEqual(new byte[] { 0, 0, 2, 0, 1 }, query[12..]);
    }

    [TestMethod]
    public void Encode_EscapedDotAndDecimalOctet_PutsThemInsideOneLabel()
    {
        byte[] query = DnsQueryEncoder.Encode(0, @"a\.b\065\256\2", 1, 1, recursionDesired: true).Value!;

        CollectionAssert.AreEqual(new byte[] { 8, (byte)'a', (byte)'.', (byte)'b', 65, (byte)'2', (byte)'5', (byte)'6', (byte)'2', 0 }, query[12..^4]);
    }

    [TestMethod]
    [DataRow("a..b", "The DNS name has an empty label.")]
    [DataRow(".com", "The DNS name has an empty label.")]
    [DataRow(@"a\", "The DNS name ends in a backslash.")]
    [DataRow("café", "The DNS name holds a character outside ASCII.")]
    public void Encode_MalformedName_Fails(string name, string expected)
    {
        DnsOutcome<byte[]> outcome = DnsQueryEncoder.Encode(0, name, 1, 1, recursionDesired: true);

        Assert.IsNull(outcome.Value);
        Assert.AreEqual(expected, outcome.Failure);
    }

    [TestMethod]
    public void Encode_LabelOf64Octets_FailsAsRfc1035Section232Allows63()
    {
        Assert.IsNotNull(DnsQueryEncoder.Encode(0, new string('a', 63), 1, 1, recursionDesired: true).Value);
        Assert.AreEqual(
            "The DNS name has a label longer than 63 octets.",
            DnsQueryEncoder.Encode(0, new string('a', 64) + ".com", 1, 1, recursionDesired: true).Failure);
    }

    [TestMethod]
    public void Encode_NameOver255Octets_FailsAsRfc1035Section232Allows255()
    {
        string label = new('a', 63);
        string longest = $"{label}.{label}.{label}.{new string('a', 61)}";

        Assert.IsNotNull(DnsQueryEncoder.Encode(0, longest, 1, 1, recursionDesired: true).Value);
        Assert.AreEqual(
            "The DNS name is longer than 255 octets.",
            DnsQueryEncoder.Encode(0, longest + "a", 1, 1, recursionDesired: true).Failure);
    }
}
