using System.Net;
using System.Security.Cryptography;
using Curl.Extension.WireGuard.Tunnel;
using Curl.Extension.WireGuard.UnitTests.IpStack;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>Checks that host names curl's connector looks up under <c>--wireguard</c> go through the tunnel's DNS, never the system resolver (BL-4392).</summary>
[TestClass]
public sealed class WireGuardTunnelDnsResolverTests
{
    [TestMethod]
    public async Task ResolveAsync_HostName_AsksTheTunnelsDnsServerAndGivesTheAnswerWithoutTheCurlResolver()
    {
        TunnelTestPeer peer = new();
        WireGuardResolverHook hook = new(TunnelTestPeer.Configuration(extraInterfaceLine: "DNS = 10.0.0.53"), peer, new ManualTimeProvider(), RandomNumberGenerator.Create());
        IDnsResolver resolver = hook.ResolverOver(new ThrowingDnsResolver());

        Task<IReadOnlyList<IPAddress>> resolving = resolver.ResolveAsync("example.com", CancellationToken.None).AsTask();
        var (dnsPacket, dnsSyn, query) = await WireGuardConnectorTests.AcceptDnsQueryAsync(peer);
        await WireGuardConnectorTests.AnswerAndCloseAsync(peer, dnsPacket, dnsSyn, query, WireGuardDnsMessageTests.Record([0xC0, 0x0C], WireGuardDnsMessage.AddressType, 1, [10, 0, 0, 1]));
        IReadOnlyList<IPAddress> addresses = await resolving;

        Assert.AreEqual(IPAddress.Parse("10.0.0.53"), dnsPacket.Destination);
        CollectionAssert.AreEqual(WireGuardDnsMessage.EncodeQuery(WireGuardConnectorTests.IdOf(query), "example.com", WireGuardDnsMessage.AddressType), query);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("10.0.0.1") }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_NoDnsServerToAsk_GivesNoAddressAndOpensNothing()
    {
        TunnelTestPeer peer = new();
        WireGuardResolverHook hook = new(TunnelTestPeer.Configuration(), peer, new ManualTimeProvider(), RandomNumberGenerator.Create());

        IReadOnlyList<IPAddress> addresses = await hook.ResolverOver(new ThrowingDnsResolver()).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.IsNull(peer.OpenedEndpoint);
    }

    [TestMethod]
    [DataRow("10.0.0.1", "10.0.0.1")]
    [DataRow("[fd00::1]", "fd00::1")]
    public async Task ResolveAsync_AddressLiteral_IsItsOwnAnswerAndOpensNothing(string host, string expected)
    {
        TunnelTestPeer peer = new();
        WireGuardResolverHook hook = new(TunnelTestPeer.Configuration(extraInterfaceLine: "DNS = 10.0.0.53"), peer, new ManualTimeProvider(), RandomNumberGenerator.Create());

        IReadOnlyList<IPAddress> addresses = await hook.ResolverOver(new ThrowingDnsResolver()).ResolveAsync(host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse(expected) }, addresses.ToArray());
        Assert.IsNull(peer.OpenedEndpoint);
    }

    private sealed class ThrowingDnsResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the system resolver must not be asked");
    }
}
