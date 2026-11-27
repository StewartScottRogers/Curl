using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Without a <see cref="QuicDialer" /> this is the interface's answer: exit 7 <c>QUIC is not
    /// available on this connector</c>. With one, the <c>--resolve</c> entries load, a bad entry
    /// or matching <c>--connect-to</c> mapping fails with exit 49, and the (mapped) host resolves
    /// exactly as <see cref="ConnectAsync" /> resolves it, through the same DNS cache, <c>-4</c> or
    /// <c>-6</c> and <c>-v</c> lines, failing with exit 6 (ADR-0144 section 3). The addresses go
    /// to the <see cref="QuicDialer" /> with the connection's number, taken from the sequence
    /// <see cref="ConnectAsync" /> takes from.
    /// </para>
    /// <para>
    /// A <c>--connect-timeout</c> greater than zero bounds the handshakes; without one each
    /// handshake has QUIC's own 10 seconds (ADR-0144 section 5). An HTTP or HTTPS proxy on the
    /// target is tunnelled through with CONNECT-UDP, as curl 8.22.0 does (BL-942,
    /// <see cref="ConnectMultiplexedThroughProxyAsync" />); a SOCKS one is not used.
    /// </para>
    /// </remarks>
    public async ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (quicDialer is null)
        {
            return MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "QUIC is not available on this connector");
        }

        if (UdpTunnelProxyOf(target) is { } proxy)
        {
            return await ConnectMultiplexedThroughProxyAsync(target, proxy, quicDialer, cancellationToken).ConfigureAwait(false);
        }

        var log = new NetworkDiagnosticLog(target.DiagnosticLog);
        var (traced, httpsConnect, attempt) = QuicConnectionFilters(target);
        var (request, failure) = await ResolveForQuicAsync(traced, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            log.Failed(DiagnosticLogComponents.Quic, failure!.ExitCode, failure.ErrorMessage);
            ReportHttpsConnectAttemptFailure(httpsConnect, attempt, failure.ExitCode);
            return failure;
        }

        // The QUIC steps are logged around the dial, so Curl.Quic needs no diagnostic log (BL-920).
        log.QuicDialling(request.DestinationHost, request.Port, request.Addresses);
        var dialled = await quicDialer.DialAsync(request, cancellationToken).ConfigureAwait(false);
        log.QuicDialled(request.DestinationHost, request.Port, dialled);
        if (dialled.Connection is null)
        {
            ReportHttpsConnectAttemptFailure(httpsConnect, attempt, dialled.ExitCode);
        }

        return dialled;
    }

    // Everything ConnectMultiplexedAsync does before the QUIC dial: the --resolve entries, the
    // --connect-to mapping and the resolve, with ConnectAsync's failures for each.
    private ValueTask<(QuicDialRequest? Request, MultiplexedConnectResult? Failure)> ResolveForQuicAsync(
        ConnectTarget target,
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        LoadResolveEntriesUnlessLoaded(target.Events);
        var destination = DestinationOf(target);
        if ((_resolveOverrides.ParseError ?? destination.ParseError) is { } parseError)
        {
            return ValueTask.FromResult<(QuicDialRequest?, MultiplexedConnectResult?)>((null, MultiplexedConnectResult.Failed(_resolveOverrides.ParseFailureCodeWith(destination), parseError)));
        }

        if (OnionAddress.IsRefused(destination.Host))
        {
            ReportOnionRefused(target.Events, destination);
            return ValueTask.FromResult<(QuicDialRequest?, MultiplexedConnectResult?)>((null, MultiplexedConnectResult.Failed(CurlExitCode.CouldntResolveHost, OnionAddress.RefusalMessage)));
        }

        return ResolveQuicDestinationAsync(target, destination, started, cancellationToken);
    }

    private async ValueTask<(QuicDialRequest? Request, MultiplexedConnectResult? Failure)> ResolveQuicDestinationAsync(
        ConnectTarget target,
        ConnectDestination destination,
        long started,
        CancellationToken cancellationToken)
    {
        var (addresses, failure) = await ResolveWithFailureReasonAsync(destination.Host, destination.Port, target, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveHost, "host", destination.Host, destination.Port, failure);
            return (null, MultiplexedConnectResult.Failed(exitCode, message));
        }

        return (new QuicDialRequest(
            target,
            destination.Host,
            destination.Port,
            addresses,
            started,
            timeProvider.GetTimestamp(),
            connectTimeout > TimeSpan.Zero ? connectTimeout : null,
            TakeConnectionNumber(),
            LocalAddressChooser()), null);
    }
}
