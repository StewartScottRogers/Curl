using System.Net;
using System.Net.Sockets;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-151: each library easy handle looks names up in a DNS cache of its own, as libcurl 8.22.0's
/// <c>dnscache_get</c> takes the share's or the handle's own multi's, so a new handle's
/// <c>CURLOPT_RESOLVER_START_FUNCTION</c> is called again and its abort answers <c>CURLE_ABORTED_BY_CALLBACK</c> (ADR-0847).
/// </summary>
[TestClass]
public sealed class Cgr151Tests
{
    [TestMethod]
    public async Task PerformAsync_NewHandleAfterAnotherResolvedTheName_CallsItsResolverStartCallbackAndHonoursTheAbort()
    {
        CountingResolver resolver = new();
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: new EngineDialing(new RefusingDialer(), resolver, new UdpDatagramConnector(resolver, TimeProvider.System)));
        await using (IEasyTransfer first = engine.CreateEasy())
        {
            first.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://dnsp1.example/" } };
            _ = await first.PerformAsync(CancellationToken.None);
        }

        int calls = 0;
        await using IEasyTransfer second = engine.CreateEasy();
        second.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://dnsp1.example/" },
            Callbacks = new() { ResolverStart = _ => ++calls },
        };

        CurlCode code = await second.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(1, resolver.Lookups);
    }

    [TestMethod]
    public void WithOwnCaches_TwoHandles_GivesEachAResolvedNamesCacheOfItsOwn()
    {
        ManagedEngineSharedState engine = new(TimeProvider.System, _ => string.Empty);

        ManagedEngineSharedState first = engine.WithOwnCaches();
        ManagedEngineSharedState second = engine.WithOwnCaches();

        Assert.AreNotSame(engine.DnsCache, first.DnsCache);
        Assert.AreNotSame(first.DnsCache, second.DnsCache);
    }

    /// <summary>A resolver that answers the loopback address and counts its look-ups.</summary>
    private sealed class CountingResolver : IDnsResolver
    {
        public int Lookups { get; private set; }

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            Lookups++;
            return ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
        }
    }

    /// <summary>A dialer whose every dial is refused, so a transfer resolves its name and opens no connection.</summary>
    private sealed class RefusingDialer : ITcpDialer
    {
        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
            throw new SocketException((int)SocketError.ConnectionRefused);

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            throw new SocketException((int)SocketError.ConnectionRefused);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new SocketException((int)SocketError.ConnectionRefused);
    }
}
