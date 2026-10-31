namespace Curl.Extension.Protocol.Coap.Dtls.Retransmission;

/// <summary>
/// Sends a DTLS flight and resends it until the peer's next flight starts (RFC 6347 section 4.2.4):
/// the timer starts at 1 second and doubles after each resend up to 60 seconds, and after
/// <see cref="MaximumRetransmissions" /> resends with no reply the handshake gives up (ADR-1061).
/// </summary>
/// <param name="sendDatagramAsync">Sends one datagram of the flight to the peer.</param>
/// <param name="timeProvider">The clock the retransmission timer runs on.</param>
internal sealed class DtlsFlightRetransmitter(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> sendDatagramAsync, TimeProvider timeProvider)
{
    /// <summary>How many times a flight is resent before the handshake gives up, OpenSSL's DTLS1_TMO_ALERT_COUNT (ADR-1061).</summary>
    public const int MaximumRetransmissions = 12;

    /// <summary>The first retransmission timeout, the 1 second RFC 6347 section 4.2.4.1 recommends.</summary>
    public static readonly TimeSpan InitialTimeout = TimeSpan.FromSeconds(1);

    /// <summary>The largest retransmission timeout, the 60 seconds RFC 6347 section 4.2.4.1 recommends.</summary>
    public static readonly TimeSpan MaximumTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Sends <paramref name="flight" /> and resends it on the doubling timer until
    /// <paramref name="awaitNextFlightAsync" /> completes, the start of the peer's next flight.
    /// </summary>
    /// <typeparam name="TReply">What the start of the peer's next flight gives.</typeparam>
    /// <param name="flight">The datagrams of the flight, sent in order each time.</param>
    /// <param name="awaitNextFlightAsync">Completes when the peer's next flight starts; cancelled once this method returns.</param>
    /// <param name="cancellationToken">Cancels the wait and the resends.</param>
    /// <returns>What <paramref name="awaitNextFlightAsync" /> gave.</returns>
    /// <exception cref="TimeoutException">The peer did not answer after <see cref="MaximumRetransmissions" /> resends; the caller ends with exit 35.</exception>
    public async ValueTask<TReply> SendFlightAsync<TReply>(
        IReadOnlyList<ReadOnlyMemory<byte>> flight,
        Func<CancellationToken, Task<TReply>> awaitNextFlightAsync,
        CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            return await ResendUntilReplyAsync(flight, awaitNextFlightAsync, stop.Token).ConfigureAwait(false);
        }
        finally
        {
            stop.Cancel();
        }
    }

    /// <summary>The timeout after <paramref name="timeout" /> expires: doubled, but at most <see cref="MaximumTimeout" />.</summary>
    internal static TimeSpan NextTimeout(TimeSpan timeout) => timeout * 2 > MaximumTimeout ? MaximumTimeout : timeout * 2;

    private async ValueTask<TReply> ResendUntilReplyAsync<TReply>(
        IReadOnlyList<ReadOnlyMemory<byte>> flight,
        Func<CancellationToken, Task<TReply>> awaitNextFlightAsync,
        CancellationToken stop)
    {
        Task<TReply> reply = awaitNextFlightAsync(stop);
        TimeSpan timeout = InitialTimeout;
        for (int retransmissions = 0; ; retransmissions++)
        {
            await SendAsync(flight, stop).ConfigureAwait(false);
            Task delay = Task.Delay(timeout, timeProvider, stop);
            if (await Task.WhenAny(reply, delay).ConfigureAwait(false) == reply)
            {
                return await reply.ConfigureAwait(false);
            }

            stop.ThrowIfCancellationRequested();
            if (retransmissions == MaximumRetransmissions)
            {
                throw new TimeoutException($"DTLS peer did not answer after {MaximumRetransmissions} retransmissions");
            }

            timeout = NextTimeout(timeout);
        }
    }

    private async ValueTask SendAsync(IReadOnlyList<ReadOnlyMemory<byte>> flight, CancellationToken stop)
    {
        foreach (ReadOnlyMemory<byte> datagram in flight)
        {
            await sendDatagramAsync(datagram, stop).ConfigureAwait(false);
        }
    }
}
