using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// The connection IDs the client issued to the server (RFC 9000 sections 5.1 and 19.16):
/// the source connection ID of its Initial packets, then more issued in NEW_CONNECTION_ID
/// frames up to the server's <c>active_connection_id_limit</c>. A RETIRE_CONNECTION_ID
/// removes one and issues its replacement, so the server always holds as many as it asked for.
/// </summary>
public sealed class QuicLocalConnectionIds
{
    /// <summary>The most connection IDs the client keeps live at once, as ngtcp2's pool of eight.</summary>
    public const int MaximumActive = 8;

    private readonly SortedDictionary<ulong, QuicConnectionIdEntry> active = [];

    private readonly ITlsRandomSource random;

    private readonly int connectionIdLength;

    private ulong nextSequenceNumber = 1;

    /// <summary>Initializes a new instance of the <see cref="QuicLocalConnectionIds" /> class.</summary>
    /// <param name="handshakeConnectionId">The source connection ID of the client's Initial packets, sequence number 0; every ID issued later has its length.</param>
    /// <param name="random">Where new connection IDs and stateless reset tokens come from.</param>
    public QuicLocalConnectionIds(byte[] handshakeConnectionId, ITlsRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(handshakeConnectionId);
        active[0] = new QuicConnectionIdEntry(0, handshakeConnectionId, null);
        connectionIdLength = handshakeConnectionId.Length;
        this.random = random;
    }

    /// <summary>Gets the live connection IDs, by sequence number.</summary>
    public IReadOnlyCollection<QuicConnectionIdEntry> Active => active.Values;

    /// <summary>Returns whether a packet's destination connection ID is one the client issued and has not seen retired.</summary>
    /// <param name="connectionId">The destination connection ID.</param>
    /// <returns><see langword="true" /> when it is live.</returns>
    public bool Contains(ReadOnlySpan<byte> connectionId)
    {
        foreach (var entry in active.Values)
        {
            if (entry.ConnectionId.AsSpan().SequenceEqual(connectionId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Issues new connection IDs until the server holds as many as it will take, at most <see cref="MaximumActive" />.</summary>
    /// <param name="serverActiveConnectionIdLimit">The server's <c>active_connection_id_limit</c>.</param>
    /// <returns>The NEW_CONNECTION_ID frames to send.</returns>
    public IReadOnlyList<QuicNewConnectionIdFrame> IssueUpTo(ulong serverActiveConnectionIdLimit)
    {
        var wanted = (int)Math.Min(serverActiveConnectionIdLimit, MaximumActive);
        List<QuicNewConnectionIdFrame> issued = [];
        while (active.Count < wanted)
        {
            issued.Add(IssueOne());
        }

        return issued;
    }

    /// <summary>Takes a RETIRE_CONNECTION_ID frame: removes the ID and issues its replacement.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The NEW_CONNECTION_ID frame that replaces it, or none when it was already retired.</returns>
    /// <exception cref="QuicTransportException">The sequence number was never issued (<see cref="QuicTransportErrorCode.ProtocolViolation" />).</exception>
    public IReadOnlyList<QuicNewConnectionIdFrame> Retire(QuicRetireConnectionIdFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.SequenceNumber >= nextSequenceNumber)
        {
            throw new QuicTransportException(QuicTransportErrorCode.ProtocolViolation, $"RETIRE_CONNECTION_ID names sequence number {frame.SequenceNumber}, which the client never issued.");
        }

        return active.Remove(frame.SequenceNumber) ? [IssueOne()] : [];
    }

    private QuicNewConnectionIdFrame IssueOne()
    {
        var connectionId = new byte[connectionIdLength];
        var resetToken = new byte[QuicNewConnectionIdFrame.StatelessResetTokenLength];
        random.Fill(connectionId);
        random.Fill(resetToken);
        var entry = new QuicConnectionIdEntry(nextSequenceNumber++, connectionId, resetToken);
        active[entry.SequenceNumber] = entry;
        return new QuicNewConnectionIdFrame(entry.SequenceNumber, 0, connectionId, resetToken);
    }
}
