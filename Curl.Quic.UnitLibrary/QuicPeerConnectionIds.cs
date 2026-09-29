namespace Curl.Quic;

/// <summary>
/// The connection IDs the server issued to the client (RFC 9000 sections 5.1 and 19.15):
/// the one from its first Initial packet, then each NEW_CONNECTION_ID. It retires those
/// below a Retire Prior To field, answering each with RETIRE_CONNECTION_ID, moves to the
/// lowest live ID when the one in use is retired, and refuses more live IDs than the
/// client's <c>active_connection_id_limit</c>.
/// </summary>
public sealed class QuicPeerConnectionIds
{
    private readonly SortedDictionary<ulong, QuicConnectionIdEntry> active = [];

    private readonly int limit;

    private ulong retiredBelow;

    /// <summary>Initializes a new instance of the <see cref="QuicPeerConnectionIds" /> class.</summary>
    /// <param name="handshakeConnectionId">The source connection ID of the server's first Initial packet, sequence number 0.</param>
    /// <param name="activeConnectionIdLimit">The <c>active_connection_id_limit</c> the client declared.</param>
    public QuicPeerConnectionIds(byte[] handshakeConnectionId, ulong activeConnectionIdLimit)
    {
        active[0] = new QuicConnectionIdEntry(0, handshakeConnectionId, null);
        Current = active[0];
        limit = (int)Math.Min(activeConnectionIdLimit, int.MaxValue);
    }

    /// <summary>Gets the connection ID the client sends to.</summary>
    public QuicConnectionIdEntry Current { get; private set; }

    /// <summary>Gets the live connection IDs, by sequence number.</summary>
    public IReadOnlyCollection<QuicConnectionIdEntry> Active => active.Values;

    /// <summary>Gives the handshake's connection ID the stateless reset token the server's transport parameters carried.</summary>
    /// <param name="statelessResetToken">The token, or <see langword="null" /> when the server sent none.</param>
    public void SetHandshakeResetToken(byte[]? statelessResetToken)
    {
        active[0] = active[0] with { StatelessResetToken = statelessResetToken };
        Current = active[0];
    }

    /// <summary>Takes a NEW_CONNECTION_ID frame and returns the RETIRE_CONNECTION_ID frames it calls for.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The frames to send, one per ID retired.</returns>
    /// <exception cref="QuicTransportException">A sequence number is reused for another ID (<see cref="QuicTransportErrorCode.ProtocolViolation" />), or the server leaves more live IDs than the limit (<see cref="QuicTransportErrorCode.ConnectionIdLimitError" />).</exception>
    public IReadOnlyList<QuicRetireConnectionIdFrame> Receive(QuicNewConnectionIdFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (active.TryGetValue(frame.SequenceNumber, out var known))
        {
            return known.ConnectionId.AsSpan().SequenceEqual(frame.ConnectionId.Span)
                ? []
                : throw new QuicTransportException(QuicTransportErrorCode.ProtocolViolation, $"NEW_CONNECTION_ID reuses sequence number {frame.SequenceNumber} for another connection ID.");
        }

        if (frame.SequenceNumber < retiredBelow)
        {
            return [new QuicRetireConnectionIdFrame(frame.SequenceNumber)];
        }

        active[frame.SequenceNumber] = new QuicConnectionIdEntry(frame.SequenceNumber, frame.ConnectionId.ToArray(), frame.StatelessResetToken.ToArray());
        var retired = RetireBelow(frame.RetirePriorTo);
        return active.Count <= limit
            ? retired
            : throw new QuicTransportException(QuicTransportErrorCode.ConnectionIdLimitError, $"The server left {active.Count} connection IDs live; the limit is {limit}.");
    }

    private List<QuicRetireConnectionIdFrame> RetireBelow(ulong retirePriorTo)
    {
        retiredBelow = Math.Max(retiredBelow, retirePriorTo);
        List<QuicRetireConnectionIdFrame> retired = [.. active.Keys.Where(sequence => sequence < retiredBelow).Select(sequence => new QuicRetireConnectionIdFrame(sequence))];
        foreach (var frame in retired)
        {
            active.Remove(frame.SequenceNumber);
        }

        Current = active.GetValueOrDefault(Current.SequenceNumber) ?? active.First().Value;
        return retired;
    }
}
