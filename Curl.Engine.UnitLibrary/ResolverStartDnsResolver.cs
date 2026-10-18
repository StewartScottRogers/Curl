using System.Net;
using Curl.Api.Callbacks;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Calls a handle's <c>CURLOPT_RESOLVER_START_FUNCTION</c> before each name look-up <paramref name="inner" /> makes
/// (ADR-0611). The connectors ask a resolver only when their DNS cache (and a <c>--resolve</c> entry in it) does not
/// answer, so the callback runs where curl 8.22.0's <c>Curl_resolv</c> calls it: before a resolve the cache missed,
/// an IP address and <c>localhost</c> included. It is given 0 for the resolver's state, the managed engine having no
/// c-ares channel to hand out. Any answer but 0 resolves nothing, with <see cref="DnsLookupFailure.AbortedByCallback" />,
/// so the connect fails with <c>CURLE_ABORTED_BY_CALLBACK</c> (exit 42) and <c>Could not resolve host: &lt;host&gt;</c>
/// (<c>proxy</c> for a proxy), as upstream test655 requires, and nothing is cached. A resolver's failure reason passes through.
/// </summary>
/// <param name="inner">Looks the names up.</param>
/// <param name="resolverStart">The handle's resolver-start callback.</param>
internal sealed class ResolverStartDnsResolver(IDnsResolver inner, ResolverStartCallback resolverStart) : IDnsResolverWithFailureReason
{
    /// <summary>
    /// Gives <paramref name="resolver" /> wrapped in a <see cref="ResolverStartDnsResolver" /> when
    /// <paramref name="resolverStart" /> is set, else <paramref name="resolver" /> itself.
    /// </summary>
    /// <param name="resolver">Looks the names up.</param>
    /// <param name="resolverStart">The handle's resolver-start callback, or <see langword="null" />.</param>
    /// <returns>The resolver the connectors look names up through.</returns>
    internal static IDnsResolver Over(IDnsResolver resolver, ResolverStartCallback? resolverStart) =>
        resolverStart is null ? resolver : new ResolverStartDnsResolver(resolver, resolverStart);

    /// <summary>Calls the callback, then resolves <paramref name="host" /> unless it refused.</summary>
    /// <param name="host">The host name or literal address to resolve.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The addresses, or none when the callback refused.</returns>
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        (await ResolveWithFailureReasonAsync(host, cancellationToken).ConfigureAwait(false)).Addresses;

    /// <summary>Calls the callback, then resolves <paramref name="host" /> with the inner resolver's failure reason unless it refused.</summary>
    /// <param name="host">The host name or literal address to resolve.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The addresses, or none and <see cref="DnsLookupFailure.AbortedByCallback" /> when the callback refused.</returns>
    public async ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, CancellationToken cancellationToken)
    {
        if (resolverStart(0) != 0)
        {
            return new DnsResolution([], DnsLookupFailure.AbortedByCallback);
        }

        return inner is IDnsResolverWithFailureReason withFailureReason
            ? await withFailureReason.ResolveWithFailureReasonAsync(host, cancellationToken).ConfigureAwait(false)
            : new DnsResolution(await inner.ResolveAsync(host, cancellationToken).ConfigureAwait(false), DnsLookupFailure.None);
    }
}
