using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Shuffles the addresses <paramref name="inner" /> resolves with <see cref="DnsAddressShuffle" />, for
/// <c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c>. The connectors cache what their resolver answers, so the shuffled order is
/// the one cached, as curl 8.22.0 shuffles a resolved entry before it enters the DNS cache. A resolver's failure
/// reason passes through.
/// </summary>
/// <param name="inner">Looks the names up.</param>
/// <param name="randomBelow">Gives a random index from zero up to, not including, its argument.</param>
public sealed class ShufflingDnsResolver(IDnsResolver inner, Func<int, int> randomBelow) : IDnsResolverWithFailureReason
{
    /// <summary>Gets the resolver whose addresses this one shuffles, for callers that act on the kind of resolver they were given.</summary>
    public IDnsResolver Inner => inner;

    /// <summary>
    /// Gives <paramref name="resolver" /> wrapped in a <see cref="ShufflingDnsResolver" /> over the cryptographic
    /// generator when <paramref name="shuffleAddresses" /> is set, else <paramref name="resolver" /> itself.
    /// </summary>
    /// <param name="resolver">Looks the names up.</param>
    /// <param name="shuffleAddresses">The handle's <c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c>.</param>
    /// <returns>The resolver the connectors look names up through.</returns>
    public static IDnsResolver Over(IDnsResolver resolver, bool shuffleAddresses) =>
        shuffleAddresses ? new ShufflingDnsResolver(resolver, DnsAddressShuffle.CryptographicRandomBelow) : resolver;

    /// <summary>Shuffles <paramref name="addresses" /> the way this resolver shuffles its own answers, for a caller that took them from <see cref="Inner" /> by another call.</summary>
    /// <param name="addresses">The addresses in the order they resolved.</param>
    /// <returns>The addresses, shuffled.</returns>
    public IReadOnlyList<IPAddress> Shuffle(IReadOnlyList<IPAddress> addresses) => DnsAddressShuffle.Shuffle(addresses, randomBelow);

    /// <summary>Resolves <paramref name="host" /> and shuffles its addresses.</summary>
    /// <param name="host">The host name or literal address to resolve.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The addresses, shuffled.</returns>
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        (await ResolveWithFailureReasonAsync(host, cancellationToken).ConfigureAwait(false)).Addresses;

    /// <summary>Resolves <paramref name="host" /> with the inner resolver's failure reason and shuffles its addresses.</summary>
    /// <param name="host">The host name or literal address to resolve.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The addresses, shuffled, and the inner resolver's failure reason.</returns>
    public async ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, CancellationToken cancellationToken)
    {
        DnsResolution resolution = inner is IDnsResolverWithFailureReason withFailureReason
            ? await withFailureReason.ResolveWithFailureReasonAsync(host, cancellationToken).ConfigureAwait(false)
            : new DnsResolution(await inner.ResolveAsync(host, cancellationToken).ConfigureAwait(false), DnsLookupFailure.None);
        return resolution with { Addresses = DnsAddressShuffle.Shuffle(resolution.Addresses, randomBelow) };
    }
}
