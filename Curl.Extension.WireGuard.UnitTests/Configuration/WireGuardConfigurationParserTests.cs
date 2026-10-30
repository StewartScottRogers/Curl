using System.Net;
using Curl.Extension.WireGuard.Configuration;

namespace Curl.Extension.WireGuard.UnitTests.Configuration;

[TestClass]
public sealed class WireGuardConfigurationParserTests
{
    private static readonly string PrivateKey = Key(1);
    private static readonly string PublicKey = Key(2);
    private static readonly string PresharedKey = Key(3);
    private static readonly string SecondPublicKey = Key(4);

    [TestMethod]
    public void Parse_EveryKey_GivesTheTypedConfiguration()
    {
        var text = string.Join(
            "\r\n",
            "# A client configuration",
            "[Interface]",
            $"privatekey={PrivateKey}   # comment after a value",
            "Address = 10.0.0.2/24, fd00::2/64",
            "ADDRESS = 10.0.0.3",
            "DNS = 1.1.1.1, example.com",
            "ListenPort = 51820",
            "MTU = 1420",
            "PostUp = iptables -A FORWARD -i wg0 -j ACCEPT",
            "Table = off",
            string.Empty,
            "[ peer ]",
            $"PublicKey = {PublicKey}",
            $"PresharedKey = {PresharedKey}",
            "Endpoint = [2001:db8::1]:51820",
            "AllowedIPs = 0.0.0.0/0",
            "AllowedIPs = ::/0",
            "PersistentKeepalive = 25",
            "[Peer]",
            $"PublicKey = {SecondPublicKey}",
            string.Empty);

        var configuration = WireGuardConfigurationParser.Parse(text);

        var configuredInterface = configuration.Interface;
        CollectionAssert.AreEqual(Convert.FromBase64String(PrivateKey), configuredInterface.PrivateKey);
        CollectionAssert.AreEqual(
            new[]
            {
                new WireGuardCidr(IPAddress.Parse("10.0.0.2"), 24),
                new WireGuardCidr(IPAddress.Parse("fd00::2"), 64),
                new WireGuardCidr(IPAddress.Parse("10.0.0.3"), 32),
            },
            configuredInterface.Addresses.ToArray());
        CollectionAssert.AreEqual(new[] { "1.1.1.1", "example.com" }, configuredInterface.DnsServers.ToArray());
        Assert.AreEqual(51820, configuredInterface.ListenPort);
        Assert.AreEqual(1420, configuredInterface.Mtu);
        Assert.HasCount(2, configuration.Peers);
        var peer = configuration.Peers[0];
        CollectionAssert.AreEqual(Convert.FromBase64String(PublicKey), peer.PublicKey);
        CollectionAssert.AreEqual(Convert.FromBase64String(PresharedKey), peer.PresharedKey);
        Assert.AreEqual(new WireGuardEndpoint("2001:db8::1", 51820), peer.Endpoint);
        CollectionAssert.AreEqual(
            new[] { new WireGuardCidr(IPAddress.Any, 0), new WireGuardCidr(IPAddress.IPv6Any, 0) },
            peer.AllowedIps.ToArray());
        Assert.AreEqual(25, peer.PersistentKeepalive);
        var secondPeer = configuration.Peers[1];
        CollectionAssert.AreEqual(Convert.FromBase64String(SecondPublicKey), secondPeer.PublicKey);
        Assert.IsNull(secondPeer.PresharedKey);
        Assert.IsNull(secondPeer.Endpoint);
        Assert.IsEmpty(secondPeer.AllowedIps);
        Assert.IsNull(secondPeer.PersistentKeepalive);
    }

    [TestMethod]
    public void Parse_OnlyRequiredKeys_LeavesOptionalSettingsEmpty()
    {
        var configuration = WireGuardConfigurationParser.Parse(Minimal());

        Assert.IsEmpty(configuration.Interface.Addresses);
        Assert.IsEmpty(configuration.Interface.DnsServers);
        Assert.IsNull(configuration.Interface.ListenPort);
        Assert.IsNull(configuration.Interface.Mtu);
    }

    [TestMethod]
    [DataRow("off")]
    [DataRow("OFF")]
    [DataRow("0")]
    public void Parse_PersistentKeepaliveOffOrZero_GivesNoKeepalive(string value)
    {
        var configuration = WireGuardConfigurationParser.Parse(Minimal($"PersistentKeepalive = {value}"));

        Assert.IsNull(configuration.Peers[0].PersistentKeepalive);
    }

    [TestMethod]
    public void Parse_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WireGuardConfigurationParser.Parse(null!));
    }

    [TestMethod]
    public void Parse_MissingPrivateKey_NamesTheInterfaceLine()
    {
        var text = $"# comment\n[Interface]\nAddress = 10.0.0.2/24\n[Peer]\nPublicKey = {PublicKey}\n";

        AssertRejected(text, "line 2: [Interface] has no PrivateKey");
    }

    [TestMethod]
    public void Parse_MissingPublicKey_NamesThePeerLine()
    {
        AssertRejected($"[Interface]\nPrivateKey = {PrivateKey}\n[Peer]\nAllowedIPs = 0.0.0.0/0\n", "line 3: [Peer] has no PublicKey");
    }

    [TestMethod]
    public void Parse_NoPeer_NamesTheLastLine()
    {
        AssertRejected($"[Interface]\nPrivateKey = {PrivateKey}\n\n", "line 2: the file has no [Peer] section");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("# nothing but a comment")]
    public void Parse_NoInterface_NamesTheLastLine(string text)
    {
        AssertRejected(text, "line 1: the file has no [Interface] section");
    }

    [TestMethod]
    [DataRow("[Tunnel]", "line 3: unknown section '[Tunnel]'")]
    [DataRow("[Peer", "line 3: unknown section '[Peer'")]
    [DataRow("[Interface]", "line 3: a second [Interface] section")]
    [DataRow("Address = 10.0.0.1/33", "line 3: '10.0.0.1/33' is not a valid CIDR")]
    [DataRow("DNS", "line 3: 'DNS' is not 'key = value'")]
    [DataRow("= 1.1.1.1", "line 3: '= 1.1.1.1' is not 'key = value'")]
    [DataRow("Colour = blue", "line 3: unknown key 'Colour' in [Interface]")]
    [DataRow("PrivateKey = AAAA", "line 3: PrivateKey is not 32 bytes of base64")]
    [DataRow("ListenPort = 70000", "line 3: ListenPort '70000' is not a number from 0 to 65535")]
    [DataRow("MTU = 0", "line 3: MTU '0' is not a number from 1 to 65535")]
    public void Parse_BadInterfaceLine_NamesTheLine(string line, string message)
    {
        AssertRejected($"[Interface]\nPrivateKey = {PrivateKey}\n{line}\n[Peer]\nPublicKey = {PublicKey}\n", message);
    }

    [TestMethod]
    [DataRow("Endpoint = 2001:db8::1:51820", "line 5: '2001:db8::1:51820' is not a valid endpoint")]
    [DataRow("AllowedIPs = 10.0.0.0/8, nonsense", "line 5: 'nonsense' is not a valid CIDR")]
    [DataRow("PublicKey = AAAA", "line 5: PublicKey is not 32 bytes of base64")]
    [DataRow("PresharedKey = AAAA", "line 5: PresharedKey is not 32 bytes of base64")]
    [DataRow("PersistentKeepalive = soon", "line 5: PersistentKeepalive 'soon' is not a number from 0 to 65535")]
    [DataRow("Table = off", "line 5: unknown key 'Table' in [Peer]")]
    public void Parse_BadPeerLine_NamesTheLine(string line, string message)
    {
        AssertRejected(Minimal(line), message);
    }

    [TestMethod]
    public void Parse_SettingBeforeAnySection_NamesTheLine()
    {
        AssertRejected($"PrivateKey = {PrivateKey}\n[Interface]\n", $"line 1: 'PrivateKey = {PrivateKey}' is outside a section");
    }

    private static string Minimal(string peerLine = "") =>
        $"[Interface]\nPrivateKey = {PrivateKey}\n[Peer]\nPublicKey = {PublicKey}\n{peerLine}\n";

    private static void AssertRejected(string text, string message)
    {
        var exception = Assert.ThrowsExactly<WireGuardConfigurationException>(() => WireGuardConfigurationParser.Parse(text));

        Assert.AreEqual(message, exception.Message);
    }

    private static string Key(byte seed) =>
        Convert.ToBase64String(Enumerable.Range(seed, 32).Select(value => (byte)value).ToArray());
}
