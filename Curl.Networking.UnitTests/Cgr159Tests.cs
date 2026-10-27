using System.Net;
using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// CGR-159: curl 8.22.0 answers a fully qualified <c>localhost.</c> (and a name under
/// <c>.localhost.</c>) itself, as it answers <c>localhost</c>: with <c>--doh-url</c>,
/// <c>http://localhost.:21802/x</c> sends no DoH query and dials <c>::1</c> (measured on
/// curl 8.22.0 Schannel: exit 0, <c>::1 1</c>).
/// </summary>
[TestClass]
public sealed class Cgr159Tests
{
    [TestMethod]
    [DataRow("localhost.")]
    [DataRow("LOCALHOST.")]
    [DataRow("curlmachine.localhost.")]
    public async Task ResolveWithFailureReasonAsync_LocalhostWithTrailingDot_AnswersLoopbackAndSendsNothing(string host)
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, new Uri("http://127.0.0.1:21800/dns-query"));

        var resolution = await resolver.ResolveWithFailureReasonAsync(host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback, IPAddress.Loopback }, resolution.Addresses.ToArray());
        Assert.IsEmpty(connector.Opened);
    }

    [TestMethod]
    [DataRow("localhost.", true)]
    [DataRow("a.localhost.", true)]
    [DataRow("localhost..", false)]
    [DataRow("notlocalhost.", false)]
    [DataRow(".", false)]
    public void IsLocalhost_NameWithTrailingDot_IsLocalhostOnlyWithoutTheDot(string host, bool expected) =>
        Assert.AreEqual(expected, TcpConnector.IsLocalhost(host));
}
