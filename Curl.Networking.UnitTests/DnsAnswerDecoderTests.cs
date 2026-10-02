using System.Net;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsAnswerDecoder" /> to curl 8.21.0's <c>doh_resp_decode</c>: the answers
/// measured in BL-639 (ADR-0152) and the compression, CNAME and failure rules of that function.
/// Messages are written in hex: <c>Header</c>, then the sections byte for byte.
/// </summary>
[TestClass]
public sealed class DnsAnswerDecoderTests
{
    /// <summary>The question for <c>example.test</c> type A, 18 bytes at offset 12.</summary>
    private const string QuestionA = "076578616D706C6504746573740000010001";

    /// <summary>The question for <c>example.test</c> type AAAA.</summary>
    private const string QuestionAaaa = "076578616D706C65047465737400001C0001";

    /// <summary>The question for <c>example.test</c> type HTTPS (65).</summary>
    private const string QuestionHttps = "076578616D706C6504746573740000410001";

    /// <summary>An A record for the question's name (a pointer to offset 12), TTL 60, 127.0.0.1.</summary>
    private const string AnswerA = "C00C000100010000003C00047F000001";

    /// <summary>An NS record for the question's name pointing at the same name.</summary>
    private const string AuthorityNs = "C00C000200010000003C0002C00C";

    /// <summary>An OPT pseudo-record: root name, type 41, class 4096, no data.</summary>
    private const string AdditionalOpt = "0000291000000000000000";

    private static readonly byte[] MeasuredAnswer = Message(Header("8180", 1, 1) + QuestionA + AnswerA);

    [TestMethod]
    public void Decode_TheMeasuredAAnswer_ReturnsItsAddressAndTtl()
    {
        // The DoH server answered example.test A with one record, 127.0.0.1 TTL 60 (ADR-0152).
        var answer = DnsAnswerDecoder.Decode(MeasuredAnswer, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, answer.Addresses.ToArray());
        Assert.IsEmpty(answer.CanonicalNames);
        Assert.AreEqual(60u, answer.TimeToLiveSeconds);
    }

    [TestMethod]
    public void Decode_AnAaaaAnswer_ReturnsItsIPv6Address()
    {
        var message = Message(Header("8180", 1, 1) + QuestionAaaa
            + "C00C001C00010000012C0010" + "00000000000000000000000000000001");

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.Aaaa);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback }, answer.Addresses.ToArray());
        Assert.AreEqual(300u, answer.TimeToLiveSeconds);
    }

    [TestMethod]
    public void Decode_AnAnswerWithAuthorityAndAdditionalRecords_SkipsThem()
    {
        var message = Message(Header("8180", 1, 1, 1, 1) + QuestionA + AnswerA + AuthorityNs + AdditionalOpt);

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, answer.Addresses.ToArray());
    }

    [TestMethod]
    public void Decode_ACnameChain_FollowsCompressedNamesAndKeepsTheSmallestTtl()
    {
        // www.example.test CNAME a.example.test (TTL 3600), a.example.test CNAME b.example.test
        // (TTL 300), b.example.test A 192.0.2.1 (TTL 120). "example.test" is at offset 16, the
        // first CNAME's data at 46 and the second's at 62; each later owner name points back.
        var message = Message(Header("8180", 1, 3)
            + "03777777076578616D706C6504746573740000010001"
            + "C00C0005000100000E1000040161C010"
            + "C02E00050001" + "0000012C00040162C010"
            + "C03E00010001" + "000000780004C0000201");

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        CollectionAssert.AreEqual(new[] { "a.example.test", "b.example.test" }, answer.CanonicalNames.ToArray());
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.1") }, answer.Addresses.ToArray());
        Assert.AreEqual(120u, answer.TimeToLiveSeconds);
    }

    [TestMethod]
    public void Decode_ACnameWithNoAddress_IsContent()
    {
        var message = Message(Header("8180", 1, 1) + QuestionA + "C00C0005000100000E100002" + "C00C");

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        CollectionAssert.AreEqual(new[] { "example.test" }, answer.CanonicalNames.ToArray());
        Assert.IsEmpty(answer.Addresses);
    }

    [TestMethod]
    public void Decode_ACnameToTheRoot_StoresAnEmptyName()
    {
        var message = Message(Header("8180", 1, 1) + QuestionA + "C00C0005000100000E100001" + "00");

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        CollectionAssert.AreEqual(new[] { string.Empty }, answer.CanonicalNames.ToArray());
    }

    [TestMethod]
    public void Decode_ACnamePointingAtItself_FailsWithLabelLoop()
    {
        // The CNAME's data is at offset 42 and is a pointer to offset 42.
        var message = Message(Header("8180", 1, 1) + QuestionA + "C00C0005000100000E100002" + "C02A");

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.LabelLoop, answer.Failure);
        Assert.IsEmpty(answer.Addresses);
        CollectionAssert.AreEqual(new[] { string.Empty }, answer.CanonicalNames.ToArray());
    }

    [TestMethod]
    public void Decode_AnAddressBeforeAnUnexpectedType_KeepsTheAddressAndTtl()
    {
        // curl's dohentry keeps what a failed decode stored (BL-958).
        var message = Message(Header("8180", 1, 2) + QuestionA + "C00C0001000100000E100004C0000201" + "C00C001C0001000000780010" + "00000000000000000000000000000001");

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.UnexpectedType, answer.Failure);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.1") }, answer.Addresses.ToArray());

        // The AAAA record's TTL (120) is never read: the type check stops the decode first.
        Assert.AreEqual(3600u, answer.TimeToLiveSeconds);
    }

    [TestMethod]
    public void Decode_ACnameOfOneHundredTwentySevenLabels_Succeeds()
    {
        // 127 labels and the root are the 128 steps curl allows.
        var answer = DnsAnswerDecoder.Decode(CnameOfLabels(127), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        Assert.AreEqual(string.Join('.', Enumerable.Repeat("a", 127)), answer.CanonicalNames[0]);
    }

    [TestMethod]
    public void Decode_ACnameOfOneHundredTwentyEightLabels_FailsWithLabelLoop()
    {
        var answer = DnsAnswerDecoder.Decode(CnameOfLabels(128), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.LabelLoop, answer.Failure);
    }

    [TestMethod]
    [DataRow("8183", DisplayName = "NXDOMAIN")]
    [DataRow("8182", DisplayName = "SERVFAIL")]
    public void Decode_ANonZeroRcode_FailsWithBadRcode(string flags)
    {
        // Measured: an RCODE 3 answer is "DoH: Bad RCODE type A for example.test" (ADR-0152).
        var message = Message(Header(flags, 1, 0) + QuestionA);

        Assert.AreEqual(DnsMessageFailure.BadRcode, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_EveryTruncationOfAFullAnswer_FailsWithOutOfRange()
    {
        // Measured: the 46-byte A answer cut to 45 is "DoH: Out of range type A" (ADR-0152).
        var full = Message(Header("8180", 1, 1, 1, 1) + QuestionA + AnswerA + AuthorityNs + AdditionalOpt);

        for (var length = 12; length < full.Length; length++)
        {
            var answer = DnsAnswerDecoder.Decode(full.AsSpan(0, length), DnsRecordType.A);

            Assert.AreEqual(DnsMessageFailure.OutOfRange, answer.Failure, $"cut to {length} bytes");
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(11)]
    public void Decode_LessThanAHeader_FailsWithTooSmall(int length)
    {
        // Measured: a 500 with an empty body is "DoH: Too small type A" (ADR-0152).
        var answer = DnsAnswerDecoder.Decode(MeasuredAnswer.AsSpan(0, length), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.TooSmall, answer.Failure);
        Assert.AreEqual((uint)int.MaxValue, answer.TimeToLiveSeconds);
    }

    [TestMethod]
    [DataRow("0100")]
    [DataRow("0001")]
    public void Decode_ANonZeroId_FailsWithBadId(string id)
    {
        var message = Message(id + Header("8180", 1, 1)[4..] + QuestionA + AnswerA);

        Assert.AreEqual(DnsMessageFailure.BadId, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_NoAnswerRecords_FailsWithNoContent()
    {
        // Measured: an AAAA answer with no records is "DoH: No content type AAAA" (ADR-0152).
        var message = Message(Header("8180", 1, 0) + QuestionAaaa);

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.Aaaa);

        Assert.AreEqual(DnsMessageFailure.NoContent, answer.Failure);
        Assert.AreEqual((uint)int.MaxValue, answer.TimeToLiveSeconds);
    }

    [TestMethod]
    public void Decode_OnlyADnameRecord_SkipsItAndFailsWithNoContent()
    {
        var message = Message(Header("8180", 1, 1) + QuestionA + "C00C0027000100000E100002" + "C00C");

        Assert.AreEqual(DnsMessageFailure.NoContent, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_AnAaaaRecordAnsweringAnAQuery_FailsWithUnexpectedType()
    {
        // The first of two answers fails, so the second is never read.
        var message = Message(Header("8180", 1, 2) + QuestionA
            + "C00C001C00010000003C0010" + "00000000000000000000000000000001" + AnswerA);

        Assert.AreEqual(DnsMessageFailure.UnexpectedType, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_ARecordOfClassChaos_FailsWithUnexpectedClass()
    {
        var message = Message(Header("8180", 1, 1) + QuestionA + "C00C000100030000003C00047F000001");

        Assert.AreEqual(DnsMessageFailure.UnexpectedClass, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_AnARecordOfFiveBytes_FailsWithRdataLength()
    {
        var message = Message(Header("8180", 1, 1) + QuestionA + "C00C000100010000003C00057F00000100");

        Assert.AreEqual(DnsMessageFailure.RdataLength, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_AnAaaaRecordOfFourBytes_FailsWithRdataLength()
    {
        var message = Message(Header("8180", 1, 1) + QuestionAaaa + "C00C001C00010000003C00047F000001");

        Assert.AreEqual(DnsMessageFailure.RdataLength, DnsAnswerDecoder.Decode(message, DnsRecordType.Aaaa).Failure);
    }

    [TestMethod]
    public void Decode_ATrailingByte_FailsWithMalformed()
    {
        var message = Message(Header("8180", 1, 1) + QuestionA + AnswerA + "00");

        Assert.AreEqual(DnsMessageFailure.Malformed, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    [DataRow("40")]
    [DataRow("80")]
    public void Decode_AQuestionLabelWithAReservedLengthPattern_FailsWithBadLabel(string lengthByte)
    {
        var message = Message(Header("8180", 1, 0) + lengthByte + "00010001");

        Assert.AreEqual(DnsMessageFailure.BadLabel, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_ACnameLabelWithAReservedLengthPattern_FailsWithBadLabel()
    {
        var message = Message(Header("8180", 1, 1) + QuestionA + "C00C0005000100000E100002" + "8000");

        Assert.AreEqual(DnsMessageFailure.BadLabel, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_ACnameLabelRunningPastTheMessage_FailsWithBadLabel()
    {
        var message = Message(Header("8180", 1, 1) + QuestionA + "C00C0005000100000E100002" + "0561");

        Assert.AreEqual(DnsMessageFailure.BadLabel, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_ACnamePointerPastTheMessage_FailsWithOutOfRange()
    {
        var message = Message(Header("8180", 1, 1) + QuestionA + "C00C0005000100000E100002" + "C0FF");

        Assert.AreEqual(DnsMessageFailure.OutOfRange, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_ACnameEndingInHalfAPointer_FailsWithOutOfRange()
    {
        var message = Message(Header("8180", 1, 1) + QuestionA + "C00C0005000100000E100001" + "C0");

        Assert.AreEqual(DnsMessageFailure.OutOfRange, DnsAnswerDecoder.Decode(message, DnsRecordType.A).Failure);
    }

    [TestMethod]
    public void Decode_MoreThanTwentyFourAddresses_KeepsTheFirstTwentyFour()
    {
        var records = string.Concat(Enumerable.Range(1, 25).Select(n => "C00C000100010000003C00047F0000" + n.ToString("X2")));
        var message = Message(Header("8180", 1, 25) + QuestionA + records);

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        Assert.HasCount(24, answer.Addresses);
        Assert.AreEqual(IPAddress.Parse("127.0.0.24"), answer.Addresses[^1]);
    }

    [TestMethod]
    public void Decode_MoreThanFourCnames_KeepsTheFirstFour()
    {
        var records = string.Concat(Enumerable.Repeat("C00C0005000100000E100002C00C", 5));
        var message = Message(Header("8180", 1, 5) + QuestionA + records);

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        Assert.HasCount(4, answer.CanonicalNames);
    }

    [TestMethod]
    public void Decode_AnSrvAnswer_StoresEachRecordWithItsTargetFollowedThroughPointers()
    {
        // Question: _k.example.test SRV; answers: 0 100 88 kdc.<question's example.test>, and 5 1 750 "." (no service).
        const string QuestionSrv = "025F6B076578616D706C6504746573740000210001";
        const string First = "C00C002100010000003C000C" + "0000" + "0064" + "0058" + "036B6463C00F";
        const string Second = "C00C002100010000003C0007" + "0005" + "0001" + "02EE" + "00";
        var message = Message(Header("8180", 1, 2) + QuestionSrv + First + Second);

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.Srv);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        CollectionAssert.AreEqual(
            new[] { new DnsServiceRecord(0, 100, 88, "kdc.example.test"), new DnsServiceRecord(5, 1, 750, string.Empty) },
            answer.ServiceRecords.ToArray());
        Assert.IsEmpty(answer.Addresses);
    }

    [TestMethod]
    public void Decode_AnSrvRecordTooShortForItsNumbersAndName_IsAnRdataLengthFailure()
    {
        const string QuestionSrv = "025F6B076578616D706C6504746573740000210001";
        var message = Message(Header("8180", 1, 1) + QuestionSrv + "C00C002100010000003C0006000000000058");

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.Srv);

        Assert.AreEqual(DnsMessageFailure.RdataLength, answer.Failure);
        Assert.IsEmpty(answer.ServiceRecords);
    }

    [TestMethod]
    public void Decode_ARecordOfATypeItDoesNotStore_IsAcceptedButStoresNothing()
    {
        // Asked for NS (type 2), which neither DoH nor the --dns-servers client asks for.
        var message = Message(Header("8180", 1, 1) + "076578616D706C6504746573740000020001" + AuthorityNs);

        var answer = DnsAnswerDecoder.Decode(message, (DnsRecordType)2);

        Assert.AreEqual(DnsMessageFailure.NoContent, answer.Failure);
    }

    [TestMethod]
    public void Decode_AnAAnswer_HasNoServiceRecords()
    {
        Assert.IsEmpty(DnsAnswerDecoder.Decode(MeasuredAnswer, DnsRecordType.A).ServiceRecords);
    }

    [TestMethod]
    public void Decode_AnHttpsAnswerWithEch_KeepsTheRecordDataUndecoded()
    {
        // example.test HTTPS 1 . alpn=h2 ech=AABBCCDDEEFF, TTL 60 (BL-707).
        const string RecordData = "000100" + "00010003026832" + "00050006AABBCCDDEEFF";
        var message = Message(Header("8180", 1, 1) + QuestionHttps + "C00C004100010000003C0014" + RecordData);

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.Https);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        Assert.HasCount(1, answer.HttpsRecordData);
        Assert.AreEqual(RecordData, Convert.ToHexString(answer.HttpsRecordData[0]));
        Assert.IsEmpty(answer.Addresses);
        Assert.AreEqual(60u, answer.TimeToLiveSeconds);
        Assert.AreEqual("AABBCCDDEEFF", Convert.ToHexString(ServiceBindingRecordDecoder.Decode(answer.HttpsRecordData[0]).Record!.EchConfigList.Span));
    }

    [TestMethod]
    public void Decode_FiveHttpsRecords_KeepsTheFirstFourAsCurlDoes()
    {
        var records = string.Concat(Enumerable.Range(1, 5).Select(priority => $"C00C004100010000003C0003{priority:X4}00"));
        var message = Message(Header("8180", 1, 5) + QuestionHttps + records);

        var answer = DnsAnswerDecoder.Decode(message, DnsRecordType.Https);

        Assert.AreEqual(DnsMessageFailure.None, answer.Failure);
        CollectionAssert.AreEqual(
            new[] { "000100", "000200", "000300", "000400" },
            answer.HttpsRecordData.Select(Convert.ToHexString).ToArray());
    }

    [TestMethod]
    public void Decode_AnAAnswer_HasNoHttpsRecordData()
    {
        Assert.IsEmpty(DnsAnswerDecoder.Decode(MeasuredAnswer, DnsRecordType.A).HttpsRecordData);
    }

    /// <summary>A header with ID 0, the given flags and section counts, in hex.</summary>
    private static string Header(string flags, int questions, int answers, int authorities = 0, int additionals = 0) =>
        "0000" + flags + $"{questions:X4}{answers:X4}{authorities:X4}{additionals:X4}";

    private static byte[] Message(string hex) => Convert.FromHexString(hex);

    /// <summary>An answer to the A question holding one CNAME of <paramref name="labelCount" /> one-letter labels.</summary>
    private static byte[] CnameOfLabels(int labelCount)
    {
        var name = string.Concat(Enumerable.Repeat("0161", labelCount)) + "00";
        var dataLength = name.Length / 2;
        return Message(Header("8180", 1, 1) + QuestionA + "C00C0005000100000E10" + $"{dataLength:X4}" + name);
    }
}
