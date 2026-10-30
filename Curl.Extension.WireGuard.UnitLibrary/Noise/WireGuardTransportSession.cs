using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Curl.Cryptography;
using Curl.Extension.WireGuard.Messages;

namespace Curl.Extension.WireGuard.Noise;

/// <summary>
/// The transport keys of one completed handshake (whitepaper section 5.4.5) and the counters
/// that go with them: <see cref="Seal" /> pads a packet to a multiple of 16 bytes and seals it
/// under the next 64-bit counter; <see cref="TryOpen" /> opens a transport data message once
/// its counter has passed the RFC 6479 replay window (section 5.4.6). The timers are section
/// 6.1's: after Rekey-After-Time (120 s) or Rekey-After-Messages (2^60) the session
/// <see cref="NeedsRekey" />; after Reject-After-Time (180 s) or Reject-After-Messages
/// (2^64 - 2^13 - 1) it neither seals nor opens.
/// </summary>
public sealed class WireGuardTransportSession : IDisposable
{
    /// <summary>Section 6.1's Rekey-After-Messages.</summary>
    public const ulong RekeyAfterMessages = 1UL << 60;

    /// <summary>Section 6.1's Reject-After-Messages.</summary>
    public const ulong RejectAfterMessages = ulong.MaxValue - (1UL << 13);

    /// <summary>Section 6.1's Rekey-After-Time.</summary>
    public static readonly TimeSpan RekeyAfterTime = TimeSpan.FromSeconds(120);

    /// <summary>Section 6.1's Reject-After-Time.</summary>
    public static readonly TimeSpan RejectAfterTime = TimeSpan.FromSeconds(180);

    private const int PaddingMultiple = 16;

    private readonly AeadChaCha20Poly1305 sendCipher;

    private readonly AeadChaCha20Poly1305 receiveCipher;

    private readonly WireGuardReplayWindow replayWindow = new();

    private readonly TimeProvider timeProvider;

    private readonly long createdAt;

    private ulong nextSendCounter;

    internal WireGuardTransportSession(
        uint localIndex,
        uint remoteIndex,
        ReadOnlySpan<byte> sendKey,
        ReadOnlySpan<byte> receiveKey,
        TimeProvider timeProvider,
        ulong nextSendCounter = 0)
    {
        LocalIndex = localIndex;
        RemoteIndex = remoteIndex;
        sendCipher = new AeadChaCha20Poly1305(sendKey);
        receiveCipher = new AeadChaCha20Poly1305(receiveKey);
        this.timeProvider = timeProvider;
        createdAt = timeProvider.GetTimestamp();
        this.nextSendCounter = nextSendCounter;
    }

    /// <summary>The index this side picked; incoming transport data names it.</summary>
    public uint LocalIndex { get; }

    /// <summary>The index the peer picked; outgoing transport data names it.</summary>
    public uint RemoteIndex { get; }

    /// <summary>Whether the initiator should start a new handshake: the keys are 120 s old or have sealed 2^60 messages.</summary>
    public bool NeedsRekey => nextSendCounter >= RekeyAfterMessages || Age >= RekeyAfterTime;

    private TimeSpan Age => timeProvider.GetElapsedTime(createdAt);

    private bool IsExpired => Age >= RejectAfterTime;

    /// <summary>
    /// Pads <paramref name="packet" /> with zeros to a multiple of 16 bytes, seals it under the
    /// next counter and returns the transport data message. An empty packet is a keepalive.
    /// </summary>
    /// <exception cref="InvalidOperationException">The keys are past Reject-After-Time or Reject-After-Messages.</exception>
    public byte[] Seal(ReadOnlySpan<byte> packet)
    {
        if (IsExpired || nextSendCounter >= RejectAfterMessages)
        {
            throw new InvalidOperationException("The WireGuard session's keys have expired; a new handshake is needed.");
        }

        var paddedLength = (packet.Length + PaddingMultiple - 1) / PaddingMultiple * PaddingMultiple;
        var padded = new byte[paddedLength];
        packet.CopyTo(padded);
        var sealedPacket = new byte[paddedLength + AeadChaCha20Poly1305.TagSize];
        var counter = nextSendCounter++;
        sendCipher.Encrypt(CounterNonce(counter), padded, sealedPacket.AsSpan(0, paddedLength), sealedPacket.AsSpan(paddedLength));
        return new TransportDataMessage(RemoteIndex, counter, sealedPacket).Encode();
    }

    /// <summary>
    /// Opens a transport data message addressed to <see cref="LocalIndex" />, giving the padded
    /// packet; the IP header inside says how much of it is the packet.
    /// </summary>
    /// <returns>
    /// <c>false</c> for a datagram that is not transport data for this session, a counter
    /// already received or behind the window, keys past Reject-After-Time, or a bad tag.
    /// </returns>
    public bool TryOpen(ReadOnlySpan<byte> datagram, [NotNullWhen(true)] out byte[]? packet)
    {
        packet = null;
        if (!TransportDataMessage.TryDecode(datagram, out var message) || !IsAcceptable(message))
        {
            return false;
        }

        var sealedPacket = message.EncryptedPacket;
        var plaintext = new byte[sealedPacket.Length - AeadChaCha20Poly1305.TagSize];
        if (!receiveCipher.TryDecrypt(CounterNonce(message.Counter), sealedPacket.AsSpan(0, plaintext.Length), sealedPacket.AsSpan(plaintext.Length), plaintext))
        {
            return false;
        }

        replayWindow.Mark(message.Counter);
        packet = plaintext;
        return true;
    }

    /// <summary>Zeroes both keys.</summary>
    public void Dispose()
    {
        sendCipher.Dispose();
        receiveCipher.Dispose();
    }

    private bool IsAcceptable(TransportDataMessage message) =>
        message.ReceiverIndex == LocalIndex
        && !IsExpired
        && replayWindow.IsAcceptable(message.Counter, RejectAfterMessages);

    /// <summary>The 12-byte nonce: four zero bytes, then the counter little-endian.</summary>
    private static byte[] CounterNonce(ulong counter)
    {
        var nonce = new byte[AeadChaCha20Poly1305.NonceSize];
        BinaryPrimitives.WriteUInt64LittleEndian(nonce.AsSpan(4), counter);
        return nonce;
    }
}
