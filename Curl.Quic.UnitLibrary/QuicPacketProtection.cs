using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// Packet protection for one direction at one encryption level (RFC 9001 section 5):
/// the AEAD over each packet's payload with its header as associated data, then header
/// protection over the first byte and the packet number. At the 1-RTT level it also holds
/// the key phase (section 6): <see cref="UpdateKeys" /> moves to the next generation of
/// AEAD keys, derived with <c>quic ku</c>, and a received short-header packet whose Key
/// Phase bit differs is tried with the next keys, or with the previous ones when it is
/// older than the current phase, so reordered packets still open. The header protection
/// key never changes.
/// </summary>
public sealed class QuicPacketProtection : IDisposable
{
    /// <summary>The length of the AEAD authentication tag every QUIC version 1 packet carries.</summary>
    public const int TagLength = 16;

    private const byte LongHeaderBit = 0x80;

    private const byte KeyPhaseBit = 0x04;

    private readonly Tls13CipherSuite cipherSuite;

    private readonly QuicHeaderProtection headerProtection;

    private byte[] currentSecret;

    private QuicPayloadProtection current;

    private QuicPayloadProtection? next;

    private QuicPayloadProtection? previous;

    private ulong? firstPacketNumberOfPhase;

    private QuicPacketProtection(Tls13CipherSuite cipherSuite, byte[] secret)
    {
        this.cipherSuite = cipherSuite;
        currentSecret = secret;
        QuicPacketKeys keys = QuicPacketKeys.Derive(cipherSuite, secret);
        headerProtection = QuicHeaderProtection.Create(cipherSuite, keys.HeaderProtectionKey);
        current = QuicPayloadProtection.Create(cipherSuite, keys);
    }

    /// <summary>Gets the Key Phase bit of the current keys: <c>false</c> for the first generation, flipping with each update.</summary>
    public bool KeyPhase { get; private set; }

    /// <summary>
    /// Returns whether QUIC packets can be protected under <paramref name="cipherSuite" />:
    /// <c>TLS_AES_128_GCM_SHA256</c>, <c>TLS_AES_256_GCM_SHA384</c>,
    /// <c>TLS_CHACHA20_POLY1305_SHA256</c> and <c>TLS_AES_128_CCM_SHA256</c> (RFC 9001 section 5.3).
    /// <c>TLS_AES_128_CCM_8_SHA256</c>, whose 8-byte tag QUIC does not use, is refused.
    /// </summary>
    /// <param name="cipherSuite">The cipher suite code point.</param>
    /// <returns>Whether <see cref="Create" /> accepts the suite.</returns>
    public static bool CanProtect(ushort cipherSuite) => cipherSuite is 0x1301 or 0x1302 or 0x1303 or 0x1304;

    /// <summary>Creates the protection a packet protection secret gives, at the first key phase.</summary>
    /// <param name="cipherSuite">The negotiated suite.</param>
    /// <param name="secret">The secret TLS installed for this level and direction.</param>
    /// <returns>The protection.</returns>
    /// <exception cref="ArgumentException">QUIC packets cannot be protected under the suite here (<see cref="CanProtect" />).</exception>
    public static QuicPacketProtection Create(Tls13CipherSuite cipherSuite, byte[] secret)
    {
        RequireProtectable(cipherSuite);
        ArgumentNullException.ThrowIfNull(secret);
        return new QuicPacketProtection(cipherSuite, secret);
    }

    /// <summary>Creates the protection of the client's Initial packets (RFC 9001 section 5.2).</summary>
    /// <param name="destinationConnectionId">The Destination Connection ID of the client's first Initial packet.</param>
    /// <returns>The protection.</returns>
    public static QuicPacketProtection CreateClientInitial(ReadOnlySpan<byte> destinationConnectionId) =>
        new(QuicInitialSecrets.CipherSuite, QuicInitialSecrets.DeriveClientInitialSecret(destinationConnectionId));

    /// <summary>Creates the protection of the server's Initial packets (RFC 9001 section 5.2).</summary>
    /// <param name="destinationConnectionId">The Destination Connection ID of the client's first Initial packet.</param>
    /// <returns>The protection.</returns>
    public static QuicPacketProtection CreateServerInitial(ReadOnlySpan<byte> destinationConnectionId) =>
        new(QuicInitialSecrets.CipherSuite, QuicInitialSecrets.DeriveServerInitialSecret(destinationConnectionId));

    /// <summary>
    /// Protects a packet: encrypts its payload under the current keys with the header as
    /// associated data, then applies header protection. A short-header packet is sent with
    /// the current <see cref="KeyPhase" />, whatever its own Key Phase bit says.
    /// </summary>
    /// <param name="packet">An Initial, 0-RTT, Handshake or 1-RTT packet whose payload is the plaintext frames.</param>
    /// <param name="packetNumber">The full packet number, whose low bytes the packet carries.</param>
    /// <returns>The protected packet, ready to put in a datagram.</returns>
    /// <exception cref="ArgumentException">The packet is a Retry or Version Negotiation packet, it does not carry the low bytes of <paramref name="packetNumber" />, or its payload is too short for the header protection sample and needs padding.</exception>
    public byte[] Protect(QuicPacket packet, ulong packetNumber)
    {
        ArgumentNullException.ThrowIfNull(packet);
        (ReadOnlyMemory<byte> plaintext, int packetNumberLength, uint truncatedPacketNumber) = Describe(packet);
        if (QuicPacketNumber.Truncate(packetNumber, packetNumberLength) != truncatedPacketNumber)
        {
            throw new ArgumentException($"The packet carries 0x{truncatedPacketNumber:x}, not the low {packetNumberLength} bytes of packet number {packetNumber}.", nameof(packetNumber));
        }

        byte[] bytes = QuicPacketCodec.Encode(ForProtection(packet, new byte[plaintext.Length + TagLength]));
        int headerLength = bytes.Length - plaintext.Length - TagLength;
        int packetNumberOffset = headerLength - packetNumberLength;
        if (!QuicHeaderProtection.HasRoomForSample(packetNumberOffset, bytes.Length))
        {
            throw new ArgumentException($"A {plaintext.Length}-byte payload after a {packetNumberLength}-byte packet number leaves no room for the header protection sample; pad it to at least {QuicHeaderProtection.SampleOffset - packetNumberLength} bytes.", nameof(packet));
        }

        current.Encrypt(packetNumber, bytes.AsSpan(0, headerLength), plaintext.Span, bytes.AsSpan(headerLength));
        headerProtection.Apply(bytes, packetNumberOffset);
        return bytes;
    }

    /// <summary>
    /// Removes protection from the packet at the front of a datagram. A packet that is too
    /// short for the sample or fails the AEAD is dropped with its status, never thrown; a
    /// short-header packet of the next key phase that opens moves this direction to it.
    /// </summary>
    /// <param name="datagram">The datagram, or what is left of it after the packets before.</param>
    /// <param name="shortHeaderConnectionIdLength">The length of the connection IDs this endpoint issued.</param>
    /// <param name="largestPacketNumber">The largest packet number received so far in this packet number space, or <see langword="null" /> when none has been.</param>
    /// <returns>The unprotected packet, or why it was dropped, and the bytes it took.</returns>
    /// <exception cref="QuicTransportException">The header cannot be read (<see cref="QuicPacketCodec.Decode" />), or an authentic packet has a Reserved Bit set (<see cref="QuicTransportErrorCode.ProtocolViolation" />).</exception>
    /// <exception cref="ArgumentException">The packet is a Retry or Version Negotiation packet, which carries no packet protection.</exception>
    public QuicUnprotectResult Unprotect(ReadOnlyMemory<byte> datagram, int shortHeaderConnectionIdLength, ulong? largestPacketNumber)
    {
        QuicDecodedPacket decoded = QuicPacketCodec.Decode(datagram, shortHeaderConnectionIdLength);
        int packetNumberOffset = decoded.HeaderLength - Describe(decoded.Packet).PacketNumberLength;
        if (!QuicHeaderProtection.HasRoomForSample(packetNumberOffset, decoded.Length))
        {
            return Dropped(QuicUnprotectStatus.DroppedTooShortForSample, decoded.Length);
        }

        byte[] bytes = datagram[..decoded.Length].ToArray();
        int packetNumberLength = headerProtection.Remove(bytes, packetNumberOffset);
        int headerLength = packetNumberOffset + packetNumberLength;
        ulong packetNumber = QuicPacketNumber.Decode(largestPacketNumber, ReadTruncatedPacketNumber(bytes.AsSpan(packetNumberOffset, packetNumberLength)), packetNumberLength);
        QuicPayloadProtection keys = SelectKeys(HasOtherKeyPhase(bytes[0]), packetNumber);
        var plaintext = new byte[bytes.Length - headerLength - TagLength];
        if (!keys.TryDecrypt(packetNumber, bytes.AsSpan(0, headerLength), bytes.AsSpan(headerLength), plaintext))
        {
            return Dropped(QuicUnprotectStatus.DroppedAuthenticationFailed, decoded.Length);
        }

        RequireReservedBitsClear(bytes[0]);
        bool keyPhaseChanged = RecordReceivedPhase(keys, packetNumber);
        QuicPacket packet = WithPayload(QuicPacketCodec.Decode(bytes, shortHeaderConnectionIdLength).Packet, plaintext);
        return new QuicUnprotectResult(QuicUnprotectStatus.Unprotected, packet, packetNumber, decoded.Length, keyPhaseChanged);
    }

    /// <summary>
    /// Moves to the next key phase (RFC 9001 section 6.1): the next-generation AEAD keys
    /// become current, the current ones are kept as the previous keys for packets still in
    /// flight, and <see cref="KeyPhase" /> flips.
    /// </summary>
    public void UpdateKeys()
    {
        previous?.Dispose();
        previous = current;
        current = next ?? CreateNextKeys();
        currentSecret = QuicPacketKeys.DeriveNextSecret(cipherSuite, currentSecret);
        next = null;
        KeyPhase = !KeyPhase;
        firstPacketNumberOfPhase = null;
    }

    /// <summary>Discards the previous key phase's keys (RFC 9001 section 6.5): packets of that phase are dropped from now on.</summary>
    public void DiscardPreviousKeys()
    {
        previous?.Dispose();
        previous = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        headerProtection.Dispose();
        current.Dispose();
        next?.Dispose();
        previous?.Dispose();
    }

    /// <summary>Refuses a suite QUIC packets cannot be protected under here.</summary>
    internal static void RequireProtectable(Tls13CipherSuite cipherSuite)
    {
        ArgumentNullException.ThrowIfNull(cipherSuite);
        if (!CanProtect(cipherSuite.Code))
        {
            throw new ArgumentException($"QUIC packets cannot be protected under cipher suite 0x{cipherSuite.Code:x4} yet.", nameof(cipherSuite));
        }
    }

    private static (ReadOnlyMemory<byte> Payload, int PacketNumberLength, uint TruncatedPacketNumber) Describe(QuicPacket packet) => packet switch
    {
        QuicLongHeaderPacket longHeader => (longHeader.Payload, longHeader.PacketNumberLength, longHeader.TruncatedPacketNumber),
        QuicShortHeaderPacket shortHeader => (shortHeader.Payload, shortHeader.PacketNumberLength, shortHeader.TruncatedPacketNumber),
        _ => throw new ArgumentException($"A {packet.GetType().Name} carries no packet protection.", nameof(packet)),
    };

    private QuicPacket ForProtection(QuicPacket packet, byte[] payload) => packet is QuicLongHeaderPacket longHeader
        ? longHeader with { Payload = payload }
        : ((QuicShortHeaderPacket)packet) with { Payload = payload, KeyPhase = KeyPhase };

    private static QuicPacket WithPayload(QuicPacket packet, byte[] payload) => packet is QuicLongHeaderPacket longHeader
        ? longHeader with { Payload = payload }
        : ((QuicShortHeaderPacket)packet) with { Payload = payload };

    private static uint ReadTruncatedPacketNumber(ReadOnlySpan<byte> packetNumber)
    {
        uint value = 0;
        foreach (byte octet in packetNumber)
        {
            value = value << 8 | octet;
        }

        return value;
    }

    private static QuicUnprotectResult Dropped(QuicUnprotectStatus status, int length) => new(status, null, 0, length, false);

    private static void RequireReservedBitsClear(byte firstByte)
    {
        int reservedBits = (firstByte & LongHeaderBit) != 0 ? 0x0c : 0x18;
        if ((firstByte & reservedBits) != 0)
        {
            throw new QuicTransportException(QuicTransportErrorCode.ProtocolViolation, "An authentic packet has a Reserved Bit set.");
        }
    }

    private bool HasOtherKeyPhase(byte firstByte) =>
        (firstByte & LongHeaderBit) == 0 && ((firstByte & KeyPhaseBit) != 0) != KeyPhase;

    private QuicPayloadProtection SelectKeys(bool otherKeyPhase, ulong packetNumber)
    {
        if (!otherKeyPhase)
        {
            return current;
        }

        if (previous is not null && !(packetNumber >= firstPacketNumberOfPhase))
        {
            return previous;
        }

        return next ??= CreateNextKeys();
    }

    private bool RecordReceivedPhase(QuicPayloadProtection keys, ulong packetNumber)
    {
        if (keys == next)
        {
            UpdateKeys();
            firstPacketNumberOfPhase = packetNumber;
            return true;
        }

        if (keys == current)
        {
            firstPacketNumberOfPhase = Math.Min(firstPacketNumberOfPhase ?? packetNumber, packetNumber);
        }

        return false;
    }

    private QuicPayloadProtection CreateNextKeys() =>
        QuicPayloadProtection.Create(cipherSuite, QuicPacketKeys.Derive(cipherSuite, QuicPacketKeys.DeriveNextSecret(cipherSuite, currentSecret)));
}
