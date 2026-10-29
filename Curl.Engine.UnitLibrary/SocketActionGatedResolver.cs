using System.Net;

using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The resolver of a transfer in a multi handle driven by <c>curl_multi_socket_action</c>: while a look-up runs the
/// transfer waits readable (<see cref="TransferSocketWait.BeginResolve" />) on a <see cref="LoopbackLookupWakeup" />,
/// which the look-up's end signals before <see cref="TransferSocketWait.EndResolve" />, as curl 8.22.0's threaded
/// resolver tells its wakeup socket with <c>CURL_POLL_IN</c> and writes to it when the lookup ends (BL-4036,
/// ADR-0983). The look-up itself is the inner resolver's, its failure reason kept.
/// </summary>
/// <param name="inner">The resolver that looks the names up.</param>
/// <param name="wait">What the transfer waits for on its socket.</param>
internal sealed class SocketActionGatedResolver(IDnsResolver inner, TransferSocketWait wait) : IDnsResolverWithFailureReason
{
    /// <summary>
    /// Gives <paramref name="resolver" /> gated on <paramref name="wait" />, or <paramref name="resolver" /> itself
    /// outside socket-action mode (<paramref name="wait" /> <see langword="null" />).
    /// </summary>
    /// <param name="resolver">The resolver that looks the names up.</param>
    /// <param name="wait">What the transfer waits for on its socket, or <see langword="null" />.</param>
    /// <returns>The resolver the transfer looks names up through.</returns>
    internal static IDnsResolver Over(IDnsResolver resolver, TransferSocketWait? wait) =>
        wait is null ? resolver : new SocketActionGatedResolver(resolver, wait);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        (await ResolveWithFailureReasonAsync(host, cancellationToken).ConfigureAwait(false)).Addresses;

    /// <inheritdoc />
    public async ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, CancellationToken cancellationToken)
    {
        LoopbackLookupWakeup wakeup = LoopbackLookupWakeup.Open();
        wait.BeginResolve(wakeup.Handle, wakeup);
        try
        {
            return inner is IDnsResolverWithFailureReason withFailureReason
                ? await withFailureReason.ResolveWithFailureReasonAsync(host, cancellationToken).ConfigureAwait(false)
                : new DnsResolution(await inner.ResolveAsync(host, cancellationToken).ConfigureAwait(false), DnsLookupFailure.None);
        }
        finally
        {
            wakeup.Signal();
            wait.EndResolve();
        }
    }
}
