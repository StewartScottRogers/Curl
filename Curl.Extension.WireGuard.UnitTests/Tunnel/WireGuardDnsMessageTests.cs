using System.Net;
using Curl.Extension.WireGuard.Tunnel;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>Checks the DNS over TCP query and answer against RFC 1035 sections 4.1 and 4.2.2 and RFC 3596.</summary>
[TestClass]
public sealed class WireGuardDnsMessageTests
{
    private static readonly byte[] ExampleComName = [7, .. "example"u8, 3, .. "com"u8, 0];

    private static readonly byte[] PointerToQuestionName = [0xC0, 0x0C];

    [TestMethod]
    public void EncodeQuery_HostName_WritesTheLengthPrefixARecursiveHeaderAndOneQuestion()
    {
        byte[] query = WireGuardDnsMessage.EncodeQuery(0x1234, "example.com.", WireGuardDnsMessage.AddressType);

        byte[] expected = [0, 29, 0x12, 0x34, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0, .. ExampleComName, 0, 1, 0, 1];
        CollectionAssert.AreEqual(expected, query);
    }

    [TestMethod]
    public void EncodeQuery_InternationalName_SendsItsALabel()
    {
        byte[] query = WireGuardDnsMessage.EncodeQuery(1, "bücher.example", WireGuardDnsMessage.Ipv6AddressType);

        byte[] expected = [0, 39, 0, 1, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0, 13, .. "xn--bcher-kva"u8, 7, .. "example"u8, 0, 0, 28, 0, 1];
        CollectionAssert.AreEqual(expected, query);
    }

    [TestMethod]
    public void EncodeQuery_EmptyLabel_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => WireGuardDnsMessage.EncodeQuery(1, "a..b", WireGuardDnsMessage.AddressType));

    [TestMethod]
    public void AddressesIn_CnameThenAddressRecords_GivesTheAddressesOfTheTypeAskedFor()
    {
        byte[] response = Response(
            0x4242,
            0x8180,
            Record(PointerToQuestionName, 5, 1, [3, .. "www"u8, 0xC0, 0x0C]),
            Record([3, .. "www"u8, 0xC0, 0x0C], WireGuardDnsMessage.AddressType, 1, [10, 0, 0, 1]),
            Record(PointerToQuestionName, WireGuardDnsMessage.AddressType, 1, [10, 0, 0, 7]));

        var addresses = WireGuardDnsMessage.AddressesIn(response, 0x4242, WireGuardDnsMessage.AddressType);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.7") }, addresses.ToArray());
    }

    [TestMethod]
    public void AddressesIn_Ipv6AddressRecord_GivesTheIpv6Address()
    {
        byte[] response = Response(7, 0x8180, Record(PointerToQuestionName, WireGuardDnsMessage.Ipv6AddressType, 1, IPAddress.Parse("fd00::1").GetAddressBytes()));

        var addresses = WireGuardDnsMessage.AddressesIn(response, 7, WireGuardDnsMessage.Ipv6AddressType);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("fd00::1") }, addresses.ToArray());
    }

    [TestMethod]
    public void AddressesIn_RecordOfAnotherClassOrLength_IsSkipped()
    {
        byte[] response = Response(
            7,
            0x8180,
            Record(PointerToQuestionName, WireGuardDnsMessage.AddressType, 3, [10, 0, 0, 1]),
            Record(PointerToQuestionName, WireGuardDnsMessage.AddressType, 1, new byte[16]),
            Record(PointerToQuestionName, WireGuardDnsMessage.Ipv6AddressType, 1, [10, 0, 0, 1]));

        Assert.IsEmpty(WireGuardDnsMessage.AddressesIn(response, 7, WireGuardDnsMessage.AddressType));
        Assert.IsEmpty(WireGuardDnsMessage.AddressesIn(response, 7, WireGuardDnsMessage.Ipv6AddressType));
    }

    [TestMethod]
    [DataRow(8, (ushort)0x8180, DisplayName = "another ID")]
    [DataRow(7, (ushort)0x0100, DisplayName = "a query, not a response")]
    [DataRow(7, (ushort)0x8183, DisplayName = "NXDOMAIN")]
    public void AddressesIn_NotASuccessfulAnswerToTheQuery_GivesNothing(int id, ushort flags)
    {
        byte[] response = Response((ushort)id, flags, Record(PointerToQuestionName, WireGuardDnsMessage.AddressType, 1, [10, 0, 0, 1]));

        Assert.IsEmpty(WireGuardDnsMessage.AddressesIn(response, 7, WireGuardDnsMessage.AddressType));
    }

    [TestMethod]
    public void AddressesIn_ShorterThanAHeader_GivesNothing() =>
        Assert.IsEmpty(WireGuardDnsMessage.AddressesIn(new byte[11], 0, WireGuardDnsMessage.AddressType));

    [TestMethod]
    public void AddressesIn_QuestionNameRunsPastTheEnd_GivesNothing()
    {
        byte[] response = [0, 7, 0x81, 0x80, 0, 2, 0, 1, 0, 0, 0, 0, 3, (byte)'a', (byte)'b'];

        Assert.IsEmpty(WireGuardDnsMessage.AddressesIn(response, 7, WireGuardDnsMessage.AddressType));
    }

    [TestMethod]
    [DataRow(0, DisplayName = "record missing")]
    [DataRow(3, DisplayName = "record fields cut short")]
    [DataRow(14, DisplayName = "record data cut short")]
    public void AddressesIn_AnswerRecordRunsPastTheEnd_KeepsTheRecordsBeforeIt(int bytesOfSecondRecord)
    {
        byte[] whole = Response(
            7,
            0x8180,
            Record(PointerToQuestionName, WireGuardDnsMessage.AddressType, 1, [10, 0, 0, 1]),
            Record(PointerToQuestionName, WireGuardDnsMessage.AddressType, 1, [10, 0, 0, 2]));
        byte[] response = [.. whole[..^(16 - bytesOfSecondRecord)]];
        response[7] = 3;

        var addresses = WireGuardDnsMessage.AddressesIn(response, 7, WireGuardDnsMessage.AddressType);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("10.0.0.1") }, addresses.ToArray());
    }

    /// <summary>Builds a response to an <c>example.com</c> question holding <paramref name="answers" />, without the TCP length prefix.</summary>
    internal static byte[] Response(ushort id, ushort flags, params byte[][] answers) =>
        [(byte)(id >> 8), (byte)id, (byte)(flags >> 8), (byte)flags, 0, 1, 0, (byte)answers.Length, 0, 0, 0, 0, .. ExampleComName, 0, 1, 0, 1, .. answers.SelectMany(answer => answer)];

    /// <summary>Builds a resource record with a TTL of 60 seconds.</summary>
    internal static byte[] Record(byte[] name, ushort type, ushort recordClass, byte[] data) =>
        [.. name, (byte)(type >> 8), (byte)type, (byte)(recordClass >> 8), (byte)recordClass, 0, 0, 0, 60, (byte)(data.Length >> 8), (byte)data.Length, .. data];
}
