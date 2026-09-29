using System.Net;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsServerQuery" /> to the queries curl 8.22.0's c-ares 1.34.8 build sent
/// (measured, BL-694), and to how a reply is matched to its query and read.
/// </summary>
[TestClass]
public sealed class DnsServerQueryTests
{
    private static readonly byte[] Cookie = Convert.FromHexString("355C114D1EDB1FDF");

    [TestMethod]
    public void Build_OverUdp_CarriesTheIdAndAnOptRecordWithTheClientCookie()
    {
        var query = DnsServerQuery.Build("bl694.example", DnsRecordType.Aaaa, 0x6604, Cookie);

        Assert.AreEqual(
            "66040100000100000000000105626C363934076578616D706C6500001C000100002904D000000000000C000A0008355C114D1EDB1FDF",
            Convert.ToHexString(query.Bytes));
    }

    [TestMethod]
    public void Build_OverTcp_CarriesAnEmptyOptRecord()
    {
        var query = DnsServerQuery.Build("bl694.example", DnsRecordType.A, 0xC073, []);

        Assert.AreEqual(
            "C0730100000100000000000105626C363934076578616D706C65000001000100002904D0000000000000",
            Convert.ToHexString(query.Bytes));
    }

    [TestMethod]
    public void Build_ANameTheEncoderRefuses_ReturnsItsFailure()
    {
        var query = DnsServerQuery.Build("a..b", DnsRecordType.A, 1, Cookie);

        Assert.AreEqual(DnsMessageFailure.BadLabel, query.Failure);
        Assert.AreEqual(0, query.Bytes.Length);
    }

    [TestMethod]
    public void Match_TheReply_IsComplete()
    {
        var query = Query();

        Assert.AreEqual(DnsReplyMatch.Complete, DnsServerQuery.Match(query, DnsTestReplies.Answer(query, [IPAddress.Loopback])));
    }

    [TestMethod]
    public void Match_TheReplyWithTheNameInAnotherCase_IsComplete()
    {
        var query = Query();
        var reply = DnsTestReplies.Answer(query, [IPAddress.Loopback]);
        reply[13] = (byte)'A';

        Assert.AreEqual(DnsReplyMatch.Complete, DnsServerQuery.Match(query, reply));
    }

    [TestMethod]
    public void Match_ATruncatedReply_IsTruncated()
    {
        var query = Query();

        Assert.AreEqual(DnsReplyMatch.Truncated, DnsServerQuery.Match(query, DnsTestReplies.Answer(query, [], truncated: true)));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(14)]
    public void Match_AReplyWithAnotherIdOrQuestionOrNotAResponse_IsAMismatch(int changedIndex)
    {
        var query = Query();
        var reply = DnsTestReplies.Answer(query, [IPAddress.Loopback]);
        reply[changedIndex] ^= 0x80;

        Assert.AreEqual(DnsReplyMatch.Mismatch, DnsServerQuery.Match(query, reply));
    }

    [TestMethod]
    public void Match_AReplyShorterThanTheQuestion_IsAMismatch()
    {
        var query = Query();

        Assert.AreEqual(DnsReplyMatch.Mismatch, DnsServerQuery.Match(query, query.AsSpan(0, 20)));
    }

    [TestMethod]
    [DataRow(1, DnsLookupFailure.FormatError)]
    [DataRow(2, DnsLookupFailure.ServerFailure)]
    [DataRow(3, DnsLookupFailure.NotFound)]
    [DataRow(4, DnsLookupFailure.NotImplemented)]
    [DataRow(5, DnsLookupFailure.Refused)]
    [DataRow(9, DnsLookupFailure.ServerFailure)]
    public void Read_AResponseCode_IsItsFailure(int responseCode, DnsLookupFailure expected)
    {
        var query = Query();

        var outcome = DnsServerQuery.Read(DnsTestReplies.Answer(query, [], responseCode), DnsRecordType.A);

        Assert.AreEqual(expected, outcome.Failure);
        Assert.IsNull(outcome.Answer);
    }

    [TestMethod]
    public void Read_AnAnswer_ReturnsItsAddresses()
    {
        var query = Query();

        var outcome = DnsServerQuery.Read(DnsTestReplies.Answer(query, [IPAddress.Loopback]), DnsRecordType.A);

        Assert.AreEqual(DnsLookupFailure.None, outcome.Failure);
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, outcome.Answer!.Addresses.ToArray());
        Assert.IsTrue(outcome.IsFinal);
    }

    [TestMethod]
    public void Read_NoErrorWithoutRecords_IsNoData()
    {
        var query = Query();

        var outcome = DnsServerQuery.Read(DnsTestReplies.Answer(query, []), DnsRecordType.A);

        Assert.AreEqual(DnsLookupFailure.NoData, outcome.Failure);
        Assert.IsTrue(outcome.IsFinal);
    }

    [TestMethod]
    public void Read_OnlyACanonicalName_IsNoData()
    {
        var query = Query();
        byte[] cname = [0xC0, 0x0C, 0, 5, 0, 1, 0, 0, 0, 60, 0, 2, 0xC0, 0x0C];
        var reply = DnsTestReplies.Answer(query, []);
        reply[7] = 1;

        var outcome = DnsServerQuery.Read([.. reply, .. cname], DnsRecordType.A);

        Assert.AreEqual(DnsLookupFailure.NoData, outcome.Failure);
    }

    [TestMethod]
    [DataRow("C00C001C00010000003C001000000000000000000000000000000001", DisplayName = "an AAAA record")]
    [DataRow("C00C000100030000003C00047F000001", DisplayName = "an A record of class CH")]
    public void Read_ARecordOfAnotherTypeOrClass_IsNoData(string recordHex)
    {
        var reply = DnsTestReplies.Answer(Query(), []);
        reply[7] = 1;

        var outcome = DnsServerQuery.Read([.. reply, .. Convert.FromHexString(recordHex)], DnsRecordType.A);

        Assert.AreEqual(DnsLookupFailure.NoData, outcome.Failure);
    }

    [TestMethod]
    public void Read_AReplyThatDoesNotDecode_IsABadReply()
    {
        var query = Query();

        var outcome = DnsServerQuery.Read(DnsTestReplies.Answer(query, [IPAddress.IPv6Loopback]).Concat(new byte[] { 1 }).ToArray(), DnsRecordType.A);

        Assert.AreEqual(DnsLookupFailure.BadReply, outcome.Failure);
        Assert.IsFalse(outcome.IsFinal);
    }

    private static byte[] Query() => DnsServerQuery.Build("ab.example", DnsRecordType.A, 0x1234, Cookie).Bytes;
}
