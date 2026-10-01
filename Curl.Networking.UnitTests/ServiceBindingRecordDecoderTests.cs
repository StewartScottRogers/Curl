using System.Net;

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

    [TestMethod]
    public void Decode_AppendixD1AliasForm_ReturnsPriorityZeroAndTheTarget()
    {
        // example.com. HTTPS 0 foo.example.com.
        var record = Decoded("0000" + FooExampleCom);

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

        Assert.AreEqual(1, record.Priority);
        Assert.AreEqual(".", record.TargetName);
    }

    [TestMethod]
    public void Decode_AppendixD2Port_ReturnsThePort()
    {
        // example.com. SVCB 16 foo.example.com. port=53
        var record = Decoded("0010" + FooExampleCom + "000300020035");

        Assert.AreEqual(16, record.Priority);
        Assert.AreEqual((ushort)53, record.Port);
    }

    [TestMethod]
    [DataRow("029B000568656C6C6F", DisplayName = "key667=hello")]
    [DataRow("029B000968656C6C6FD2716F6F", DisplayName = "key667=hello\\210qoo")]
    public void Decode_AppendixD2GenericKey_SkipsTheUnknownKey(string parameters)
    {
        var record = Decoded("0001" + FooExampleCom + parameters);

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

        CollectionAssert.AreEqual(
            new[] { IPAddress.Parse("2001:db8::1"), IPAddress.Parse("2001:db8::53:1") },
            record.IPv6Hints.ToArray());
    }

    [TestMethod]
    public void Decode_AppendixD2IPv4MappedStyleHint_ReturnsTheAddress()
    {
        // example.com. SVCB 1 example.com. ipv6hint="2001:db8:122:344::192.0.2.33"
        var record = Decoded("0001076578616D706C6503636F6D00" + "00060010" + "20010DB80122034400000000C0000221");

        Assert.AreEqual("example.com", record.TargetName);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("2001:db8:122:344::192.0.2.33") }, record.IPv6Hints.ToArray());
    }

    [TestMethod]
    public void Decode_AppendixD2ParamsInWireOrder_ReturnsAlpnAndIPv4Hint()
    {
        // example.com. SVCB 16 foo.example.org. (alpn=h2,h3-19 mandatory=ipv4hint,alpn ipv4hint=192.0.2.1)
        var record = Decoded("0010" + FooExampleOrg
            + "000000040001000400010009026832056833" + "2D3139" + "00040004C0000201");

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

        CollectionAssert.AreEqual(new[] { "f\\oo,bar", "h2" }, record.ApplicationProtocols.ToArray());
    }

    [TestMethod]
    public void Decode_ARecordWithEch_ReturnsTheEchConfigListAndNoDefaultAlpn()
    {
        var record = Decoded("000100" + "00010003026832" + "00020000" + "00050006AABBCCDDEEFF");

        CollectionAssert.AreEqual(new[] { "h2" }, record.ApplicationProtocols.ToArray());
        Assert.IsTrue(record.NoDefaultApplicationProtocol);
        Assert.AreEqual("AABBCCDDEEFF", Convert.ToHexString(record.EchConfigList.Span));
    }

    [TestMethod]
    [DataRow("", ServiceBindingFailure.Truncated, DisplayName = "no data")]
    [DataRow("0001", ServiceBindingFailure.Truncated, DisplayName = "priority only")]
    [DataRow("000105", ServiceBindingFailure.BadTargetName, DisplayName = "label past the end")]
    [DataRow("000103666F6F", ServiceBindingFailure.BadTargetName, DisplayName = "name without its root")]
    [DataRow("0001C00C", ServiceBindingFailure.BadTargetName, DisplayName = "compressed name")]
    [DataRow("0001000003", ServiceBindingFailure.Truncated, DisplayName = "key without its length")]
    [DataRow("00010000030004" + "0035", ServiceBindingFailure.ParameterOverrun, DisplayName = "value past the end")]
    [DataRow("00010000030001" + "35", ServiceBindingFailure.BadParameterValue, DisplayName = "one-byte port")]
    [DataRow("00010000010000", ServiceBindingFailure.BadParameterValue, DisplayName = "empty alpn")]
    [DataRow("0001000001000100", ServiceBindingFailure.BadParameterValue, DisplayName = "empty alpn identifier")]
    [DataRow("000100000100020568", ServiceBindingFailure.BadParameterValue, DisplayName = "alpn identifier past its value")]
    [DataRow("0001000002000100", ServiceBindingFailure.BadParameterValue, DisplayName = "no-default-alpn with a value")]
    [DataRow("0001000004000" + "3C00002", ServiceBindingFailure.BadParameterValue, DisplayName = "three-byte ipv4hint")]
    [DataRow("00010000060000", ServiceBindingFailure.BadParameterValue, DisplayName = "empty ipv6hint")]
    public void Decode_AMalformedRecord_IsRefusedWithItsFailure(string hex, ServiceBindingFailure expected)
    {
        var decoding = ServiceBindingRecordDecoder.Decode(Convert.FromHexString(hex));

        Assert.AreEqual(expected, decoding.Failure);
        Assert.IsNull(decoding.Record);
    }

    private static ServiceBindingRecord Decoded(string hex)
    {
        var decoding = ServiceBindingRecordDecoder.Decode(Convert.FromHexString(hex));

        Assert.AreEqual(ServiceBindingFailure.None, decoding.Failure);
        return decoding.Record!;
    }
}
