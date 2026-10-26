using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-085 of the third-party gap report (BL-3614): curl 8.22.0 strips an IPv6 zone id from
/// the target in the CONNECT request line and its <c>Host</c> header, sending
/// <c>CONNECT [fe80::1]:8080</c> for <c>http://[fe80::1%25eth0]:8080/x</c> (measured).
/// </summary>
[TestClass]
public sealed class Cgr085Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("fe80::1%eth0")]
    [DataRow("[fe80::1%eth0]")]
    public void BuildConnectRequest_HostWithZoneId_SendsTheAddressWithoutIt(string host)
    {
        var proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 20600, null);
        Diagnostics.Arrange("host", host);

        var request = Encoding.Latin1.GetString(HttpProxyTunnel.BuildConnectRequest(host, 8080, proxy, HttpProxyTunnelOptions.Default, null));

        Diagnostics.Act("request", request);
        StringAssert.StartsWith(request, "CONNECT [fe80::1]:8080 HTTP/1.1\r\nHost: [fe80::1]:8080\r\n");
    }

    [TestMethod]
    [DataRow("fe80::1", "fe80::1")]
    [DataRow("[fe80::1]", "[fe80::1]")]
    [DataRow("example.com", "example.com")]
    [DataRow("a%b", "a%b")]
    public void WithoutZoneId_HostWithoutZoneId_IsUnchanged(string host, string expected)
    {
        var actual = HttpProxyTunnel.WithoutZoneId(host);

        Diagnostics.Act("actual", actual);
        Assert.AreEqual(expected, actual);
    }
}
