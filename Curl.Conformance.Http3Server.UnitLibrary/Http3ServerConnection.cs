using System.Buffers.Binary;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// The HTTP/3 stand-in's I/O-free QUIC version 1 server connection: datagrams go in and
/// datagrams come out. It accepts the client's first Initial (its Destination Connection ID at
/// least 8 bytes), derives the Initial keys from it, chooses its own connection ID, and runs
/// <see cref="Http3ServerTlsHandshake" /> over CRYPTO frames at the Initial and Handshake levels,
/// installing the keys TLS derives. Its transport parameters are <see cref="DefaultTransportParameters" />
/// with <c>original_destination_connection_id</c> and <c>initial_source_connection_id</c>;
/// they replace the settings' own. It acknowledges every ack-eliciting packet in its space,
/// drops a packet that fails protection or repeats one, sends HANDSHAKE_DONE once the client's
/// Finished checks out, carries stream data both ways, and closes in either direction. An
/// Initial of another version in a datagram of 1200 bytes or more gets Version Negotiation.
/// Until a Handshake packet from the client validates its address it sends at most three times
/// the bytes received (RFC 9000 section 8.1) and holds the rest. No STREAM frame passes the
/// client's MAX_DATA or MAX_STREAM_DATA limit: the rest waits until the client raises it
/// (RFC 9000 section 4.1). Nothing is resent and no congestion control runs: in memory nothing
/// is lost.
/// </summary>
/// <param name="tlsSettings">The certificate, key and preferences of the TLS handshake.</param>
/// <param name="random">The source of the connection ID, the server random and its key share.</param>
public sealed class Http3ServerConnection(Http3ServerTlsSettings tlsSettings, ITlsRandomSource random) : IDisposable
{
    /// <summary>The length of the connection ID the server chooses.</summary>
    public const int ConnectionIdLength = 8;

    /// <summary>How many times the bytes received the server sends before the client's address is validated.</summary>
    public const int AmplificationFactor = 3;

    private const int MinimumInitialDatagramLength = 1200;

    private readonly Http3ServerTlsSettings tlsSettings = tlsSettings ?? throw new ArgumentNullException(nameof(tlsSettings));

    private readonly ITlsRandomSource random = random ?? throw new ArgumentNullException(nameof(random));

    private readonly Http3ServerPacketSpace initial = new(QuicPacketType.Initial, TlsEncryptionLevel.Initial);

    private readonly Http3ServerPacketSpace handshake = new(QuicPacketType.Handshake, TlsEncryptionLevel.Handshake);

    private readonly Http3ServerPacketSpace application = new(QuicPacketType.OneRtt, TlsEncryptionLevel.Application);

    private readonly Dictionary<ulong, Http3ServerStream> streams = [];

    private readonly Queue<Http3ServerStream> accepted = [];

    private readonly Dictionary<ulong, Http3ServerSendStream> sendStreams = [];

    private readonly Queue<byte[]> outbox = [];

    private Http3ServerTlsHandshake? tls;

    private byte[] originalDestinationConnectionId = [];

    private byte[] clientConnectionId = [];

    private ulong nextUnidirectionalStreamId = 3;

    private long bytesReceived;

    private long bytesSent;

    private bool addressValidated;

    private QuicTransportParameters clientParameters = new();

    private ulong connectionSendLimit;

    private ulong connectionBytesSent;

    /// <summary>
    /// Gets the transport parameters the server declares, before the connection IDs: limits so
    /// large that the client never waits on MAX_DATA, MAX_STREAM_DATA or MAX_STREAMS.
    /// </summary>
    public static QuicTransportParameters DefaultTransportParameters { get; } = new()
    {
        InitialMaxData = 1UL << 40,
        InitialMaxStreamDataBidiLocal = 1UL << 40,
        InitialMaxStreamDataBidiRemote = 1UL << 40,
        InitialMaxStreamDataUni = 1UL << 40,
        InitialMaxStreamsBidi = 1UL << 20,
        InitialMaxStreamsUni = 1UL << 20,
    };

    /// <summary>Gets the connection ID the server chose.</summary>
    public byte[] ConnectionId { get; } = CreateConnectionId(random);

    /// <summary>Gets a value indicating whether the client's Finished has checked out.</summary>
    public bool IsHandshakeComplete { get; private set; }

    /// <summary>Gets a value indicating whether the connection is closed: it reads and sends nothing more.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Gets the client's CONNECTION_CLOSE frame, once one has arrived.</summary>
    public QuicConnectionCloseFrame? PeerClose { get; private set; }

    /// <summary>Gets the TLS alert that ended the handshake, or <see langword="null" />.</summary>
    public TlsHandshakeFailure? TlsFailure => tls?.Failure;

    /// <summary>
    /// Takes one datagram from the client and returns the datagrams to send in answer. A
    /// packet that cannot be read drops the rest of its datagram; one that fails protection,
    /// repeats, or names another connection is dropped. A frame that breaks the protocol
    /// closes the connection with its transport error.
    /// </summary>
    /// <param name="datagram">The datagram.</param>
    /// <returns>The datagrams to send; none once the connection is closed.</returns>
    public IReadOnlyList<byte[]> Receive(ReadOnlyMemory<byte> datagram)
    {
        if (IsClosed)
        {
            return [];
        }

        bytesReceived += datagram.Length;
        try
        {
            ReceivePackets(datagram);
        }
        catch (QuicTransportException error)
        {
            CloseWithTransportError(error.ErrorCode);
        }

        return PeerClose is null ? Flush() : [];
    }

    /// <summary>Returns the next stream the client opened that has not been returned before.</summary>
    /// <returns>The stream, or <see langword="null" /> when none waits.</returns>
    public Http3ServerStream? AcceptStream() => accepted.TryDequeue(out var stream) ? stream : null;

    /// <summary>Returns the ID of a new server-initiated unidirectional stream: 3, 7, 11 and on (RFC 9000 section 2.1).</summary>
    /// <returns>The stream ID, for <see cref="Write" />.</returns>
    /// <exception cref="InvalidOperationException">The handshake is not complete, or the connection is closed.</exception>
    public ulong OpenUnidirectionalStream()
    {
        RequireOpen();
        var id = nextUnidirectionalStreamId;
        nextUnidirectionalStreamId += 4;
        return id;
    }

    /// <summary>
    /// Queues bytes on a stream, after those written before, in STREAM frames of at most 1000
    /// bytes; <see cref="TakeDatagramsToSend" /> sends them. Bytes past the client's MAX_DATA or
    /// MAX_STREAM_DATA limit wait, with the FIN after them, until the client raises it.
    /// </summary>
    /// <param name="streamId">A client bidirectional stream, or one from <see cref="OpenUnidirectionalStream" />.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="endStream">Whether the last frame carries the FIN.</param>
    /// <exception cref="InvalidOperationException">The handshake is not complete, or the connection is closed.</exception>
    public void Write(ulong streamId, ReadOnlySpan<byte> bytes, bool endStream)
    {
        RequireOpen();
        SendStream(streamId).Append(bytes, endStream);
        QueueStreamFrames();
    }

    /// <summary>Returns the datagrams carrying what waits to be sent, such as the stream data <see cref="Write" /> queued.</summary>
    /// <returns>The datagrams.</returns>
    public IReadOnlyList<byte[]> TakeDatagramsToSend() => Flush();

    /// <summary>Closes the connection with an application CONNECTION_CLOSE (frame type 0x1d) carrying <paramref name="applicationErrorCode" />, such as HTTP/3's <c>H3_NO_ERROR</c>.</summary>
    /// <param name="applicationErrorCode">The application's error code.</param>
    /// <returns>The datagrams to send, the close among them.</returns>
    /// <exception cref="InvalidOperationException">The handshake is not complete, or the connection is closed.</exception>
    public IReadOnlyList<byte[]> Close(ulong applicationErrorCode)
    {
        RequireOpen();
        application.QueueFrame(new QuicConnectionCloseFrame(applicationErrorCode, null, ReadOnlyMemory<byte>.Empty));
        IsClosed = true;
        return Flush();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        tls?.Dispose();
        initial.Dispose();
        handshake.Dispose();
        application.Dispose();
    }

    private static byte[] CreateConnectionId(ITlsRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var id = new byte[ConnectionIdLength];
        random.Fill(id);
        return id;
    }

    // A long header whose version is neither 0 (Version Negotiation) nor 1 (RFC 9000 section 6.1).
    private static bool HasOtherVersion(ReadOnlySpan<byte> packet) =>
        packet.Length >= MinimumInitialDatagramLength
        && (packet[0] & 0x80) != 0
        && BinaryPrimitives.ReadUInt32BigEndian(packet[1..]) is not (0 or QuicPacketCodec.Version1);

    private void RequireOpen()
    {
        if (!IsHandshakeComplete || IsClosed)
        {
            throw new InvalidOperationException("The QUIC connection is not open: its handshake is not complete, or it is closed.");
        }
    }

    private void ReceivePackets(ReadOnlyMemory<byte> datagram)
    {
        var remaining = datagram;
        while (!remaining.IsEmpty && !IsClosed && TryDecode(remaining) is { } decoded)
        {
            ReceivePacket(decoded.Packet, remaining[..decoded.Length]);
            remaining = remaining[decoded.Length..];
        }
    }

    private QuicDecodedPacket? TryDecode(ReadOnlyMemory<byte> bytes)
    {
        if (HasOtherVersion(bytes.Span))
        {
            SendVersionNegotiation(bytes.Span);
            return null;
        }

        try
        {
            return QuicPacketCodec.Decode(bytes, ConnectionIdLength);
        }
        catch (QuicTransportException)
        {
            // A packet whose header cannot be read is dropped with the rest of its datagram (RFC 9000 section 12.2).
            return null;
        }
    }

    // The client's connection IDs swap places, and the list holds version 1 alone (RFC 9000 section 17.2.1).
    // A datagram of 1200 bytes holds both connection IDs whatever their length bytes say.
    private void SendVersionNegotiation(ReadOnlySpan<byte> packet)
    {
        var destination = packet.Slice(6, packet[5]);
        var source = packet.Slice(7 + destination.Length, packet[6 + destination.Length]);
        outbox.Enqueue(QuicPacketCodec.Encode(new QuicVersionNegotiationPacket(source.ToArray(), destination.ToArray(), [QuicPacketCodec.Version1])));
    }

    private void ReceivePacket(QuicPacket packet, ReadOnlyMemory<byte> bytes)
    {
        switch (packet)
        {
            case QuicLongHeaderPacket { Type: QuicPacketType.Initial or QuicPacketType.Handshake } longHeader:
                ReceiveLongHeader(longHeader, bytes);
                break;
            case QuicShortHeaderPacket shortHeader:
                ReceiveShortHeader(shortHeader, bytes);
                break;
            default:
                // A 0-RTT packet (no early data is accepted), a Retry or a Version Negotiation packet is never a client's to send here.
                break;
        }
    }

    private void ReceiveLongHeader(QuicLongHeaderPacket header, ReadOnlyMemory<byte> bytes)
    {
        if ((tls is null && !TryAccept(header)) || !IsForThisConnection(header.DestinationConnectionId.Span))
        {
            return;
        }

        var space = header.Type == QuicPacketType.Initial ? initial : handshake;
        if (Open(space, bytes) is QuicLongHeaderPacket packet)
        {
            // A Handshake packet from the client proves its address (RFC 9000 section 8.1).
            addressValidated |= space == handshake;
            ReceiveFrames(space, packet.Payload);
        }
    }

    // The client's first Initial packet chooses the Initial keys (RFC 9001 section 5.2) and its
    // Destination Connection ID is at least 8 bytes (RFC 9000 section 7.2).
    private bool TryAccept(QuicLongHeaderPacket header)
    {
        if (header.Type != QuicPacketType.Initial || header.DestinationConnectionId.Length < 8)
        {
            return false;
        }

        originalDestinationConnectionId = header.DestinationConnectionId.ToArray();
        clientConnectionId = header.SourceConnectionId.ToArray();
        initial.ReceiveProtection = QuicPacketProtection.CreateClientInitial(originalDestinationConnectionId);
        initial.SendProtection = QuicPacketProtection.CreateServerInitial(originalDestinationConnectionId);
        var parameters = DefaultTransportParameters with
        {
            OriginalDestinationConnectionId = originalDestinationConnectionId,
            InitialSourceConnectionId = ConnectionId,
        };
        tls = new Http3ServerTlsHandshake(tlsSettings with { QuicTransportParameters = parameters.Encode() }, random);
        return true;
    }

    private bool IsForThisConnection(ReadOnlySpan<byte> destinationConnectionId) =>
        destinationConnectionId.SequenceEqual(ConnectionId) || destinationConnectionId.SequenceEqual(originalDestinationConnectionId);

    private void ReceiveShortHeader(QuicShortHeaderPacket header, ReadOnlyMemory<byte> bytes)
    {
        if (header.DestinationConnectionId.Span.SequenceEqual(ConnectionId) && Open(application, bytes) is QuicShortHeaderPacket packet)
        {
            ReceiveFrames(application, packet.Payload);
        }
    }

    private static QuicPacket? Open(Http3ServerPacketSpace space, ReadOnlyMemory<byte> bytes)
    {
        var result = space.ReceiveProtection?.Unprotect(bytes, ConnectionIdLength, space.LargestReceived);
        return result is { Status: QuicUnprotectStatus.Unprotected } && space.RecordReceived(result.PacketNumber) ? result.Packet : null;
    }

    private void ReceiveFrames(Http3ServerPacketSpace space, ReadOnlyMemory<byte> payload)
    {
        var frames = QuicFrameCodec.Decode(payload, space.PacketType);
        if (frames.Any(frame => frame.IsAckEliciting))
        {
            space.RequireAcknowledgement();
        }

        foreach (var frame in frames.TakeWhile(_ => !IsClosed))
        {
            ReceiveFrame(space, frame);
        }
    }

    private void ReceiveFrame(Http3ServerPacketSpace space, QuicFrame frame)
    {
        switch (frame)
        {
            case QuicCryptoFrame crypto:
                ReceiveCrypto(space, crypto);
                break;
            case QuicStreamFrame stream:
                ReceiveStream(stream);
                break;
            case QuicMaxDataFrame maxData:
                connectionSendLimit = Math.Max(connectionSendLimit, maxData.MaximumData);
                QueueStreamFrames();
                break;
            case QuicMaxStreamDataFrame maxStreamData:
                SendStream(maxStreamData.StreamId).Raise(maxStreamData.MaximumStreamData);
                QueueStreamFrames();
                break;
            case QuicConnectionCloseFrame close:
                // The client closed: the server drains and sends nothing more (RFC 9000 section 10.2.2).
                PeerClose = close;
                IsClosed = true;
                break;
            default:
                // ACK (nothing is resent), PING, PADDING, the client's BLOCKED and MAX_STREAMS frames and connection ID frames ask nothing of the stand-in.
                break;
        }
    }

    private void ReceiveCrypto(Http3ServerPacketSpace space, QuicCryptoFrame frame)
    {
        var bytes = space.CryptoReceived.Receive(frame);
        if (bytes.Length > 0)
        {
            Apply(tls!.Receive(space.Level, bytes));
        }
    }

    private void Apply(Tls13HandshakeOutput output)
    {
        foreach (var secret in output.SecretsInstalled)
        {
            Install(secret);
        }

        foreach (var bytes in output.BytesToSend)
        {
            SpaceAt(bytes.Level).QueueCrypto(bytes.Bytes);
        }

        if (output.Failure is { } failure)
        {
            CloseWithTransportError(QuicTransportErrorCode.CryptoErrorBase + (byte)failure.Alert);
        }
        else if (output.IsComplete)
        {
            IsHandshakeComplete = true;
            application.QueueFrame(new QuicHandshakeDoneFrame());

            // The handshake refuses a ClientHello without quic_transport_parameters, so they are there.
            clientParameters = QuicTransportParameters.Decode(tls!.ClientQuicTransportParameters!);
            connectionSendLimit = clientParameters.InitialMaxData;
        }
    }

    // A server unidirectional stream has the client's initial_max_stream_data_uni, a client
    // bidirectional one its initial_max_stream_data_bidi_local (RFC 9000 section 18.2).
    private Http3ServerSendStream SendStream(ulong streamId)
    {
        if (!sendStreams.TryGetValue(streamId, out var stream))
        {
            var limit = (streamId & 2) != 0 ? clientParameters.InitialMaxStreamDataUni : clientParameters.InitialMaxStreamDataBidiLocal;
            stream = new Http3ServerSendStream(streamId, limit);
            sendStreams.Add(streamId, stream);
        }

        return stream;
    }

    private void QueueStreamFrames()
    {
        foreach (var stream in sendStreams.Values)
        {
            while (stream.TakeFrame(connectionSendLimit - connectionBytesSent) is { } frame)
            {
                connectionBytesSent += (ulong)frame.Data.Length;
                application.QueueFrame(frame);
            }
        }
    }

    private void Install(Tls13TrafficSecret secret)
    {
        var space = SpaceAt(secret.Level);
        var protection = QuicPacketProtection.Create(tls!.CipherSuite!, secret.Secret);
        if (secret.Direction == TlsTrafficDirection.Read)
        {
            space.ReceiveProtection = protection;
        }
        else
        {
            space.SendProtection = protection;
        }
    }

    private Http3ServerPacketSpace SpaceAt(TlsEncryptionLevel level) => level switch
    {
        TlsEncryptionLevel.Initial => initial,
        TlsEncryptionLevel.Handshake => handshake,
        _ => application,
    };

    private void ReceiveStream(QuicStreamFrame frame)
    {
        if (!streams.TryGetValue(frame.StreamId, out var stream))
        {
            stream = new Http3ServerStream(frame.StreamId);
            streams.Add(frame.StreamId, stream);
            accepted.Enqueue(stream);
        }

        stream.Receive(frame);
    }

    // CONNECTION_CLOSE of type 0x1c at every level the server has keys for, as it cannot know which the client still reads (RFC 9000 section 10.2.3).
    private void CloseWithTransportError(QuicTransportErrorCode errorCode)
    {
        foreach (var space in new[] { initial, handshake, application }.Where(space => space.SendProtection is not null))
        {
            space.QueueFrame(new QuicConnectionCloseFrame((ulong)errorCode, 0, ReadOnlyMemory<byte>.Empty));
        }

        IsClosed = true;
    }

    private List<byte[]> Flush()
    {
        foreach (var space in new[] { initial, handshake, application }.Where(space => space.SendProtection is not null))
        {
            foreach (var payload in space.TakePayloads())
            {
                outbox.Enqueue(Protect(space, payload));
            }
        }

        List<byte[]> datagrams = [];
        while (outbox.TryPeek(out var datagram) && MaySend(datagram.Length))
        {
            datagrams.Add(outbox.Dequeue());
            bytesSent += datagram.Length;
        }

        return datagrams;
    }

    private bool MaySend(int length) => addressValidated || bytesSent + length <= AmplificationFactor * bytesReceived;

    private byte[] Protect(Http3ServerPacketSpace space, byte[] payload)
    {
        var packetNumber = space.TakePacketNumber();
        QuicPacket packet = space.PacketType == QuicPacketType.OneRtt
            ? new QuicShortHeaderPacket(clientConnectionId, 4, (uint)packetNumber, payload)
            : new QuicLongHeaderPacket(space.PacketType, QuicPacketCodec.Version1, clientConnectionId, ConnectionId, ReadOnlyMemory<byte>.Empty, 4, (uint)packetNumber, payload);
        return space.SendProtection!.Protect(packet, packetNumber);
    }
}
