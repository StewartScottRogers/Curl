using System.Net;
using Curl.Extension.WireGuard.Configuration;

namespace Curl.Extension.WireGuard.UnitTests.Configuration;

[TestClass]
public sealed class WireGuardValueParserTests
{
    [TestMethod]
    public void ParseKey_ThirtyTwoBytesOfBase64_GivesTheBytes()
    {
        var bytes = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

        var key = WireGuardValueParser.ParseKey(Convert.ToBase64String(bytes), "PublicKey", 4);

        CollectionAssert.AreEqual(bytes, key);
    }

    [TestMethod]
    [DataRow("not base64!")]
    [DataRow("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==")]
    [DataRow("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [DataRow("")]
    public void ParseKey_NotThirtyTwoBytesOfBase64_NamesTheLineAndKey(string value)
    {
        var exception = Assert.ThrowsExactly<WireGuardConfigurationException>(
            () => WireGuardValueParser.ParseKey(value, "PrivateKey", 4));

        Assert.AreEqual("line 4: PrivateKey is not 32 bytes of base64", exception.Message);
        Assert.AreEqual(4, exception.LineNumber);
    }

    [TestMethod]
    [DataRow("10.0.0.2/24", "10.0.0.2", 24)]
    [DataRow("10.0.0.2", "10.0.0.2", 32)]
    [DataRow("0.0.0.0/0", "0.0.0.0", 0)]
    [DataRow("fd00::1", "fd00::1", 128)]
    [DataRow("fd00::/64", "fd00::", 64)]
    public void ParseCidr_AddressWithOrWithoutPrefix_GivesAddressAndPrefixLength(string value, string address, int prefixLength)
    {
        var cidr = WireGuardValueParser.ParseCidr(value, 1);

        Assert.AreEqual(new WireGuardCidr(IPAddress.Parse(address), prefixLength), cidr);
    }

    [TestMethod]
    [DataRow("10.0.0.300/24")]
    [DataRow("10.0.0.1/33")]
    [DataRow("10.0.0.1/")]
    [DataRow("10.0.0.1/a")]
    [DataRow("10.0.0.1/-1")]
    [DataRow("10.0.0.1/99999999999")]
    [DataRow("fd00::/129")]
    [DataRow("fe80::1%eth0/64")]
    [DataRow("vpn.example.com")]
    public void ParseCidr_Malformed_NamesTheLine(string value)
    {
        var exception = Assert.ThrowsExactly<WireGuardConfigurationException>(() => WireGuardValueParser.ParseCidr(value, 7));

        Assert.AreEqual($"line 7: '{value}' is not a valid CIDR", exception.Message);
    }

    [TestMethod]
    public void ParseCidrList_CommaSeparated_GivesEveryEntry()
    {
        var cidrs = WireGuardValueParser.ParseCidrList("10.0.0.0/8 , fd00::/64,", 1).ToList();

        Assert.HasCount(2, cidrs);
        Assert.AreEqual(new WireGuardCidr(IPAddress.Parse("fd00::"), 64), cidrs[1]);
    }

    [TestMethod]
    [DataRow("vpn.example.com:51820", "vpn.example.com", 51820)]
    [DataRow("192.0.2.1:1", "192.0.2.1", 1)]
    [DataRow("[2001:db8::1]:65535", "2001:db8::1", 65535)]
    public void ParseEndpoint_HostAndPort_GivesHostAndPort(string value, string host, int port)
    {
        var endpoint = WireGuardValueParser.ParseEndpoint(value, 1);

        Assert.AreEqual(new WireGuardEndpoint(host, port), endpoint);
    }

    [TestMethod]
    [DataRow("vpn.example.com")]
    [DataRow("vpn.example.com:")]
    [DataRow("vpn.example.com:0")]
    [DataRow("vpn.example.com:65536")]
    [DataRow(":51820")]
    [DataRow("2001:db8::1:51820")]
    [DataRow("[2001:db8::zz]:51820")]
    [DataRow("[]:51820")]
    [DataRow("[:51820")]
    [DataRow("[a]b:51820")]
    public void ParseEndpoint_Malformed_NamesTheLine(string value)
    {
        var exception = Assert.ThrowsExactly<WireGuardConfigurationException>(() => WireGuardValueParser.ParseEndpoint(value, 9));

        Assert.AreEqual($"line 9: '{value}' is not a valid endpoint", exception.Message);
    }

    [TestMethod]
    public void ParseNumber_InRange_GivesTheNumber()
    {
        Assert.AreEqual(51820, WireGuardValueParser.ParseNumber("51820", "ListenPort", 0, 65535, 1));
    }

    [TestMethod]
    public void ParseNumber_OutOfRange_NamesTheLineKeyAndRange()
    {
        var exception = Assert.ThrowsExactly<WireGuardConfigurationException>(
            () => WireGuardValueParser.ParseNumber("0", "MTU", 1, 65535, 3));

        Assert.AreEqual("line 3: MTU '0' is not a number from 1 to 65535", exception.Message);
    }

    [TestMethod]
    public void SplitList_EmptyEntries_AreLeftOut()
    {
        CollectionAssert.AreEqual(new[] { "1.1.1.1", "example.com" }, WireGuardValueParser.SplitList(" 1.1.1.1, ,example.com ").ToArray());
    }
}
