using Curl.Protocol.Abstractions;

namespace Curl.Quic;

/// <summary>
/// Runs a <see cref="QuicClientHandshake" /> over a datagram channel until it completes,
/// fails or times out: sends its datagrams, hands it every datagram from the server's
/// endpoint, and ignores datagrams from anywhere else. The handshake must complete within
/// <c>--connect-timeout</c>, or within 10 seconds when none is given, as curl's ngtcp2
/// build requires (ADR-0144 section 5); time comes from the injected <see cref="TimeProvider" />.
/// </summary>
/// <param name="timeProvider">The clock the timeout runs on.</param>
public sealed class QuicClientConnector(TimeProvider timeProvider)
{
    /// <summary>How long the handshake may take when no connect timeout is given: curl's <c>QUIC_HANDSHAKE_TIMEOUT</c>.</summary>
    public static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(10);

    private const int ReceiveBufferLength = (int)QuicTransportParameters.DefaultMaxUdpPayloadSize;

    /// <summary>
    /// Runs the handshake. When the timeout fires first the client closes the connection as
    /// curl's build does: with <c>INTERNAL_ERROR</c> and exit 55 after the 10-second
    /// handshake timeout, with <c>NO_ERROR</c> and exit 28 after <paramref name="connectTimeout" />.
    /// </summary>
    /// <param name="handshake">The handshake, not yet started.</param>
    /// <param name="channel">The channel to the server.</param>
    /// <param name="connectTimeout">The <c>--connect-timeout</c>, or <see langword="null" /> when none was given.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns><see langword="null" /> when the handshake completed, otherwise why it failed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    public async Task<QuicHandshakeFailure?> RunHandshakeAsync(QuicClientHandshake handshake, IDatagramChannel channel, TimeSpan? connectTimeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handshake);
        ArgumentNullException.ThrowIfNull(channel);
        var startedAt = timeProvider.GetTimestamp();
        using var timeout = new CancellationTokenSource(connectTimeout ?? DefaultHandshakeTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await SendAsync(channel, handshake.Start(), linked.Token).ConfigureAwait(false);
            await ExchangeAsync(handshake, channel, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var milliseconds = (long)timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;
            var close = connectTimeout is null
                ? handshake.Abandon(QuicTransportErrorCode.InternalError, new QuicHandshakeFailure(CurlExitCode.SendError, "ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT"))
                : handshake.Abandon(QuicTransportErrorCode.NoError, new QuicHandshakeFailure(CurlExitCode.OperationTimedOut, $"Connection timed out after {milliseconds} milliseconds"));
            await SendAsync(channel, close, cancellationToken).ConfigureAwait(false);
        }

        return handshake.Failure;
    }

    private static async Task ExchangeAsync(QuicClientHandshake handshake, IDatagramChannel channel, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferLength];
        while (!handshake.IsComplete && handshake.Failure is null)
        {
            var received = await channel.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (received.RemoteEndPoint.Equals(channel.ServerEndPoint))
            {
                await SendAsync(channel, handshake.Receive(buffer.AsMemory(0, received.Length)), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task SendAsync(IDatagramChannel channel, IReadOnlyList<byte[]> datagrams, CancellationToken cancellationToken)
    {
        foreach (var datagram in datagrams)
        {
            await channel.SendAsync(datagram, channel.ServerEndPoint, cancellationToken).ConfigureAwait(false);
        }
    }
}
