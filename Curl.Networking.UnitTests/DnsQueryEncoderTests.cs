using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsQueryEncoder" /> to the DoH query curl 8.21.0 sent, measured in BL-639
/// (ADR-0152), and to the name limits of curl's <c>doh_req_encode</c>.
/// </summary>
[TestClass]
public sealed class DnsQueryEncoderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Encode_WithNullHostName_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("host name", "(null)");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => DnsQueryEncoder.Encode(null!, DnsRecordType.A));

        Diagnostics.Act("parameter name", exception.ParamName);
        Diagnostics.Assert("parameter name", "hostName", exception.ParamName);
        Assert.AreEqual("hostName", exception.ParamName);
    }

    [TestMethod]
    public void Encode_AnAQuery_ReturnsTheThirtyBytesCurlPosted()
    {
        // curl --doh-url https://127.0.0.1:P/dns-query --doh-insecure http://example.test/ (ADR-0152)
        Diagnostics.Arrange("host name", "example.test");
        Diagnostics.Arrange("record type", DnsRecordType.A);

        var encoding = DnsQueryEncoder.Encode("example.test", DnsRecordType.A);

        WriteEncoding(encoding);
        Diagnostics.Assert("failure", DnsMessageFailure.None, encoding.Failure);
        Diagnostics.Diff("query", Convert.FromHexString("000001000001000000000000076578616D706C6504746573740000010001"), encoding.Bytes);
        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.AreEqual(
            "000001000001000000000000076578616D706C6504746573740000010001",
            Convert.ToHexString(encoding.Bytes));
    }

    [TestMethod]
    public void Encode_AnAaaaQuery_DiffersOnlyInItsQuestionType()
    {
        Diagnostics.Arrange("host name", "example.test");
        Diagnostics.Arrange("record type", DnsRecordType.Aaaa);

        var encoding = DnsQueryEncoder.Encode("example.test", DnsRecordType.Aaaa);

        WriteEncoding(encoding);
        Diagnostics.Diff("query", Convert.FromHexString("000001000001000000000000076578616D706C65047465737400001C0001"), encoding.Bytes);
        Assert.AreEqual(
            "000001000001000000000000076578616D706C65047465737400001C0001",
            Convert.ToHexString(encoding.Bytes));
    }

    [TestMethod]
    public void Encode_WithATrailingDot_ReturnsTheSameBytesAsWithout()
    {
        Diagnostics.Arrange("host names", "example.test and example.test.");
        Diagnostics.Arrange("record type", DnsRecordType.A);

        var withoutDot = DnsQueryEncoder.Encode("example.test", DnsRecordType.A).Bytes;
        var withDot = DnsQueryEncoder.Encode("example.test.", DnsRecordType.A).Bytes;

        Diagnostics.Act("query length without the dot", withoutDot.Length);
        Diagnostics.Act("query length with the dot", withDot.Length);
        Diagnostics.Diff("query with the dot", withoutDot, withDot);
        CollectionAssert.AreEqual(
            DnsQueryEncoder.Encode("example.test", DnsRecordType.A).Bytes,
            DnsQueryEncoder.Encode("example.test.", DnsRecordType.A).Bytes);
    }

    [TestMethod]
    public void Encode_AnEmptyName_AsksForTheRoot()
    {
        Diagnostics.Arrange("host name", "(empty)");
        Diagnostics.Arrange("record type", DnsRecordType.A);

        var encoding = DnsQueryEncoder.Encode(string.Empty, DnsRecordType.A);

        WriteEncoding(encoding);
        Diagnostics.Assert("failure", DnsMessageFailure.None, encoding.Failure);
        Diagnostics.Diff("query", Convert.FromHexString("0000010000010000000000000000010001"), encoding.Bytes);
        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.AreEqual("0000010000010000000000000000010001", Convert.ToHexString(encoding.Bytes));
    }

    [TestMethod]
    [DataRow("a..b")]
    [DataRow(".")]
    [DataRow(".a")]
    public void Encode_WithAnEmptyLabel_FailsWithBadLabel(string hostName)
    {
        Diagnostics.Arrange("host name", hostName);
        Diagnostics.Arrange("record type", DnsRecordType.A);

        var encoding = DnsQueryEncoder.Encode(hostName, DnsRecordType.A);

        WriteEncoding(encoding);
        Diagnostics.Assert("failure", DnsMessageFailure.BadLabel, encoding.Failure);
        Diagnostics.Assert("query length", 0, encoding.Bytes.Length);
        Assert.AreEqual(DnsMessageFailure.BadLabel, encoding.Failure);
        Assert.IsEmpty(encoding.Bytes);
    }

    [TestMethod]
    public void Encode_WithALabelOfSixtyThreeBytes_Succeeds()
    {
        Diagnostics.Arrange("first label length", 63);
        Diagnostics.Arrange("record type", DnsRecordType.A);

        var encoding = DnsQueryEncoder.Encode(new string('a', 63) + ".test", DnsRecordType.A);

        WriteEncoding(encoding);
        Diagnostics.Assert("failure", DnsMessageFailure.None, encoding.Failure);
        Diagnostics.Assert("first label length byte", 63, encoding.Bytes[12]);
        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.AreEqual(63, encoding.Bytes[12]);
    }

    [TestMethod]
    public void Encode_WithALabelOfSixtyFourBytes_FailsWithBadLabel()
    {
        Diagnostics.Arrange("first label length", 64);
        Diagnostics.Arrange("record type", DnsRecordType.A);

        var encoding = DnsQueryEncoder.Encode(new string('a', 64) + ".test", DnsRecordType.A);

        WriteEncoding(encoding);
        Diagnostics.Assert("failure", DnsMessageFailure.BadLabel, encoding.Failure);
        Assert.AreEqual(DnsMessageFailure.BadLabel, encoding.Failure);
    }

    [TestMethod]
    public void Encode_AtTheTwoHundredSeventyTwoByteLimit_Succeeds()
    {
        // 254 bytes of name without a trailing dot: 12 + 1 + 254 + 1 + 4 = 272.
        Diagnostics.Arrange("host name length", 254);
        Diagnostics.Arrange("record type", DnsRecordType.A);

        var encoding = DnsQueryEncoder.Encode(NameOfLength(254), DnsRecordType.A);

        WriteEncoding(encoding);
        Diagnostics.Assert("failure", DnsMessageFailure.None, encoding.Failure);
        Diagnostics.Assert("query length", 272, encoding.Bytes.Length);
        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.HasCount(272, encoding.Bytes);
    }

    [TestMethod]
    public void Encode_AtTheLimitWithATrailingDot_Succeeds()
    {
        Diagnostics.Arrange("host name length", "254 and a trailing dot");
        Diagnostics.Arrange("record type", DnsRecordType.A);

        var encoding = DnsQueryEncoder.Encode(NameOfLength(254) + ".", DnsRecordType.A);

        WriteEncoding(encoding);
        Diagnostics.Assert("failure", DnsMessageFailure.None, encoding.Failure);
        Diagnostics.Assert("query length", 272, encoding.Bytes.Length);
        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.HasCount(272, encoding.Bytes);
    }

    [TestMethod]
    public void Encode_OneBytePastTheLimit_FailsWithNameTooLong()
    {
        Diagnostics.Arrange("host name length", 255);
        Diagnostics.Arrange("record type", DnsRecordType.A);

        var encoding = DnsQueryEncoder.Encode(NameOfLength(255), DnsRecordType.A);

        WriteEncoding(encoding);
        Diagnostics.Assert("failure", DnsMessageFailure.NameTooLong, encoding.Failure);
        Diagnostics.Assert("query length", 0, encoding.Bytes.Length);
        Assert.AreEqual(DnsMessageFailure.NameTooLong, encoding.Failure);
        Assert.IsEmpty(encoding.Bytes);
    }

    /// <summary>A name of 63-byte labels joined by dots, cut to <paramref name="length" /> bytes.</summary>
    private static string NameOfLength(int length)
    {
        var label = new string('a', 63);
        return string.Join('.', label, label, label, label, label)[..length];
    }

    [TestMethod]
    public void Encode_AnHttpsQuery_AsksForType65()
    {
        // BL-707: curl's HTTPS query differs from its A query only in QTYPE, 00 41.
        Diagnostics.Arrange("host name", "example.test");
        Diagnostics.Arrange("record type", DnsRecordType.Https);

        var encoding = DnsQueryEncoder.Encode("example.test", DnsRecordType.Https);

        WriteEncoding(encoding);
        Diagnostics.Assert("failure", DnsMessageFailure.None, encoding.Failure);
        Diagnostics.Diff("query", Convert.FromHexString("000001000001000000000000076578616D706C6504746573740000410001"), encoding.Bytes);
        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.AreEqual(
            "000001000001000000000000076578616D706C6504746573740000410001",
            Convert.ToHexString(encoding.Bytes));
    }

    private void WriteEncoding(DnsQueryEncoding encoding)
    {
        Diagnostics.Act("failure", encoding.Failure);
        Diagnostics.Bytes("query", encoding.Bytes);
    }
}
