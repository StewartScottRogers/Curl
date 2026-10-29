using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-221 of the third-party gap report (BL-3750): curl 8.22.0 writes <c>[DNS] added H:P:A to
/// cache</c> for a <c>--resolve</c> entry and brackets an IPv6 literal host in the
/// <c>Established connection to</c> line (measured with curl 8.22.0 Schannel).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_ToAnIpv6LiteralHost_ReportsTheConnectionOpenedWithTheHostBracketed()
    {
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection(), LocalEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 55116) };
        var connector = CreateConnector(new FakeDnsResolver(IPAddress.IPv6Loopback), dialer, new FakeTlsProvider());

        await ConnectLoggedAsync(connector, new ConnectTarget("::1", 20700, UseTls: false) { Events = events });

        Assert.HasCount(1, events.Opened);
        Assert.AreEqual("[::1]", events.Opened[0].HostName);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAResolveEntry_ReportsItWithTheDnsPrefixedWording()
    {
        var connector = CreateConnectorWithResolveEntries("foo.test:20700:127.0.0.1");
        var events = new RecordingTransferEvents();

        await ConnectLoggedAsync(connector, new ConnectTarget("foo.test", 20700, UseTls: false) { Events = events });

        Assert.AreEqual("[DNS] added foo.test:20700:127.0.0.1 to cache", events.Info[0]);
    }
}
