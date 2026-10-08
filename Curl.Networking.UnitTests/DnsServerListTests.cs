using System.Net;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsServerList" /> to the <c>--dns-servers</c> values curl 8.22.0's c-ares
/// 1.34.8 build accepted and refused when it resolved a name (measured, BL-643 and BL-694).
/// </summary>
[TestClass]
public sealed class DnsServerListTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("192.0.2.1", "192.0.2.1:53")]
    [DataRow("192.0.2.1:5353", "192.0.2.1:5353")]
    [DataRow("192.0.2.1:0", "192.0.2.1:53")]
    [DataRow("::1", "[::1]:53")]
    [DataRow("[::1]", "[::1]:53")]
    [DataRow("[2001:db8::1]:5353", "[2001:db8::1]:5353")]
    [DataRow(" 192.0.2.1", "192.0.2.1:53")]
    [DataRow("192.0.2.1,", "192.0.2.1:53")]
    public void TryParse_OneEntry_ReturnsItsEndPoint(string text, string expected)
    {
        Diagnostics.Arrange("--dns-servers", $"\"{text}\"");

        var parsed = DnsServerList.TryParse(text, out var servers);

        Diagnostics.Act("parsed", parsed);
        Diagnostics.Act("servers", servers is null ? "(null)" : string.Join(", ", servers));
        Diagnostics.Assert("parsed", true, parsed);
        Assert.IsTrue(parsed);
        Assert.IsNotNull(servers);

        Diagnostics.Diff("server", expected, servers.Single().ToString());
        Assert.AreEqual(expected, servers.Single().ToString());
    }

    [TestMethod]
    public void TryParse_SeveralEntries_KeepsTheirOrderAndTrimsSpaces()
    {
        const string text = "192.0.2.1:15380, [::1]:53 ,192.0.2.2";
        Diagnostics.Arrange("--dns-servers", $"\"{text}\"");

        var parsed = DnsServerList.TryParse(text, out var servers);

        Diagnostics.Act("parsed", parsed);
        Diagnostics.Act("servers", servers is null ? "(null)" : string.Join(", ", servers));
        Diagnostics.Assert("parsed", true, parsed);
        Assert.IsTrue(parsed);
        Assert.IsNotNull(servers);

        Diagnostics.Diff("servers", "192.0.2.1:15380, [::1]:53, 192.0.2.2:53", string.Join(", ", servers!));
        CollectionAssert.AreEqual(
            new[] { "192.0.2.1:15380", "[::1]:53", "192.0.2.2:53" },
            servers.Select(server => server.ToString()).ToArray());
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("localhost:15380")]
    [DataRow("1.2.3.4:99999")]
    [DataRow("1.2.3.4;5.6.7.8")]
    [DataRow("1.2.3")]
    [DataRow("1.2.3.4:x")]
    [DataRow("1.2.3.4:+53")]
    [DataRow("::1:53:zz")]
    [DataRow("[::1")]
    [DataRow("[192.0.2.1]")]
    [DataRow("[::1]53")]
    [DataRow("2001:db8::1:")]
    [DataRow(",")]
    [DataRow("")]
    public void TryParse_AMalformedList_IsRefused(string text)
    {
        Diagnostics.Arrange("--dns-servers", $"\"{text}\"");

        var parsed = DnsServerList.TryParse(text, out var servers);

        Diagnostics.Act("parsed", parsed);
        Diagnostics.Act("servers", servers is null ? "(null)" : string.Join(", ", servers));
        Diagnostics.Assert("parsed", false, parsed);
        Assert.IsFalse(parsed);

        Diagnostics.Assert("servers is null", true, servers is null);
        Assert.IsNull(servers);
    }

    [TestMethod]
    public void TryParse_WithNull_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("--dns-servers", "(null)");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => DnsServerList.TryParse(null!, out _));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void TryParseAddress_AShortIPv4Form_IsRefusedAsInetPtonRefusesIt()
    {
        Diagnostics.Arrange("short form", "127.1");
        Diagnostics.Arrange("full form", "127.0.0.1");

        var shortParsed = DnsServerList.TryParseAddress("127.1", System.Net.Sockets.AddressFamily.InterNetwork, out _);
        var fullParsed = DnsServerList.TryParseAddress("127.0.0.1", System.Net.Sockets.AddressFamily.InterNetwork, out var parsedAddress);

        Diagnostics.Act("short form parsed", shortParsed);
        Diagnostics.Act("full form parsed", fullParsed);
        Diagnostics.Act("full form address", parsedAddress);
        Diagnostics.Assert("short form parsed", false, shortParsed);
        Diagnostics.Assert("full form parsed", true, fullParsed);
        Diagnostics.Assert("full form address", IPAddress.Loopback, parsedAddress);
        Assert.IsFalse(DnsServerList.TryParseAddress("127.1", System.Net.Sockets.AddressFamily.InterNetwork, out _));
        Assert.IsTrue(DnsServerList.TryParseAddress("127.0.0.1", System.Net.Sockets.AddressFamily.InterNetwork, out var address));
        Assert.AreEqual(IPAddress.Loopback, address);
    }
}
