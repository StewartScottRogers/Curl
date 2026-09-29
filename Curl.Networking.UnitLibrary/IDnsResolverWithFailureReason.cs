using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="IDnsResolver" /> that also says why a name did not resolve, so
/// <see cref="TcpConnector" /> and <see cref="UdpDatagramConnector" /> can print it as curl's
/// c-ares build does: <c>Could not resolve host: &lt;host&gt; (&lt;reason&gt;)</c>, or exit 43
/// for a <see cref="DnsLookupFailure.BadConfiguration" /> (BL-694).
/// </summary>
public interface IDnsResolverWithFailureReason : IDnsResolver
{
    /// <summary>Resolves <paramref name="host" />, reporting why it did not resolve.</summary>
    /// <param name="host">The host name or literal address to resolve.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The addresses, or why there are none.</returns>
    ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, CancellationToken cancellationToken);
}
