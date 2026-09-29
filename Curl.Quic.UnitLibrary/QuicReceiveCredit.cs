namespace Curl.Quic;

/// <summary>
/// One of this endpoint's limits on what the peer may do, for one stream's data
/// (MAX_STREAM_DATA), the connection's data (MAX_DATA) or the streams the peer may open
/// (MAX_STREAMS), per RFC 9000 section 4: the limit advertised, the highest offset or count
/// the peer has reached, and how much of it is consumed (read by the application, or, for
/// streams, closed). The limit moves by a fixed window: once less than half the window is
/// left above what was consumed, the new limit is what was consumed plus the window, as
/// ngtcp2 does when curl turns window auto-tuning off (ADR-0144, ADR-0172).
/// </summary>
/// <param name="initialLimit">The initial limit, which is also the window each raise restores.</param>
/// <param name="violation">The error a peer that goes past the limit is closed with.</param>
/// <param name="what">What the limit covers, for the error's reason, such as <c>stream 4</c>.</param>
internal sealed class QuicReceiveCredit(ulong initialLimit, QuicTransportErrorCode violation, string what)
{
    private readonly ulong window = initialLimit;

    /// <summary>Gets the limit advertised to the peer.</summary>
    public ulong Limit { get; private set; } = initialLimit;

    /// <summary>Gets the highest offset, total or count the peer has reached.</summary>
    public ulong Received { get; private set; }

    /// <summary>Gets how much of it is consumed, which the window counts from.</summary>
    public ulong Consumed { get; private set; }

    /// <summary>Gets a value indicating whether less than half the window is left above what was consumed, so a raised limit is due.</summary>
    public bool IsRaiseDue => Limit < Consumed + ((window + 1) / 2);

    /// <summary>Records that the peer has reached <paramref name="highest" />; returns by how much that moves the highest it had reached.</summary>
    /// <param name="highest">The offset, total or count the peer has now reached.</param>
    /// <returns>The increase, zero when nothing new was reached.</returns>
    /// <exception cref="QuicTransportException"><paramref name="highest" /> is past the limit, which is the violation this credit was made with.</exception>
    public ulong Receive(ulong highest)
    {
        if (highest > Limit)
        {
            throw new QuicTransportException(violation, $"The peer reached {highest} on {what}, past its limit of {Limit}.");
        }

        var increase = highest > Received ? highest - Received : 0;
        Received += increase;
        return increase;
    }

    /// <summary>Records what the application has consumed.</summary>
    /// <param name="amount">How much.</param>
    public void Consume(ulong amount) => Consumed += amount;

    /// <summary>Raises the limit to what was consumed plus the window and returns it, for the frame that advertises it.</summary>
    /// <returns>The new limit.</returns>
    public ulong TakeRaise() => Limit = Consumed + window;
}
