namespace Curl.Quic;

/// <summary>
/// The streams of one client connection and their flow control (RFC 9000 sections 2 to 4),
/// with no I/O. It opens client-initiated bidirectional (IDs 0, 4, 8, ...) and
/// unidirectional (2, 6, 10, ...) streams up to the server's MAX_STREAMS, sending
/// STREAMS_BLOCKED once per limit when it runs out; creates the server's streams, and every
/// lower-numbered one of the same type, when a frame first names them, refusing one past
/// the client's limit with <see cref="QuicTransportErrorCode.StreamLimitError" />; routes
/// the stream frames of RFC 9000 sections 19.4 to 19.14 to their streams; holds what is
/// sent to the server's MAX_DATA and each stream's MAX_STREAM_DATA, sending DATA_BLOCKED
/// and STREAM_DATA_BLOCKED once per limit; and raises the client's MAX_DATA,
/// MAX_STREAM_DATA and MAX_STREAMS as data is read and the server's streams close. A frame
/// that breaks a rule is a <see cref="QuicTransportException" /> carrying the error the
/// connection closes with.
/// </summary>
public sealed class QuicStreamSet
{
    /// <summary>The most a STREAM frame takes besides its data: the type, a stream ID and an offset of up to 8 bytes each, and a length of 2 bytes, since no packet holds 16384 bytes.</summary>
    internal const int StreamFrameOverhead = 19;

    private readonly QuicTransportParameters local;

    private readonly SortedDictionary<ulong, QuicStream> streams = [];

    private readonly List<QuicFrame> controlFrames = [];

    private readonly Queue<QuicStream> acceptedBidirectional = new();

    private readonly Queue<QuicStream> acceptedUnidirectional = new();

    private readonly QuicReceiveCredit connectionReceive;

    private readonly QuicReceiveCredit serverBidirectionalStreams;

    private readonly QuicReceiveCredit serverUnidirectionalStreams;

    private readonly QuicSendCredit connectionSend = new(0);

    private readonly QuicSendCredit clientBidirectionalStreams = new(0);

    private readonly QuicSendCredit clientUnidirectionalStreams = new(0);

    private QuicTransportParameters peer = new();

    /// <summary>Initializes a new instance of the <see cref="QuicStreamSet" /> class with the client's limits; the server's are zero until <see cref="SetPeerTransportParameters" />.</summary>
    /// <param name="localParameters">The transport parameters the client declared.</param>
    public QuicStreamSet(QuicTransportParameters localParameters)
    {
        ArgumentNullException.ThrowIfNull(localParameters);
        local = localParameters;
        connectionReceive = new QuicReceiveCredit(local.InitialMaxData, QuicTransportErrorCode.FlowControlError, "the connection");
        serverBidirectionalStreams = new QuicReceiveCredit(local.InitialMaxStreamsBidi, QuicTransportErrorCode.StreamLimitError, "the server's bidirectional streams");
        serverUnidirectionalStreams = new QuicReceiveCredit(local.InitialMaxStreamsUni, QuicTransportErrorCode.StreamLimitError, "the server's unidirectional streams");
    }

    /// <summary>Gets how many bytes the connection's flow control still lets the client send.</summary>
    public ulong ConnectionSendAvailable => connectionSend.Available;

    /// <summary>Gets the servers latest MAX_STREAMS for the clients bidirectional streams.</summary>
    public ulong ClientBidirectionalStreamLimit => clientBidirectionalStreams.Limit;

    /// <summary>Gets the MAX_DATA the client has advertised.</summary>
    public ulong ConnectionReceiveLimit => connectionReceive.Limit;

    /// <summary>Gets a value indicating whether a control frame, a raised limit or stream data is ready to go.</summary>
    internal bool HasFramesToSend =>
        controlFrames.Count > 0
        || connectionReceive.IsRaiseDue
        || serverBidirectionalStreams.IsRaiseDue
        || serverUnidirectionalStreams.IsRaiseDue
        || streams.Values.Any(stream => stream.IsLimitRaiseDue || stream.CanSendNow(connectionSend.Available));

    /// <summary>Takes the server's transport parameters: its initial MAX_DATA, MAX_STREAMS and the stream limits of the streams opened from now on.</summary>
    /// <param name="parameters">The server's transport parameters.</param>
    public void SetPeerTransportParameters(QuicTransportParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        peer = parameters;
        connectionSend.Raise(parameters.InitialMaxData);
        clientBidirectionalStreams.Raise(parameters.InitialMaxStreamsBidi);
        clientUnidirectionalStreams.Raise(parameters.InitialMaxStreamsUni);
    }

    /// <summary>Opens the next client-initiated bidirectional stream.</summary>
    /// <returns>The stream, or <see langword="null" /> when the server's MAX_STREAMS allows no more; STREAMS_BLOCKED then goes out, once per limit.</returns>
    public QuicStream? OpenBidirectional() => Open(clientBidirectionalStreams, isUnidirectional: false);

    /// <summary>Opens the next client-initiated unidirectional stream.</summary>
    /// <returns>The stream, or <see langword="null" /> when the server's MAX_STREAMS allows no more; STREAMS_BLOCKED then goes out, once per limit.</returns>
    public QuicStream? OpenUnidirectional() => Open(clientUnidirectionalStreams, isUnidirectional: true);

    /// <summary>Takes the next bidirectional stream the server opened, in stream ID order.</summary>
    /// <returns>The stream, or <see langword="null" /> when none waits.</returns>
    public QuicStream? AcceptBidirectional() => acceptedBidirectional.TryDequeue(out var stream) ? stream : null;

    /// <summary>Takes the next unidirectional stream the server opened, in stream ID order.</summary>
    /// <returns>The stream, or <see langword="null" /> when none waits.</returns>
    public QuicStream? AcceptUnidirectional() => acceptedUnidirectional.TryDequeue(out var stream) ? stream : null;

    /// <summary>Takes one frame from the server; frames that are not about streams or flow control, and DATA_BLOCKED and STREAMS_BLOCKED, which only inform, change nothing.</summary>
    /// <param name="frame">The frame.</param>
    /// <exception cref="QuicTransportException">The frame breaks a stream, flow control or stream limit rule.</exception>
    public void Receive(QuicFrame frame)
    {
        switch (frame)
        {
            case QuicStreamFrame data:
                ReceiveData(data);
                break;
            case QuicResetStreamFrame reset:
                ReceiveReset(reset);
                break;
            case QuicStopSendingFrame stopSending:
                ReceiveStopSending(stopSending);
                break;
            case QuicStreamDataBlockedFrame streamDataBlocked:
                ResolveForReceiving(streamDataBlocked.StreamId);
                break;
            default:
                ReceiveLimit(frame);
                break;
        }
    }

    /// <summary>Returns whether a frame of a lost packet is worth sending again: STREAM data of a stream the client has reset is not (RFC 9000 section 13.3).</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>Whether to send it again.</returns>
    internal bool ShouldResend(QuicFrame frame) => frame is not QuicStreamFrame data || !streams[data.StreamId].IsSendReset;

    // MAX_STREAM_DATA, MAX_DATA and MAX_STREAMS raise the client's limits; a lower value than the current one changes nothing.
    private void ReceiveLimit(QuicFrame frame)
    {
        switch (frame)
        {
            case QuicMaxStreamDataFrame maxStreamData:
                ResolveForSending(maxStreamData.StreamId).RaiseSendLimit(maxStreamData.MaximumStreamData);
                break;
            case QuicMaxDataFrame maxData:
                connectionSend.Raise(maxData.MaximumData);
                break;
            case QuicMaxStreamsFrame maxStreams:
                (maxStreams.IsUnidirectional ? clientUnidirectionalStreams : clientBidirectionalStreams).Raise(maxStreams.MaximumStreams);
                break;
        }
    }

    /// <summary>Adds the frames for the next packet within <paramref name="room" /> bytes: raised limits and other control frames first, then stream data in stream ID order, then the BLOCKED frames now due.</summary>
    /// <param name="frames">The packet's frames so far.</param>
    /// <param name="room">The bytes the frames added may take.</param>
    internal void TakeFrames(List<QuicFrame> frames, int room)
    {
        QueueRaises();
        room = TakeControlFrames(frames, room);
        foreach (var stream in streams.Values)
        {
            if (room <= StreamFrameOverhead)
            {
                break;
            }

            if (stream.TakeFrame(room - StreamFrameOverhead, connectionSend.Available) is { } frame)
            {
                frames.Add(frame);
                room -= QuicFrameCodec.Encode([frame]).Length;
                connectionSend.Use((ulong)frame.Data.Length);
                OnStateChanged(stream);
            }
        }

        // The BLOCKED frames the data just sent made due go in the same packet when they fit.
        QueueBlockedReports();
        TakeControlFrames(frames, room);
    }

    /// <summary>Queues a frame a stream needs sent: RESET_STREAM or STOP_SENDING.</summary>
    internal void QueueControlFrame(QuicFrame frame) => controlFrames.Add(frame);

    /// <summary>Credits bytes a stream consumed (read or discarded) to its limit and the connection's.</summary>
    internal void OnConsumed(QuicStream stream, ulong amount)
    {
        stream.Consume(amount);
        connectionReceive.Consume(amount);
        OnStateChanged(stream);
    }

    /// <summary>Takes note that a stream has new bytes to send, which may already be past a limit.</summary>
    internal void OnWritten() => QueueBlockedReports();

    /// <summary>Counts a server stream towards MAX_STREAMS once both its parts are done.</summary>
    internal void OnStateChanged(QuicStream stream)
    {
        if (!stream.IsClientInitiated && !stream.IsReleased && stream.IsClosed)
        {
            stream.IsReleased = true;
            (stream.IsUnidirectional ? serverUnidirectionalStreams : serverBidirectionalStreams).Consume(1);
        }
    }

    private static QuicTransportException StreamStateError(string reason) => new(QuicTransportErrorCode.StreamStateError, reason);

    private QuicStream? Open(QuicSendCredit limit, bool isUnidirectional)
    {
        if (limit.Available == 0)
        {
            if (limit.TakeBlockedReport())
            {
                controlFrames.Add(new QuicStreamsBlockedFrame(isUnidirectional, limit.Limit));
            }

            return null;
        }

        var id = (limit.Used << 2) | (isUnidirectional ? 2UL : 0UL);
        limit.Use(1);
        var stream = isUnidirectional
            ? new QuicStream(this, id, new QuicSendCredit(peer.InitialMaxStreamDataUni), null)
            : new QuicStream(this, id, new QuicSendCredit(peer.InitialMaxStreamDataBidiRemote), ReceiveCredit(local.InitialMaxStreamDataBidiLocal, id));
        streams.Add(id, stream);
        return stream;
    }

    private static QuicReceiveCredit ReceiveCredit(ulong window, ulong id) => new(window, QuicTransportErrorCode.FlowControlError, $"stream {id}");

    private void ReceiveData(QuicStreamFrame frame)
    {
        var stream = ResolveForReceiving(frame.StreamId);
        connectionReceive.Receive(connectionReceive.Received + stream.ReceiveData(frame));
        OnStateChanged(stream);
    }

    private void ReceiveReset(QuicResetStreamFrame frame)
    {
        var stream = ResolveForReceiving(frame.StreamId);
        connectionReceive.Receive(connectionReceive.Received + stream.ReceiveReset(frame));
        OnStateChanged(stream);
    }

    private void ReceiveStopSending(QuicStopSendingFrame frame)
    {
        var stream = ResolveForSending(frame.StreamId);
        stream.ReceiveStopSending(frame);
        OnStateChanged(stream);
    }

    // The client's unidirectional streams carry nothing from the server (RFC 9000 sections 19.4, 19.8 and 19.13).
    private QuicStream ResolveForReceiving(ulong id) => (id & 3) == 2
        ? throw StreamStateError($"The server sent a frame for the receiving part of the client's unidirectional stream {id}, which has none.")
        : Resolve(id);

    // The server's unidirectional streams have no sending part here (RFC 9000 sections 19.5 and 19.10).
    private QuicStream ResolveForSending(ulong id) => (id & 3) == 3
        ? throw StreamStateError($"The server sent a frame for the sending part of its own unidirectional stream {id}, which the client has none of.")
        : Resolve(id);

    private QuicStream Resolve(ulong id)
    {
        if ((id & 1) == 0)
        {
            // A frame for a client stream not yet opened is a STREAM_STATE_ERROR (RFC 9000 section 19.8).
            return streams.TryGetValue(id, out var opened) ? opened : throw StreamStateError($"The server sent a frame for stream {id}, which the client has not opened.");
        }

        // A server stream opens with every lower-numbered stream of its type (RFC 9000 section 3.2).
        var isUnidirectional = (id & 2) != 0;
        var limit = isUnidirectional ? serverUnidirectionalStreams : serverBidirectionalStreams;
        var created = limit.Receive((id >> 2) + 1);
        for (var next = limit.Received - created; next < limit.Received; next++)
        {
            CreateServerStream((next << 2) | (id & 3), isUnidirectional);
        }

        return streams[id];
    }

    private void CreateServerStream(ulong id, bool isUnidirectional)
    {
        var stream = isUnidirectional
            ? new QuicStream(this, id, null, ReceiveCredit(local.InitialMaxStreamDataUni, id))
            : new QuicStream(this, id, new QuicSendCredit(peer.InitialMaxStreamDataBidiLocal), ReceiveCredit(local.InitialMaxStreamDataBidiRemote, id));
        streams.Add(id, stream);
        (isUnidirectional ? acceptedUnidirectional : acceptedBidirectional).Enqueue(stream);
    }

    private void QueueRaises()
    {
        if (connectionReceive.IsRaiseDue)
        {
            controlFrames.Add(new QuicMaxDataFrame(connectionReceive.TakeRaise()));
        }

        if (serverBidirectionalStreams.IsRaiseDue)
        {
            controlFrames.Add(new QuicMaxStreamsFrame(false, serverBidirectionalStreams.TakeRaise()));
        }

        if (serverUnidirectionalStreams.IsRaiseDue)
        {
            controlFrames.Add(new QuicMaxStreamsFrame(true, serverUnidirectionalStreams.TakeRaise()));
        }

        controlFrames.AddRange(streams.Values.Where(stream => stream.IsLimitRaiseDue).Select(stream => stream.TakeLimitRaise()).ToList());
    }

    // Control frames go in order while they fit; returns the room left.
    private int TakeControlFrames(List<QuicFrame> frames, int room)
    {
        while (controlFrames.Count > 0 && QuicFrameCodec.Encode([controlFrames[0]]).Length is var length && length <= room)
        {
            frames.Add(controlFrames[0]);
            controlFrames.RemoveAt(0);
            room -= length;
        }

        return room;
    }

    private void QueueBlockedReports()
    {
        if (streams.Values.Any(stream => stream.UnsentLength > 0) && connectionSend.TakeBlockedReport())
        {
            controlFrames.Add(new QuicDataBlockedFrame(connectionSend.Limit));
        }

        foreach (var stream in streams.Values)
        {
            if (stream.TakeBlockedReport())
            {
                controlFrames.Add(new QuicStreamDataBlockedFrame(stream.Id, stream.SendLimit));
            }
        }
    }
}
