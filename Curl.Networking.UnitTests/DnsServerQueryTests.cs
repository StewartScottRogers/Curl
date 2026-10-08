using System.Net;

using Curl.Networking.Fakes;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsServerQuery" /> to the queries curl 8.22.0's c-ares 1.34.8 build sent
/// (measured, BL-694), and to how a reply is matched to its query and read.
/// </summary>
[TestClass]
public sealed class DnsServerQueryTests
{
    private static readonly byte[] Cookie = Convert.FromHexString("355C114D1EDB1FDF");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Build_OverUdp_CarriesTheIdAndAnOptRecordWithTheClientCookie()
    {
        Diagnostics.Arrange("name", "bl694.example");
        Diagnostics.Arrange("record type", DnsRecordType.Aaaa);
        Diagnostics.Arrange("id", "0x6604");
        Diagnostics.Bytes("client cookie", Cookie);

        var query = DnsServerQuery.Build("bl694.example", DnsRecordType.Aaaa, 0x6604, Cookie);

        Diagnostics.Act("failure", query.Failure);
        Diagnostics.Bytes("query", query.Bytes);
        Diagnostics.Diff(
            "query",
            Convert.FromHexString("66040100000100000000000105626C363934076578616D706C6500001C000100002904D000000000000C000A0008355C114D1EDB1FDF"),
            query.Bytes);
        Assert.AreEqual(
            "66040100000100000000000105626C363934076578616D706C6500001C000100002904D000000000000C000A0008355C114D1EDB1FDF",
            Convert.ToHexString(query.Bytes));
    }

    [TestMethod]
    public void Build_OverTcp_CarriesAnEmptyOptRecord()
    {
        Diagnostics.Arrange("name", "bl694.example");
        Diagnostics.Arrange("record type", DnsRecordType.A);
        Diagnostics.Arrange("id", "0xC073");
        Diagnostics.Arrange("client cookie", "(none)");

        var query = DnsServerQuery.Build("bl694.example", DnsRecordType.A, 0xC073, []);

        Diagnostics.Act("failure", query.Failure);
        Diagnostics.Bytes("query", query.Bytes);
        Diagnostics.Diff(
            "query",
            Convert.FromHexString("C0730100000100000000000105626C363934076578616D706C65000001000100002904D0000000000000"),
            query.Bytes);
        Assert.AreEqual(
            "C0730100000100000000000105626C363934076578616D706C65000001000100002904D0000000000000",
            Convert.ToHexString(query.Bytes));
    }

    [TestMethod]
    public void Build_ANameTheEncoderRefuses_ReturnsItsFailure()
    {
        Diagnostics.Arrange("name", "a..b");
        Diagnostics.Arrange("record type", DnsRecordType.A);

        var query = DnsServerQuery.Build("a..b", DnsRecordType.A, 1, Cookie);

        Diagnostics.Act("failure", query.Failure);
        Diagnostics.Act("query length", query.Bytes.Length);
        Diagnostics.Assert("failure", DnsMessageFailure.BadLabel, query.Failure);
        Diagnostics.Assert("query length", 0, query.Bytes.Length);
        Assert.AreEqual(DnsMessageFailure.BadLabel, query.Failure);
        Assert.AreEqual(0, query.Bytes.Length);
    }

    [TestMethod]
    public void Match_TheReply_IsComplete()
    {
        var query = Query();
        var reply = DnsTestReplies.Answer(query, [IPAddress.Loopback]);
        Diagnostics.Bytes("query", query);
        Diagnostics.Bytes("reply", reply);
        Diagnostics.Arrange("reply answer", IPAddress.Loopback);

        var match = DnsServerQuery.Match(query, reply);

        Diagnostics.Act("match", match);
        Diagnostics.Assert("match", DnsReplyMatch.Complete, match);
        Assert.AreEqual(DnsReplyMatch.Complete, DnsServerQuery.Match(query, DnsTestReplies.Answer(query, [IPAddress.Loopback])));
    }

    [TestMethod]
    public void Match_TheReplyWithTheNameInAnotherCase_IsComplete()
    {
        var query = Query();
        var reply = DnsTestReplies.Answer(query, [IPAddress.Loopback]);
        reply[13] = (byte)'A';
        Diagnostics.Bytes("query", query);
        Diagnostics.Bytes("reply", reply);
        Diagnostics.Arrange("changed reply byte 13", "'A'");

        var match = DnsServerQuery.Match(query, reply);

        Diagnostics.Act("match", match);
        Diagnostics.Assert("match", DnsReplyMatch.Complete, match);
        Assert.AreEqual(DnsReplyMatch.Complete, DnsServerQuery.Match(query, reply));
    }

    [TestMethod]
    public void Match_ATruncatedReply_IsTruncated()
    {
        var query = Query();
        var reply = DnsTestReplies.Answer(query, [], truncated: true);
        Diagnostics.Bytes("query", query);
        Diagnostics.Bytes("reply", reply);
        Diagnostics.Arrange("reply truncated", true);

        var match = DnsServerQuery.Match(query, reply);

        Diagnostics.Act("match", match);
        Diagnostics.Assert("match", DnsReplyMatch.Truncated, match);
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
        Diagnostics.Arrange("flipped reply byte", changedIndex);
        Diagnostics.Bytes("query", query);
        Diagnostics.Bytes("reply", reply);

        var match = DnsServerQuery.Match(query, reply);

        Diagnostics.Act("match", match);
        Diagnostics.Assert("match", DnsReplyMatch.Mismatch, match);
        Assert.AreEqual(DnsReplyMatch.Mismatch, DnsServerQuery.Match(query, reply));
    }

    [TestMethod]
    public void Match_AReplyShorterThanTheQuestion_IsAMismatch()
    {
        var query = Query();
        Diagnostics.Bytes("query", query);
        Diagnostics.Arrange("reply", "the query's first 20 bytes");

        var match = DnsServerQuery.Match(query, query.AsSpan(0, 20));

        Diagnostics.Act("match", match);
        Diagnostics.Assert("match", DnsReplyMatch.Mismatch, match);
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
        var reply = DnsTestReplies.Answer(query, [], responseCode);
        Diagnostics.Arrange("response code", responseCode);
        Diagnostics.Bytes("reply", reply);

        var outcome = DnsServerQuery.Read(reply, DnsRecordType.A);

        WriteOutcome(outcome);
        Diagnostics.Assert("failure", expected, outcome.Failure);
        Diagnostics.Assert("answer is null", true, outcome.Answer is null);
        Assert.AreEqual(expected, outcome.Failure);
        Assert.IsNull(outcome.Answer);
    }

    [TestMethod]
    public void Read_AnAnswer_ReturnsItsAddresses()
    {
        var query = Query();
        var reply = DnsTestReplies.Answer(query, [IPAddress.Loopback]);
        Diagnostics.Arrange("reply answer", IPAddress.Loopback);
        Diagnostics.Bytes("reply", reply);

        var outcome = DnsServerQuery.Read(reply, DnsRecordType.A);

        WriteOutcome(outcome);
        Diagnostics.Assert("failure", DnsLookupFailure.None, outcome.Failure);
        Diagnostics.Assert("addresses", IPAddress.Loopback, outcome.Answer is null ? "(null)" : string.Join(", ", outcome.Answer.Addresses));
        Diagnostics.Assert("is final", true, outcome.IsFinal);
        Assert.AreEqual(DnsLookupFailure.None, outcome.Failure);
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, outcome.Answer!.Addresses.ToArray());
        Assert.IsTrue(outcome.IsFinal);
    }

    [TestMethod]
    public void Read_NoErrorWithoutRecords_IsNoData()
    {
        var query = Query();
        var reply = DnsTestReplies.Answer(query, []);
        Diagnostics.Arrange("reply answers", "(none)");
        Diagnostics.Bytes("reply", reply);

        var outcome = DnsServerQuery.Read(reply, DnsRecordType.A);

        WriteOutcome(outcome);
        Diagnostics.Assert("failure", DnsLookupFailure.NoData, outcome.Failure);
        Diagnostics.Assert("is final", true, outcome.IsFinal);
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
        byte[] fullReply = [.. reply, .. cname];
        Diagnostics.Arrange("reply answers", "one CNAME record");
        Diagnostics.Bytes("reply", fullReply);

        var outcome = DnsServerQuery.Read(fullReply, DnsRecordType.A);

        WriteOutcome(outcome);
        Diagnostics.Assert("failure", DnsLookupFailure.NoData, outcome.Failure);
        Assert.AreEqual(DnsLookupFailure.NoData, outcome.Failure);
    }

    [TestMethod]
    [DataRow("C00C001C00010000003C001000000000000000000000000000000001", DisplayName = "an AAAA record")]
    [DataRow("C00C000100030000003C00047F000001", DisplayName = "an A record of class CH")]
    public void Read_ARecordOfAnotherTypeOrClass_IsNoData(string recordHex)
    {
        var reply = DnsTestReplies.Answer(Query(), []);
        reply[7] = 1;
        byte[] fullReply = [.. reply, .. Convert.FromHexString(recordHex)];
        Diagnostics.Arrange("answer record", recordHex);
        Diagnostics.Bytes("reply", fullReply);

        var outcome = DnsServerQuery.Read(fullReply, DnsRecordType.A);

        WriteOutcome(outcome);
        Diagnostics.Assert("failure", DnsLookupFailure.NoData, outcome.Failure);
        Assert.AreEqual(DnsLookupFailure.NoData, outcome.Failure);
    }

    [TestMethod]
    public void Read_AReplyThatDoesNotDecode_IsABadReply()
    {
        var query = Query();
        var reply = DnsTestReplies.Answer(query, [IPAddress.IPv6Loopback]).Concat(new byte[] { 1 }).ToArray();
        Diagnostics.Arrange("reply", "an AAAA answer to an A query, one stray byte after it");
        Diagnostics.Bytes("reply", reply);

        var outcome = DnsServerQuery.Read(reply, DnsRecordType.A);

        WriteOutcome(outcome);
        Diagnostics.Assert("failure", DnsLookupFailure.BadReply, outcome.Failure);
        Diagnostics.Assert("is final", false, outcome.IsFinal);
        Assert.AreEqual(DnsLookupFailure.BadReply, outcome.Failure);
        Assert.IsFalse(outcome.IsFinal);
    }

    private static byte[] Query() => DnsServerQuery.Build("ab.example", DnsRecordType.A, 0x1234, Cookie).Bytes;

    private void WriteOutcome(DnsQueryOutcome outcome)
    {
        Diagnostics.Act("failure", outcome.Failure);
        Diagnostics.Act("addresses", outcome.Answer is null ? "(null)" : string.Join(", ", outcome.Answer.Addresses));
        Diagnostics.Act("is final", outcome.IsFinal);
    }
}
