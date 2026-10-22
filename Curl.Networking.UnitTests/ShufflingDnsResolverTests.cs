using System.Net;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

[TestClass]
public sealed class ShufflingDnsResolverTests
{
    private static readonly IPAddress First = IPAddress.Parse("192.0.2.1");
    private static readonly IPAddress Second = IPAddress.Parse("192.0.2.2");
    private static readonly IPAddress Third = IPAddress.Parse("2001:db8::3");

    [TestMethod]
    public void Over_ShuffleOff_GivesTheResolverItselfSoTheOrderIsUnchanged()
    {
        FakeDnsResolver resolver = new(First, Second);

        Assert.AreSame(resolver, ShufflingDnsResolver.Over(resolver, shuffleAddresses: false));
        Assert.IsInstanceOfType<ShufflingDnsResolver>(ShufflingDnsResolver.Over(resolver, shuffleAddresses: true));
    }

    [TestMethod]
    public async Task ResolveAsync_PlainResolver_ShufflesItsAddresses()
    {
        ShufflingDnsResolver resolver = new(new FakeDnsResolver(First, Second, Third), _ => 0);

        IReadOnlyList<IPAddress> addresses = await resolver.ResolveAsync("h", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Second, Third, First }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ResolverWithFailureReason_KeepsTheReasonAndShufflesTheAddresses()
    {
        ShufflingDnsResolver resolver = new(new ReasoningDnsResolver(new DnsResolution([First, Second], DnsLookupFailure.None)), _ => 0);

        DnsResolution resolution = await resolver.ResolveWithFailureReasonAsync("h", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Second, First }, resolution.Addresses.ToArray());
        Assert.AreEqual(DnsLookupFailure.None, resolution.Failure);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_NothingResolved_PassesTheFailureThrough()
    {
        ShufflingDnsResolver resolver = new(new ReasoningDnsResolver(new DnsResolution([], DnsLookupFailure.AbortedByCallback)), _ => 0);

        DnsResolution resolution = await resolver.ResolveWithFailureReasonAsync("h", CancellationToken.None);

        Assert.IsEmpty(resolution.Addresses);
        Assert.AreEqual(DnsLookupFailure.AbortedByCallback, resolution.Failure);
    }

    [TestMethod]
    public async Task ResolveAsync_ShuffleOnOverTheCryptographicGenerator_KeepsEveryAddress()
    {
        IDnsResolver resolver = ShufflingDnsResolver.Over(new FakeDnsResolver(First, Second, Third), shuffleAddresses: true);

        IReadOnlyList<IPAddress> addresses = await resolver.ResolveAsync("h", CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { First, Second, Third }, addresses.ToArray());
    }
}
