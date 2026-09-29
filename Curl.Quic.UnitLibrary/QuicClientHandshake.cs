using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// The QUIC version 1 client handshake (RFC 9000 sections 7, 8 and 17, RFC 9001 section 4)
/// with no I/O: datagrams go in and datagrams come out. It sends the ClientHello of the
/// hand-built <see cref="Tls13ClientHandshake" /> in the first Initial, padded to 1200
/// bytes, carries TLS messages in CRYPTO frames at the Initial, Handshake and 1-RTT levels,
/// installs the keys TLS derives, and acknowledges what the server sends. It follows one
/// Retry (new destination connection ID, its token, the ClientHello again), fails on a
/// Version Negotiation packet without version 1, checks the server's transport parameters
/// against the connection IDs used, discards the Initial keys when it first sends a
/// Handshake packet and the Handshake keys at HANDSHAKE_DONE, issues connection IDs up to
/// the server's limit and retires the server's as asked, and keeps NEW_TOKEN tokens for
/// the next connection. Failures map to curl's exit codes (ADR-0144 section 7, ADR-0165).
/// Once complete it carries the connection's <see cref="Streams" />: their frames go in and
/// out of its 1-RTT packets, and a stream or flow control violation closes the connection
/// with its transport error.
/// </summary>
public sealed class QuicClientHandshake : IDisposable
{
    private readonly QuicClientSettings settings;

    private readonly TimeProvider timeProvider;

    private readonly long startTimestamp;

    private readonly Tls13ClientHandshake tls;

    private readonly QuicPacketNumberSpace initial = new(QuicPacketType.Initial);

    private readonly QuicPacketNumberSpace handshake = new(QuicPacketType.Handshake);

    private readonly QuicPacketNumberSpace application = new(QuicPacketType.OneRtt);

    private readonly QuicPacketNumberSpace?[] spacesByLevel;

    private readonly QuicPacketNumberSpace[] spacesById;

    private readonly byte[] originalDestinationConnectionId;

    private readonly byte[] sourceConnectionId;

    private readonly List<byte[]> receivedTokens = [];

    private byte[] destinationConnectionId;

    private byte[]? serverSourceConnectionId;

    private byte[]? retrySourceConnectionId;

    private ReadOnlyMemory<byte> token;

    private bool serverPacketReceived;

    private bool started;

    private QuicTransportErrorCode? closeErrorCode;

    /// <summary>Initializes a new instance of the <see cref="QuicClientHandshake" /> class: chooses the connection IDs and derives the Initial keys.</summary>
    /// <param name="settings">What the handshake offers.</param>
    /// <param name="random">Where connection IDs, reset tokens, the TLS client random and key shares come from.</param>
    /// <param name="verifier">Verifies the server's certificate chain.</param>
    /// <param name="timeProvider">The clock loss detection, the probe timeout and the ACK Delay run on.</param>
    /// <exception cref="ArgumentOutOfRangeException">The connection ID length is not 8 to 20 bytes.</exception>
    public QuicClientHandshake(QuicClientSettings settings, ITlsRandomSource random, IServerCertificateVerifier verifier, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.timeProvider = timeProvider;
        startTimestamp = timeProvider.GetTimestamp();
        Recovery = new QuicLossRecovery(QuicCongestionController.Create(settings.CongestionControl, QuicDatagramAssembler.DatagramSize));
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.ConnectionIdLength, 8);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.ConnectionIdLength, QuicFrameCodec.MaximumConnectionIdLength);
        this.settings = settings;
        originalDestinationConnectionId = RandomBytes(random, settings.ConnectionIdLength);
        destinationConnectionId = originalDestinationConnectionId;
        sourceConnectionId = RandomBytes(random, settings.ConnectionIdLength);
        LocalConnectionIds = new QuicLocalConnectionIds(sourceConnectionId, random);
        TransportParameters = settings.TransportParameters with { InitialSourceConnectionId = sourceConnectionId };
        TlsExtension parameters = QuicTransportParametersExtension.Encode(TransportParameters.Encode());
        tls = new Tls13ClientHandshake(settings.Tls with { FixedExtensions = [.. settings.Tls.FixedExtensions, parameters] }, random, verifier);
        token = settings.Token;
        Streams = new QuicStreamSet(TransportParameters);
        application.Streams = Streams;
        spacesByLevel = [initial, null, handshake, application];
        spacesById = [initial, handshake, application];
        foreach (var space in spacesById)
        {
            space.AckDelayExponent = (int)TransportParameters.AckDelayExponent;
        }

        InstallInitialKeys();
    }

    /// <summary>Gets the transport parameters the client declares, its <c>initial_source_connection_id</c> included.</summary>
    public QuicTransportParameters TransportParameters { get; }

    /// <summary>Gets the server's transport parameters once its EncryptedExtensions has arrived and they check out.</summary>
    public QuicTransportParameters? ServerTransportParameters { get; private set; }

    /// <summary>Gets the TLS handshake, for the cipher suite, the server's certificates and the ALPN protocol it chose.</summary>
    public Tls13ClientHandshake Tls => tls;

    /// <summary>Gets the connection IDs the client issued.</summary>
    public QuicLocalConnectionIds LocalConnectionIds { get; }

    /// <summary>Gets the connection IDs the server issued, once its first Initial packet has arrived.</summary>
    public QuicPeerConnectionIds? PeerConnectionIds { get; private set; }

    /// <summary>Gets the address-validation tokens NEW_TOKEN frames carried, for the next connection to this server.</summary>
    public IReadOnlyList<byte[]> ReceivedTokens => receivedTokens;

    /// <summary>Gets a value indicating whether the handshake is complete (RFC 9001 section 4.1.1): TLS finished and the client's Finished is sent.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>Gets a value indicating whether the handshake is confirmed: HANDSHAKE_DONE has arrived (RFC 9001 section 4.1.2).</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>Gets a value indicating whether the Initial keys are discarded.</summary>
    public bool InitialKeysDiscarded => initial.IsDiscarded;

    /// <summary>Gets a value indicating whether the Handshake keys are discarded.</summary>
    public bool HandshakeKeysDiscarded => handshake.IsDiscarded;

    /// <summary>Gets the connection's streams and flow control (RFC 9000 sections 2 to 4); streams open once the server's transport parameters have arrived.</summary>
    public QuicStreamSet Streams { get; }

    /// <summary>Gets the loss detection and congestion control of the connection (RFC 9002).</summary>
    public QuicLossRecovery Recovery { get; }

    /// <summary>Gets how long until <see cref="OnLossDetectionTimeout" /> is due: zero when it is overdue, <see cref="Timeout.InfiniteTimeSpan" /> while the timer is not armed or once the handshake has failed.</summary>
    public TimeSpan TimeUntilLossDetectionTimeout => Failure is null && Recovery.LossDetectionTimer is { } timer
        ? TimeSpan.FromTicks(Math.Max(0, (timer - Now()).Ticks))
        : Timeout.InfiniteTimeSpan;

    /// <summary>Returns how long the pacer holds a datagram of <paramref name="length" /> bytes before it may go; zero when it may go now (RFC 9002 section 7.7).</summary>
    /// <param name="length">The datagram's length.</param>
    /// <returns>The wait.</returns>
    public TimeSpan TimeUntilSend(int length) =>
        Recovery.Pacer.TimeUntilSend(Now(), length, Recovery.Congestion.CongestionWindow, Recovery.Rtt.SmoothedRtt);

    /// <summary>Takes a datagram that has just gone out of the pacer's budget.</summary>
    /// <param name="length">The datagram's length.</param>
    public void OnDatagramSent(int length) =>
        Recovery.Pacer.OnPacketSent(Now(), length, Recovery.Congestion.CongestionWindow, Recovery.Rtt.SmoothedRtt);

    /// <summary>Gets why the handshake failed, or <see langword="null" /> while it has not.</summary>
    public QuicHandshakeFailure? Failure { get; private set; }

    /// <summary>Starts the handshake.</summary>
    /// <returns>The first datagram: one Initial packet carrying the ClientHello, padded to 1200 bytes.</returns>
    /// <exception cref="InvalidOperationException">The handshake has already started.</exception>
    public IReadOnlyList<byte[]> Start()
    {
        if (started)
        {
            throw new InvalidOperationException("The QUIC handshake has already started.");
        }

        started = true;
        Apply(tls.Start());
        return Flush();
    }

    /// <summary>
    /// Takes one datagram from the server and returns the datagrams to send in answer. A
    /// packet that cannot be read drops the rest of its datagram; one that fails
    /// authentication, repeats, or carries another connection ID is dropped. Once the
    /// handshake has failed, nothing more is read or sent.
    /// </summary>
    /// <param name="datagram">The datagram.</param>
    /// <returns>The datagrams to send: acknowledgements, CRYPTO data, connection IDs, or the CONNECTION_CLOSE of a failure the client detected.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Start" /> has not been called.</exception>
    public IReadOnlyList<byte[]> Receive(ReadOnlyMemory<byte> datagram)
    {
        if (!started)
        {
            throw new InvalidOperationException("Start the QUIC handshake before passing it datagrams.");
        }

        if (Failure is not null)
        {
            return [];
        }

        try
        {
            ReceivePackets(datagram);
        }
        catch (QuicTransportException error)
        {
            Fail(new QuicHandshakeFailure(CurlExitCode.CouldntConnect, error.Message), error.ErrorCode);
        }

        return Failure is null ? Flush() : CloseIfDetected();
    }

    /// <summary>
    /// Runs the loss detection timer's expiry (RFC 9002 section 6): packets lost by the time
    /// threshold have their CRYPTO and other data queued again, or, at the probe timeout, the
    /// oldest unacknowledged ack-eliciting packet's data goes again in a probe (a PING when
    /// it carried nothing to resend), outside the congestion window.
    /// </summary>
    /// <returns>The datagrams to send.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Start" /> has not been called.</exception>
    public IReadOnlyList<byte[]> OnLossDetectionTimeout()
    {
        if (!started)
        {
            throw new InvalidOperationException("Start the QUIC handshake before running its loss detection timer.");
        }

        if (Failure is not null)
        {
            return [];
        }

        var outcome = Recovery.OnLossDetectionTimeout(Now());
        var space = SpaceOf(outcome.Space);
        RequeueLost(space, outcome.Lost);
        if (outcome.ProbeRequired)
        {
            QueueProbe(space);
        }

        return Flush(outcome.ProbeRequired);
    }

    /// <summary>Returns the datagrams carrying what waits to be sent, such as the stream data, STOP_SENDING or raised limits the application's use of <see cref="Streams" /> queued, as far as the congestion window allows; nothing once the connection has failed.</summary>
    /// <returns>The datagrams to send.</returns>
    public IReadOnlyList<byte[]> TakeDatagramsToSend() => Failure is null ? Flush() : [];

    /// <summary>Closes the connection with an application CONNECTION_CLOSE (frame type 0x1d) carrying <paramref name="applicationErrorCode" />, such as HTTP/3's <c>H3_NO_ERROR</c>.</summary>
    /// <param name="applicationErrorCode">The application's error code.</param>
    /// <returns>The datagrams to send, whatever the congestion window says.</returns>
    public IReadOnlyList<byte[]> CloseWithApplicationError(ulong applicationErrorCode)
    {
        application.QueueFrame(new QuicConnectionCloseFrame(applicationErrorCode, null, ReadOnlyMemory<byte>.Empty));
        return Flush(ignoreCongestionWindow: true);
    }

    /// <summary>Gives up on the handshake, as when its timeout fires: records the failure and closes the connection with <paramref name="errorCode" />.</summary>
    /// <param name="errorCode">The transport error CONNECTION_CLOSE carries.</param>
    /// <param name="failure">Why the handshake failed.</param>
    /// <returns>The datagrams carrying CONNECTION_CLOSE at every level the client has keys for.</returns>
    public IReadOnlyList<byte[]> Abandon(QuicTransportErrorCode errorCode, QuicHandshakeFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Fail(failure, errorCode);
        return CloseIfDetected();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        tls.Dispose();
        initial.Dispose();
        handshake.Dispose();
        application.Dispose();
    }

    private static byte[] RandomBytes(ITlsRandomSource random, int length)
    {
        var bytes = new byte[length];
        random.Fill(bytes);
        return bytes;
    }

    private static bool SameConnectionId(byte[]? actual, byte[]? expected) =>
        actual is null ? expected is null : expected is not null && actual.AsSpan().SequenceEqual(expected);

    private void RequeueLost(QuicPacketNumberSpace space, IEnumerable<QuicSentPacket> packets)
    {
        foreach (var packet in packets)
        {
            space.RequeueLost(packet.Frames.Where(Streams.ShouldResend));
        }
    }

    private TimeSpan Now() => timeProvider.GetElapsedTime(startTimestamp);

    private QuicPacketNumberSpace SpaceOf(QuicPacketNumberSpaceId id) => spacesById[(int)id];

    // A probe resends the oldest unacknowledged ack-eliciting packet's data; with none to resend it is a PING (RFC 9002 section 6.2.4).
    private void QueueProbe(QuicPacketNumberSpace space)
    {
        if (Recovery.GetUnacknowledgedPackets(space.Id).FirstOrDefault(packet => packet.IsAckEliciting) is { } oldest)
        {
            space.RequeueLost(oldest.Frames.Where(Streams.ShouldResend));
        }

        if (!space.HasFramesToSend)
        {
            space.QueueFrame(new QuicPingFrame());
        }
    }

    private void DiscardSpace(QuicPacketNumberSpace space)
    {
        space.Discard();
        Recovery.DiscardSpace(space.Id, Now());
    }

    private void InstallInitialKeys()
    {
        initial.Dispose();
        initial.SendProtection = QuicPacketProtection.CreateClientInitial(destinationConnectionId);
        initial.ReceiveProtection = QuicPacketProtection.CreateServerInitial(destinationConnectionId);
    }

    private void Fail(QuicHandshakeFailure failure, QuicTransportErrorCode? closeWith)
    {
        Failure = failure;
        closeErrorCode = closeWith;
    }

    private IReadOnlyList<byte[]> CloseIfDetected()
    {
        if (closeErrorCode is not { } errorCode)
        {
            return [];
        }

        foreach (var space in spacesById.Where(space => space.SendProtection is not null))
        {
            space.DropUnsentCrypto();
            space.QueueFrame(new QuicConnectionCloseFrame((ulong)errorCode, 0, ReadOnlyMemory<byte>.Empty));
        }

        return Flush(ignoreCongestionWindow: true);
    }

    // Probes and CONNECTION_CLOSE go whatever the congestion window says (RFC 9002 section 7.5).
    private List<byte[]> Flush(bool ignoreCongestionWindow = false)
    {
        var allowance = ignoreCongestionWindow ? long.MaxValue : Recovery.Congestion.AvailableWindow;
        var assembly = QuicDatagramAssembler.Assemble(spacesById, SendAddress(), Now(), allowance);
        foreach (var sent in assembly.Packets)
        {
            RecordSent(sent.Space, sent.Packet);
        }

        return assembly.Datagrams;
    }

    private QuicPacketAddress SendAddress() =>
        new(destinationConnectionId, PeerConnectionIds?.Current.ConnectionId ?? destinationConnectionId, sourceConnectionId, token);

    private void RecordSent(QuicPacketNumberSpace space, QuicSentPacket packet)
    {
        Recovery.OnPacketSent(space.Id, packet);
        if (space == handshake && !initial.IsDiscarded)
        {
            // A client discards its Initial keys when it first sends a Handshake packet (RFC 9001 section 4.9.1).
            DiscardSpace(initial);
        }
    }

    private void ReceivePackets(ReadOnlyMemory<byte> datagram)
    {
        var remaining = datagram;
        while (!remaining.IsEmpty && Failure is null && TryDecode(remaining) is { } decoded)
        {
            ReceivePacket(decoded.Packet, remaining[..decoded.Length]);
            remaining = remaining[decoded.Length..];
        }
    }

    private QuicDecodedPacket? TryDecode(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return QuicPacketCodec.Decode(bytes, settings.ConnectionIdLength);
        }
        catch (QuicTransportException)
        {
            // A packet whose header cannot be read is dropped with the rest of its datagram (RFC 9000 section 12.2).
            return null;
        }
    }

    private void ReceivePacket(QuicPacket packet, ReadOnlyMemory<byte> bytes)
    {
        switch (packet)
        {
            case QuicVersionNegotiationPacket versionNegotiation:
                ReceiveVersionNegotiation(versionNegotiation);
                break;
            case QuicRetryPacket retry:
                ReceiveRetry(retry);
                break;
            case QuicLongHeaderPacket longHeader:
                ReceiveLongHeader(longHeader, bytes);
                break;
            default:
                ReceiveShortHeader((QuicShortHeaderPacket)packet, bytes);
                break;
        }
    }

    private void ReceiveVersionNegotiation(QuicVersionNegotiationPacket packet)
    {
        // Ignored once any other packet has arrived, when it names another connection, and when it lists the version in use (RFC 9000 section 6.2).
        if (serverPacketReceived || !LocalConnectionIds.Contains(packet.DestinationConnectionId.Span) || packet.SupportedVersions.Contains(QuicPacketCodec.Version1))
        {
            return;
        }

        var versions = string.Join(", ", packet.SupportedVersions.Select(version => $"0x{version:x8}"));
        Fail(new QuicHandshakeFailure(CurlExitCode.CouldntConnect, $"The server supports QUIC versions {versions}, not version 1."), null);
    }

    private void ReceiveRetry(QuicRetryPacket packet)
    {
        // Only the first Retry, before any other packet, naming this connection, choosing a new
        // connection ID, with a valid integrity tag (RFC 9000 section 17.2.5.2).
        if (serverPacketReceived
            || !LocalConnectionIds.Contains(packet.DestinationConnectionId.Span)
            || packet.SourceConnectionId.Span.SequenceEqual(originalDestinationConnectionId)
            || !QuicRetryIntegrity.HasValidTag(originalDestinationConnectionId, packet))
        {
            return;
        }

        serverPacketReceived = true;
        retrySourceConnectionId = packet.SourceConnectionId.ToArray();
        destinationConnectionId = retrySourceConnectionId;
        token = packet.RetryToken.ToArray();
        InstallInitialKeys();
        initial.RequeueCrypto();

        // A Retry resets loss recovery for the Initial packets sent before it (RFC 9002 section 6.3).
        Recovery.DiscardSpace(QuicPacketNumberSpaceId.Initial, Now());
    }

    private void ReceiveLongHeader(QuicLongHeaderPacket header, ReadOnlyMemory<byte> bytes)
    {
        var space = header.Type == QuicPacketType.Initial ? initial : handshake;
        if (IsLongHeaderForThisClient(header) && Open(space, bytes) is { } packet)
        {
            AdoptServerConnectionId(header.SourceConnectionId.ToArray());
            ReceiveFrames(space, ((QuicLongHeaderPacket)packet).Payload);
        }
    }

    // A 0-RTT packet is only ever sent to a server; the client drops it. Once the server has
    // chosen its connection ID, a long header packet from any other is dropped (RFC 9000 section 7.2).
    private bool IsLongHeaderForThisClient(QuicLongHeaderPacket header) =>
        header.Type != QuicPacketType.ZeroRtt
        && LocalConnectionIds.Contains(header.DestinationConnectionId.Span)
        && (serverSourceConnectionId is null || header.SourceConnectionId.Span.SequenceEqual(serverSourceConnectionId));

    private void ReceiveShortHeader(QuicShortHeaderPacket header, ReadOnlyMemory<byte> bytes)
    {
        if (LocalConnectionIds.Contains(header.DestinationConnectionId.Span) && Open(application, bytes) is { } packet)
        {
            ReceiveFrames(application, ((QuicShortHeaderPacket)packet).Payload);
        }
    }

    private QuicPacket? Open(QuicPacketNumberSpace space, ReadOnlyMemory<byte> bytes)
    {
        var result = space.ReceiveProtection?.Unprotect(bytes, settings.ConnectionIdLength, space.LargestReceived);
        if (result is not { Status: QuicUnprotectStatus.Unprotected } || !space.RecordReceived(result.PacketNumber, Now()))
        {
            return null;
        }

        serverPacketReceived = true;
        return result.Packet;
    }

    private void AdoptServerConnectionId(byte[] connectionId)
    {
        if (serverSourceConnectionId is null)
        {
            // The server's first Initial packet chooses the connection ID the client sends to from now on (RFC 9000 section 7.2).
            serverSourceConnectionId = connectionId;
            destinationConnectionId = connectionId;
            PeerConnectionIds = new QuicPeerConnectionIds(connectionId, TransportParameters.ActiveConnectionIdLimit);
        }
    }

    private void ReceiveFrames(QuicPacketNumberSpace space, ReadOnlyMemory<byte> payload)
    {
        var frames = QuicFrameCodec.Decode(payload, space.PacketType);
        if (frames.Any(frame => frame.IsAckEliciting))
        {
            space.RequireAcknowledgement();
        }

        foreach (var frame in frames.TakeWhile(_ => Failure is null))
        {
            ReceiveFrame(space, frame);
        }
    }

    private void ReceiveFrame(QuicPacketNumberSpace space, QuicFrame frame)
    {
        switch (frame)
        {
            case QuicCryptoFrame crypto:
                ReceiveCrypto(space, crypto);
                break;
            case QuicAckFrame acknowledgement:
                ReceiveAcknowledgement(space, acknowledgement);
                break;
            case QuicConnectionCloseFrame close:
                Fail(QuicHandshakeFailures.FromServerClose(close), null);
                break;
            default:
                ReceiveConnectionFrame(frame);
                break;
        }
    }

    private void ReceiveAcknowledgement(QuicPacketNumberSpace space, QuicAckFrame frame)
    {
        space.ReceiveAcknowledgement(frame);

        // The ACK Delay is scaled by the server's ack_delay_exponent, 3 until its transport parameters arrive (RFC 9000 section 18.2).
        var exponent = (int)(ServerTransportParameters?.AckDelayExponent ?? QuicTransportParameters.DefaultAckDelayExponent);
        var ackDelay = TimeSpan.FromMicroseconds(Math.Min(frame.AckDelay, 1UL << 32) << exponent);
        RequeueLost(space, Recovery.OnAckReceived(space.Id, frame, ackDelay, Now()).Lost);
    }

    private void ReceiveConnectionFrame(QuicFrame frame)
    {
        switch (frame)
        {
            case QuicHandshakeDoneFrame:
                Confirm();
                break;
            case QuicNewTokenFrame newToken:
                receivedTokens.Add(newToken.Token.ToArray());
                break;
            case QuicNewConnectionIdFrame newConnectionId:
                QueueApplicationFrames(PeerConnectionIds!.Receive(newConnectionId));
                break;
            case QuicRetireConnectionIdFrame retireConnectionId:
                QueueApplicationFrames(LocalConnectionIds.Retire(retireConnectionId));
                break;
            default:
                // Stream and flow control frames are the streams'; PADDING and PING ask for nothing more.
                Streams.Receive(frame);
                break;
        }
    }

    private void QueueApplicationFrames(IEnumerable<QuicFrame> frames)
    {
        foreach (var frame in frames)
        {
            application.QueueFrame(frame);
        }
    }

    private void ReceiveCrypto(QuicPacketNumberSpace space, QuicCryptoFrame frame)
    {
        var bytes = space.CryptoReceived.Receive(frame);
        if (bytes.Length > 0)
        {
            Apply(tls.Receive(LevelOf(space), bytes));
        }
    }

    private TlsEncryptionLevel LevelOf(QuicPacketNumberSpace space) => (TlsEncryptionLevel)Array.IndexOf(spacesByLevel, space);

    private void Apply(Tls13HandshakeOutput output)
    {
        foreach (var secret in output.SecretsInstalled)
        {
            Install(secret);
        }

        foreach (var bytes in output.BytesToSend)
        {
            spacesByLevel[(int)bytes.Level]!.QueueCrypto(bytes.Bytes);
        }

        if (output.Failure is { } failure)
        {
            Fail(QuicHandshakeFailures.FromTls(failure), QuicHandshakeFailures.CryptoError(failure.Alert));
            return;
        }

        CheckServerTransportParameters();
        if (output.IsComplete && !IsComplete)
        {
            Complete();
        }
    }

    private void Install(Tls13TrafficSecret secret)
    {
        var space = spacesByLevel[(int)secret.Level]!;
        var protection = QuicPacketProtection.Create(tls.CipherSuite!, secret.Secret);
        Recovery.HasHandshakeKeys |= secret.Level == TlsEncryptionLevel.Handshake;
        if (secret.Direction == TlsTrafficDirection.Read)
        {
            space.ReceiveProtection = protection;
        }
        else
        {
            space.SendProtection = protection;
        }
    }

    private void CheckServerTransportParameters()
    {
        if (ServerTransportParameters is not null || tls.ServerQuicTransportParameters is not { } encoded)
        {
            return;
        }

        var parameters = QuicTransportParameters.Decode(encoded);
        RequireConnectionId(parameters.OriginalDestinationConnectionId, originalDestinationConnectionId, "original_destination_connection_id");
        RequireConnectionId(parameters.InitialSourceConnectionId, serverSourceConnectionId, "initial_source_connection_id");
        RequireConnectionId(parameters.RetrySourceConnectionId, retrySourceConnectionId, "retry_source_connection_id");
        if (parameters.VersionInformation is { ChosenVersion: not QuicPacketCodec.Version1 } versionInformation)
        {
            // The server must choose the version in use (RFC 9368 section 4).
            throw new QuicTransportException(QuicTransportErrorCode.VersionNegotiationError, $"The server's version_information chooses version 0x{versionInformation.ChosenVersion:x8}, not version 1.");
        }

        PeerConnectionIds!.SetHandshakeResetToken(parameters.StatelessResetToken);
        Streams.SetPeerTransportParameters(parameters);
        Recovery.MaxAckDelay = TimeSpan.FromMilliseconds(parameters.MaxAckDelay);
        ServerTransportParameters = parameters;
    }

    private static void RequireConnectionId(byte[]? sent, byte[]? used, string name)
    {
        // A missing, unexpected or different connection ID is a TRANSPORT_PARAMETER_ERROR (RFC 9000 section 7.3).
        if (!SameConnectionId(sent, used))
        {
            throw new QuicTransportException(QuicTransportErrorCode.TransportParameterError, $"The server's {name} does not match the connection ID in use.");
        }
    }

    private void Complete()
    {
        if (tls.ApplicationProtocol is null)
        {
            Fail(new QuicHandshakeFailure(CurlExitCode.SslConnectError, "The server chose no ALPN protocol; QUIC requires one."), QuicHandshakeFailures.CryptoError(TlsAlertDescription.NoApplicationProtocol));
            return;
        }

        if (ServerTransportParameters is null)
        {
            Fail(new QuicHandshakeFailure(CurlExitCode.SslConnectError, "The server sent no QUIC transport parameters."), QuicHandshakeFailures.CryptoError(TlsAlertDescription.MissingExtension));
            return;
        }

        IsComplete = true;
        QueueApplicationFrames(LocalConnectionIds.IssueUpTo(ServerTransportParameters.ActiveConnectionIdLimit));
    }

    private void Confirm()
    {
        IsConfirmed = true;
        Recovery.ConfirmHandshake(Now());

        // The client discards its Handshake keys once the handshake is confirmed (RFC 9001 section 4.9.2).
        DiscardSpace(initial);
        DiscardSpace(handshake);
    }
}
