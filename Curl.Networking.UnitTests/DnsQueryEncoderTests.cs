namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsQueryEncoder" /> to the DoH query curl 8.21.0 sent, measured in BL-639
/// (ADR-0152), and to the name limits of curl's <c>doh_req_encode</c>.
/// </summary>
[TestClass]
public sealed class DnsQueryEncoderTests
{
    [TestMethod]
    public void Encode_WithNullHostName_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => DnsQueryEncoder.Encode(null!, DnsRecordType.A));

        Assert.AreEqual("hostName", exception.ParamName);
    }

    [TestMethod]
    public void Encode_AnAQuery_ReturnsTheThirtyBytesCurlPosted()
    {
        // curl --doh-url https://127.0.0.1:P/dns-query --doh-insecure http://example.test/ (ADR-0152)
        var encoding = DnsQueryEncoder.Encode("example.test", DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.AreEqual(
            "000001000001000000000000076578616D706C6504746573740000010001",
            Convert.ToHexString(encoding.Bytes));
    }

    [TestMethod]
    public void Encode_AnAaaaQuery_DiffersOnlyInItsQuestionType()
    {
        var encoding = DnsQueryEncoder.Encode("example.test", DnsRecordType.Aaaa);

        Assert.AreEqual(
            "000001000001000000000000076578616D706C65047465737400001C0001",
            Convert.ToHexString(encoding.Bytes));
    }

    [TestMethod]
    public void Encode_WithATrailingDot_ReturnsTheSameBytesAsWithout()
    {
        CollectionAssert.AreEqual(
            DnsQueryEncoder.Encode("example.test", DnsRecordType.A).Bytes,
            DnsQueryEncoder.Encode("example.test.", DnsRecordType.A).Bytes);
    }

    [TestMethod]
    public void Encode_AnEmptyName_AsksForTheRoot()
    {
        var encoding = DnsQueryEncoder.Encode(string.Empty, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.AreEqual("0000010000010000000000000000010001", Convert.ToHexString(encoding.Bytes));
    }

    [TestMethod]
    [DataRow("a..b")]
    [DataRow(".")]
    [DataRow(".a")]
    public void Encode_WithAnEmptyLabel_FailsWithBadLabel(string hostName)
    {
        var encoding = DnsQueryEncoder.Encode(hostName, DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.BadLabel, encoding.Failure);
        Assert.IsEmpty(encoding.Bytes);
    }

    [TestMethod]
    public void Encode_WithALabelOfSixtyThreeBytes_Succeeds()
    {
        var encoding = DnsQueryEncoder.Encode(new string('a', 63) + ".test", DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.AreEqual(63, encoding.Bytes[12]);
    }

    [TestMethod]
    public void Encode_WithALabelOfSixtyFourBytes_FailsWithBadLabel()
    {
        var encoding = DnsQueryEncoder.Encode(new string('a', 64) + ".test", DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.BadLabel, encoding.Failure);
    }

    [TestMethod]
    public void Encode_AtTheTwoHundredSeventyTwoByteLimit_Succeeds()
    {
        // 254 bytes of name without a trailing dot: 12 + 1 + 254 + 1 + 4 = 272.
        var encoding = DnsQueryEncoder.Encode(NameOfLength(254), DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.HasCount(272, encoding.Bytes);
    }

    [TestMethod]
    public void Encode_AtTheLimitWithATrailingDot_Succeeds()
    {
        var encoding = DnsQueryEncoder.Encode(NameOfLength(254) + ".", DnsRecordType.A);

        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.HasCount(272, encoding.Bytes);
    }

    [TestMethod]
    public void Encode_OneBytePastTheLimit_FailsWithNameTooLong()
    {
        var encoding = DnsQueryEncoder.Encode(NameOfLength(255), DnsRecordType.A);

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
        var encoding = DnsQueryEncoder.Encode("example.test", DnsRecordType.Https);

        Assert.AreEqual(DnsMessageFailure.None, encoding.Failure);
        Assert.AreEqual(
            "000001000001000000000000076578616D706C6504746573740000410001",
            Convert.ToHexString(encoding.Bytes));
    }
}
