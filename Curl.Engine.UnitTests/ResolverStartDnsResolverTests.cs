using System.Net;
using Curl.Api.Callbacks;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class ResolverStartDnsResolverTests
{
    private static readonly IPAddress Address = IPAddress.Parse("192.0.2.9");

    [TestMethod]
    public void Over_NoCallback_GivesTheResolverItself()
    {
        PlainResolver resolver = new();

        Assert.AreSame(resolver, ResolverStartDnsResolver.Over(resolver, resolverStart: null));
        Assert.IsInstanceOfType<ResolverStartDnsResolver>(ResolverStartDnsResolver.Over(resolver, _ => 0));
    }

    [TestMethod]
    public async Task ResolveAsync_CallbackAnswersZero_CallsItWithNoResolverStateThenResolves()
    {
        List<string> calls = [];
        PlainResolver inner = new(calls);
        ResolverStartDnsResolver resolver = new(inner, state =>
        {
            calls.Add($"start {state}");
            return 0;
        });

        IReadOnlyList<IPAddress> addresses = await resolver.ResolveAsync("h", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Address }, addresses.ToArray());
        CollectionAssert.AreEqual(new[] { "start 0", "resolve h" }, calls);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_CallbackAnswersNonZero_ResolvesNothingAndDoesNotAskTheResolver()
    {
        List<string> calls = [];
        ResolverStartDnsResolver resolver = new(new PlainResolver(calls), _ => 1);

        DnsResolution resolution = await resolver.ResolveWithFailureReasonAsync("h", CancellationToken.None);

        Assert.IsEmpty(resolution.Addresses);
        Assert.AreEqual(DnsLookupFailure.None, resolution.Failure);
        Assert.IsEmpty(calls);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_InnerGivesAReason_PassesTheReasonThrough()
    {
        DnsResolution refused = new([], DnsLookupFailure.BadConfiguration);
        ResolverStartDnsResolver resolver = new(new ReasonResolver(refused), _ => 0);

        Assert.AreSame(refused, await resolver.ResolveWithFailureReasonAsync("h", CancellationToken.None));
    }

    /// <summary>A resolver that answers <see cref="Address" /> for every name and records each look-up.</summary>
    private sealed class PlainResolver(List<string>? calls = null) : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            calls?.Add($"resolve {host}");
            return ValueTask.FromResult<IReadOnlyList<IPAddress>>([Address]);
        }
    }

    /// <summary>A resolver with a failure reason that answers one resolution.</summary>
    private sealed class ReasonResolver(DnsResolution resolution) : IDnsResolverWithFailureReason
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult(resolution.Addresses);

        public ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult(resolution);
    }
}
