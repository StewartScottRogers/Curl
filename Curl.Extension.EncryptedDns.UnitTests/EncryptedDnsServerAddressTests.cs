using Curl.Extension.Abstractions;

namespace Curl.Extension.EncryptedDns;

[TestClass]
public sealed class EncryptedDnsServerAddressTests
{
    private static readonly ExtensionOption Option = EncryptedDnsExtension.DnsOverTlsOption;

    [TestMethod]
    [DataRow("dot.example", "dot.example", 853)]
    [DataRow("dot.example:8853", "dot.example", 8853)]
    [DataRow("192.0.2.1:1", "192.0.2.1", 1)]
    [DataRow("dot.example:65535", "dot.example", 65535)]
    [DataRow("[2001:db8::1]:5353", "2001:db8::1", 5353)]
    [DataRow("[2001:db8::1]", "2001:db8::1", 853)]
    [DataRow("2001:db8::1", "2001:db8::1", 853)]
    public void Parse_HostAndOptionalPort_GivesTheHostAndPort(string value, string expectedHost, int expectedPort)
    {
        (string host, int port) = EncryptedDnsServerAddress.Parse(value, Option, 853);

        Assert.AreEqual(expectedHost, host);
        Assert.AreEqual(expectedPort, port);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(":853")]
    [DataRow("dot.example:")]
    [DataRow("dot.example:0")]
    [DataRow("dot.example:65536")]
    [DataRow("dot.example:+53")]
    [DataRow("dot.example:x")]
    [DataRow("[2001:db8::1")]
    [DataRow("[2001:db8::1]853")]
    [DataRow("[]:853")]
    public void Parse_NotHostAndPort_ThrowsNamingTheOption(string value)
    {
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => EncryptedDnsServerAddress.Parse(value, Option, 853));

        Assert.StartsWith($"--dns-over-tls needs a resolver as host[:port], not '{value}'", error.Message);
    }
}
