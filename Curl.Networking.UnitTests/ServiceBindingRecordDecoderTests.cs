using System.Net;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="ServiceBindingRecordDecoder" /> to RFC 9460's wire format: Appendix D's
/// AliasMode and ServiceMode test vectors decode to the parameters they spell out, a record
/// with an <c>ech</c> parameter yields its ECHConfigList, and every malformed record is refused
/// with its <see cref="ServiceBindingFailure" />.
/// </summary>
[TestClass]
public sealed class ServiceBindingRecordDecoderTests
{
    /// <summary><c>foo.example.com.</c>, uncompressed.</summary>
    private const string FooExampleCom = "03666F6F076578616D706C6503636F6D00";

    /// <summary><c>foo.example.org.</c>, uncompressed.</summary>
    private const string FooExampleOrg = "03666F6F076578616D706C65036F726700";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Decode_AppendixD1AliasForm_ReturnsPriorityZeroAndTheTarget()
    {
        // example.com. HTTPS 0 foo.example.com.
        var record = Decoded("0000" + FooExampleCom);

        Diagnostics.Assert("priority, target", "0, foo.example.com", $"{record.Priority}, {record.TargetName}");

        Assert.AreEqual(0, record.Priority);
        Assert.AreEqual("foo.example.com", record.TargetName);
        Assert.IsEmpty(record.ApplicationProtocols);
        Assert.IsNull(record.Port);
        Assert.IsTrue(record.EchConfigList.IsEmpty);
    }

    [TestMethod]
    public void Decode_AppendixD2UseTheOwnerName_ReturnsTheRootTarget()
    {
        // example.com. SVCB 1 .
        var record = Decoded("000100");

        Diagnostics.Assert("priority, target", "1, .", $"{record.Priority}, {record.TargetName}");

        Assert.AreEqual(1, record.Priority);
        Assert.AreEqual(".", record.TargetName);
    }

    [TestMethod]
    public void Decode_AppendixD2Port_ReturnsThePort()
    {
        // example.com. SVCB 16 foo.example.com. port=53
        var record = Decoded("0010" + FooExampleCom + "000300020035");

        Diagnostics.Assert("priority, port", "16, 53", $"{record.Priority}, {record.Port}");

        Assert.AreEqual(16, record.Priority);
        Assert.AreEqual((ushort)53, record.Port);
    }

    [TestMethod]
    [DataRow("029B000568656C6C6F", DisplayName = "key667=hello")]
    [DataRow("029B000968656C6C6FD2716F6F", DisplayName = "key667=hello\\210qoo")]
    public void Decode_AppendixD2GenericKey_SkipsTheUnknownKey(string parameters)
    {
        Diagnostics.Arrange("parameters", parameters);

        var record = Decoded("0001" + FooExampleCom + parameters);

        Diagnostics.Assert("target", "foo.example.com", record.TargetName);

        Assert.AreEqual("foo.example.com", record.TargetName);
        Assert.IsEmpty(record.ApplicationProtocols);
        Assert.IsNull(record.Port);
    }

    [TestMethod]
    public void Decode_AppendixD2TwoIPv6Hints_ReturnsBoth()
    {
        // example.com. SVCB 1 foo.example.com. ipv6hint="2001:db8::1,2001:db8::53:1"
        var record = Decoded("0001" + FooExampleCom + "00060020"
            + "20010DB8000000000000000000000001" + "20010DB8000000000000000000530001");

        Diagnostics.Assert("ipv6 hints", "2001:db8::1, 2001:db8::53:1", string.Join(", ", record.IPv6Hints));

        CollectionAssert.AreEqual(
            new[] { IPAddress.Parse("2001:db8::1"), IPAddress.Parse("2001:db8::53:1") },
            record.IPv6Hints.ToArray());
    }

    [TestMethod]
    public void Decode_AppendixD2IPv4MappedStyleHint_ReturnsTheAddress()
    {
        // example.com. SVCB 1 example.com. ipv6hint="2001:db8:122:344::192.0.2.33"
        var record = Decoded("0001076578616D706C6503636F6D00" + "00060010" + "20010DB80122034400000000C0000221");

        Diagnostics.Assert("target", "example.com", record.TargetName);
        Diagnostics.Assert("ipv6 hints", "2001:db8:122:344::c000:221", string.Join(", ", record.IPv6Hints));

        Assert.AreEqual("example.com", record.TargetName);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("2001:db8:122:344::192.0.2.33") }, record.IPv6Hints.ToArray());
    }

    [TestMethod]
    public void Decode_AppendixD2ParamsInWireOrder_ReturnsAlpnAndIPv4Hint()
    {
        // example.com. SVCB 16 foo.example.org. (alpn=h2,h3-19 mandatory=ipv4hint,alpn ipv4hint=192.0.2.1)
        var record = Decoded("0010" + FooExampleOrg
            + "000000040001000400010009026832056833" + "2D3139" + "00040004C0000201");

        Diagnostics.Assert("alpn", "h2, h3-19", string.Join(", ", record.ApplicationProtocols));
        Diagnostics.Assert("ipv4 hints", "192.0.2.1", string.Join(", ", record.IPv4Hints));

        Assert.AreEqual(16, record.Priority);
        Assert.AreEqual("foo.example.org", record.TargetName);
        CollectionAssert.AreEqual(new[] { "h2", "h3-19" }, record.ApplicationProtocols.ToArray());
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.1") }, record.IPv4Hints.ToArray());
    }

    [TestMethod]
    public void Decode_AppendixD2AlpnWithEscapes_ReturnsTheIdentifiersVerbatim()
    {
        // example.com. SVCB 16 foo.example.org. alpn="f\\oo\,bar,h2"
        var record = Decoded("0010" + FooExampleOrg + "0001000C" + "08665C6F6F2C626172" + "026832");

        Diagnostics.Assert("alpn", "f\\oo,bar | h2", string.Join(" | ", record.ApplicationProtocols));

        CollectionAssert.AreEqual(new[] { "f\\oo,bar", "h2" }, record.ApplicationProtocols.ToArray());
    }

    [TestMethod]
    public void Decode_ARecordWithEch_ReturnsTheEchConfigListAndNoDefaultAlpn()
    {
        var record = Decoded("000100" + "00010003026832" + "00020000" + "00050006AABBCCDDEEFF");

        Diagnostics.Assert("alpn", "h2", string.Join(", ", record.ApplicationProtocols));
        Diagnostics.Assert("no-default-alpn", true, record.NoDefaultApplicationProtocol);
        Diagnostics.Bytes("ech config list", record.EchConfigList.Span);

        CollectionAssert.AreEqual(new[] { "h2" }, record.ApplicationProtocols.ToArray());
        Assert.IsTrue(record.NoDefaultApplicationProtocol);
        Assert.AreEqual("AABBCCDDEEFF", Convert.ToHexString(record.EchConfigList.Span));
    }

    [TestMethod]
    public void Decode_ARecordWithAMandatoryList_KeepsThatItHadOneAndIgnoresItsKeys()
    {
        // 1 . (mandatory=alpn alpn=h2)
        var record = Decoded("000100" + "000000020001" + "00010003026832");

        Diagnostics.Assert("has mandatory keys", true, record.HasMandatoryKeys);
        Diagnostics.Assert("alpn", "h2", string.Join(", ", record.ApplicationProtocols));

        Assert.IsTrue(record.HasMandatoryKeys);
        CollectionAssert.AreEqual(new[] { "h2" }, record.ApplicationProtocols.ToArray());
    }

    [TestMethod]
    public void Decode_ARecordWithoutAMandatoryList_HasNoMandatoryKeys()
    {
        var record = Decoded("000100" + "00010003026832");

        Diagnostics.Assert("has mandatory keys", false, record.HasMandatoryKeys);

        Assert.IsFalse(record.HasMandatoryKeys);
    }

    [TestMethod]
    [DataRow("00010000010000", DisplayName = "empty alpn")]
    [DataRow("0001000001000100", DisplayName = "empty alpn identifier")]
    public void Decode_AnEmptyAlpnOrAlpnIdentifier_IsAcceptedAsCurlAcceptsIt(string hex)
    {
        // Upstream unit1658's "rname + blank alpn field" decodes, though RFC 9460 asks for one or more alpn-ids.
        var record = Decoded(hex);

        Diagnostics.Assert("alpn count", hex.Length > 14 ? 1 : 0, record.ApplicationProtocols.Count);

        Assert.HasCount(hex.Length > 14 ? 1 : 0, record.ApplicationProtocols);
    }

    [TestMethod]
    [DataRow("00", DisplayName = "one byte")]
    [DataRow("0003", DisplayName = "a key without its length")]
    [DataRow("000300", DisplayName = "three bytes")]
    public void Decode_FewerThanAParameterHeaderLeft_IgnoresTheTrailingBytes(string trailing)
    {
        // curl reads SvcParams only while 4 or more bytes remain.
        var record = Decoded("000100" + "000300020050" + trailing);

        Diagnostics.Assert("port", 80, record.Port);

        Assert.AreEqual((ushort)80, record.Port);
    }

    [TestMethod]
    public void Decode_TargetWithAControlByteAfterANul_IsAcceptedAsCurlsCStringEndsAtTheNul()
    {
        var record = Decoded("000103610001" + "00");

        Diagnostics.Assert("target", "a\0\u0001", record.TargetName);

        Assert.AreEqual("a\0\u0001", record.TargetName);
    }

    [TestMethod]
    [DataRow("", ServiceBindingFailure.Truncated, DisplayName = "no data")]
    [DataRow("0001", ServiceBindingFailure.Truncated, DisplayName = "priority only")]
    [DataRow("000105", ServiceBindingFailure.BadTargetName, DisplayName = "label past the end")]
    [DataRow("000103666F6F", ServiceBindingFailure.BadTargetName, DisplayName = "name without its root")]
    [DataRow("0001C00C", ServiceBindingFailure.BadTargetName, DisplayName = "compressed name")]
    [DataRow("00010361016200", ServiceBindingFailure.TargetNameHasControlCharacter, DisplayName = "control byte in the target")]
    [DataRow("00010361206200", ServiceBindingFailure.TargetNameHasControlCharacter, DisplayName = "space in the target")]
    [DataRow("00010361627F00", ServiceBindingFailure.TargetNameHasControlCharacter, DisplayName = "DEL in the target")]
    [DataRow("00010000030004" + "0035", ServiceBindingFailure.ParameterOverrun, DisplayName = "value past the end")]
    [DataRow("000100" + "00040004C0000201" + "00010003026832", ServiceBindingFailure.ParametersOutOfOrder, DisplayName = "keys out of order")]
    [DataRow("000100" + "000300020050" + "000300020050", ServiceBindingFailure.ParametersOutOfOrder, DisplayName = "key repeated")]
    [DataRow("000100" + "00040004C0000201" + "0001FFFF", ServiceBindingFailure.ParametersOutOfOrder, DisplayName = "key out of order and past the end")]
    [DataRow("00010000030001" + "35", ServiceBindingFailure.BadParameterValue, DisplayName = "one-byte port")]
    [DataRow("000100000100020568", ServiceBindingFailure.BadApplicationProtocolList, DisplayName = "alpn identifier past its value")]
    [DataRow("0001000002000100", ServiceBindingFailure.BadParameterValue, DisplayName = "no-default-alpn with a value")]
    [DataRow("0001000004000" + "3C00002", ServiceBindingFailure.BadParameterValue, DisplayName = "three-byte ipv4hint")]
    [DataRow("00010000050000", ServiceBindingFailure.BadParameterValue, DisplayName = "empty ech")]
    [DataRow("00010000060000", ServiceBindingFailure.BadParameterValue, DisplayName = "empty ipv6hint")]
    public void Decode_AMalformedRecord_IsRefusedWithItsFailure(string hex, ServiceBindingFailure expected)
    {
        Diagnostics.Arrange("record hex", hex);

        var decoding = ServiceBindingRecordDecoder.Decode(Convert.FromHexString(hex));

        Diagnostics.Act("failure", decoding.Failure);
        Diagnostics.Act("record", decoding.Record);
        Diagnostics.Assert("failure", expected, decoding.Failure);
        Diagnostics.Assert("record", null, decoding.Record);

        Assert.AreEqual(expected, decoding.Failure);
        Assert.IsNull(decoding.Record);
    }

    private ServiceBindingRecord Decoded(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        Diagnostics.Bytes("record", bytes);
        Diagnostics.Arrange("record length", bytes.Length);

        var decoding = ServiceBindingRecordDecoder.Decode(bytes);

        Diagnostics.Act("failure", decoding.Failure);
        Diagnostics.Act("record", decoding.Record);
        Diagnostics.Assert("failure", ServiceBindingFailure.None, decoding.Failure);

        Assert.AreEqual(ServiceBindingFailure.None, decoding.Failure);
        return decoding.Record!;
    }
}
