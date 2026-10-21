using System.Net;
using Curl.Api.Options.Network;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>Pins that <c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c> puts a <see cref="ShufflingDnsResolver" /> under the connectors' look-ups (BL-2922).</summary>
[TestClass]
public sealed class EngineCompositionDnsShuffleTests
{
    [TestMethod]
    public void LookUpsOf_ShuffleOff_WrapsTheResolverItselfSoTheOrderIsUnchanged()
    {
        FixedResolver resolver = new();
        IDnsResolver? wrapped = null;

        IDnsResolver lookUps = EngineComposition.LookUpsOf(resolver, new EasyNetworkOptions(), inner => wrapped = inner);

        Assert.AreSame(resolver, wrapped);
        Assert.AreSame(resolver, lookUps);
    }

    [TestMethod]
    public async Task LookUpsOf_ShuffleOn_WrapsAShufflingResolverOverTheResolver()
    {
        FixedResolver resolver = new();

        IDnsResolver lookUps = EngineComposition.LookUpsOf(resolver, new EasyNetworkOptions { DnsShuffleAddresses = true }, inner => inner);

        Assert.IsInstanceOfType<ShufflingDnsResolver>(lookUps);
        CollectionAssert.AreEquivalent(FixedResolver.Addresses, (await lookUps.ResolveAsync("h", CancellationToken.None)).ToArray());
    }

    private sealed class FixedResolver : IDnsResolver
    {
        public static readonly IPAddress[] Addresses = [IPAddress.Parse("192.0.2.1"), IPAddress.Parse("192.0.2.2")];

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>(Addresses);
    }
}
