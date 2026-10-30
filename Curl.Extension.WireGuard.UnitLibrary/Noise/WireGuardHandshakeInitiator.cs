using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.Messages;

namespace Curl.Extension.WireGuard.Noise;

/// <summary>
/// The initiator's side of WireGuard's Noise_IKpsk2_25519_ChaChaPoly_BLAKE2s handshake with one
/// peer (whitepaper sections 5.4.2 to 5.4.5 and 5.4.7): <see cref="CreateInitiation" /> writes a
/// handshake initiation, <see cref="TryConsumeCookieReply" /> takes the cookie a loaded peer
/// sends so the next initiation carries mac2, and <see cref="TryConsumeResponse" /> checks the
/// response's mac1 and Noise payload and gives the <see cref="WireGuardTransportSession" />.
/// The clock (TAI64N timestamps, the cookie's 120-second life) and the random source
/// (ephemeral keys, indices) are injected.
/// </summary>
public sealed class WireGuardHandshakeInitiator
{
    /// <summary>How long a cookie from a cookie reply is put in mac2 (section 5.4.4).</summary>
    public static readonly TimeSpan CookieLifetime = TimeSpan.FromSeconds(120);

    private const int KeySize = X25519.KeySize;

    private readonly byte[] staticPrivateKey = new byte[KeySize];

    private readonly byte[] staticPublicKey = new byte[KeySize];

    private readonly byte[] peerPublicKey;

    private readonly byte[] presharedKey = new byte[KeySize];

    private readonly byte[] staticSharedSecret = new byte[KeySize];

    private readonly byte[] peerMac1Key = new byte[KeySize];

    private readonly byte[] ownMac1Key = new byte[KeySize];

    private readonly byte[] peerCookieKey = new byte[KeySize];

    private readonly TimeProvider timeProvider;

    private readonly RandomNumberGenerator random;

    private PendingHandshake? pending;

    private byte[]? cookie;

    private long cookieReceivedAt;

    /// <summary>Prepares a handshake from this side's static private key to one peer.</summary>
    /// <param name="privateKey">This side's 32-byte static private key.</param>
    /// <param name="peerPublicKey">The peer's 32-byte static public key.</param>
    /// <param name="presharedKey">The 32-byte preshared key, or empty for none (all zero, section 5.2).</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="random">The source of ephemeral keys and indices.</param>
    /// <exception cref="ArgumentException">
    /// A key is not 32 bytes, or the peer's public key is a low-order point that gives an all-zero shared secret.
    /// </exception>
    public WireGuardHandshakeInitiator(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        ReadOnlySpan<byte> presharedKey,
        TimeProvider timeProvider,
        RandomNumberGenerator random)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(random);
        X25519.ComputePublicKey(privateKey, staticPublicKey);
        privateKey.CopyTo(staticPrivateKey);
        if (!X25519.TryComputeSharedSecret(privateKey, peerPublicKey, staticSharedSecret))
        {
            throw new ArgumentException("The peer's public key is a low-order point.", nameof(peerPublicKey));
        }

        if (!presharedKey.IsEmpty)
        {
            WireGuardMessageLayout.WriteField(presharedKey, this.presharedKey, nameof(presharedKey));
        }

        this.peerPublicKey = peerPublicKey.ToArray();
        WireGuardMessageAuthentication.DeriveMac1Key(peerPublicKey, peerMac1Key);
        WireGuardMessageAuthentication.DeriveMac1Key(staticPublicKey, ownMac1Key);
        WireGuardMessageAuthentication.DeriveCookieKey(peerPublicKey, peerCookieKey);
        this.timeProvider = timeProvider;
        this.random = random;
    }

    /// <summary>Prepares a handshake from a wg-quick configuration's interface to one of its peers.</summary>
    public static WireGuardHandshakeInitiator ForPeer(
        WireGuardInterface wireGuardInterface,
        WireGuardPeer peer,
        TimeProvider timeProvider,
        RandomNumberGenerator random)
    {
        ArgumentNullException.ThrowIfNull(wireGuardInterface);
        ArgumentNullException.ThrowIfNull(peer);
        return new WireGuardHandshakeInitiator(wireGuardInterface.PrivateKey, peer.PublicKey, peer.PresharedKey, timeProvider, random);
    }

    /// <summary>
    /// Writes a new handshake initiation (section 5.4.2) with a new ephemeral key and index,
    /// mac1, and mac2 when a cookie younger than <see cref="CookieLifetime" /> is held. It
    /// replaces any handshake still waiting for its response.
    /// </summary>
    public byte[] CreateInitiation()
    {
        var ephemeralPrivateKey = new byte[KeySize];
        random.GetBytes(ephemeralPrivateKey);
        var ephemeralPublicKey = new byte[KeySize];
        X25519.ComputePublicKey(ephemeralPrivateKey, ephemeralPublicKey);
        Span<byte> indexBytes = stackalloc byte[sizeof(uint)];
        random.GetBytes(indexBytes);
        var senderIndex = BinaryPrimitives.ReadUInt32LittleEndian(indexBytes);

        var state = NoiseSymmetricState.ForResponder(peerPublicKey);
        state.MixKey(ephemeralPublicKey);
        state.MixHash(ephemeralPublicKey);
        Span<byte> key = stackalloc byte[KeySize];
        Span<byte> sharedSecret = stackalloc byte[KeySize];
        _ = X25519.TryComputeSharedSecret(ephemeralPrivateKey, peerPublicKey, sharedSecret);
        state.MixKeyAndDeriveKey(sharedSecret, key);
        var encryptedStatic = state.EncryptAndHash(key, staticPublicKey);
        state.MixKeyAndDeriveKey(staticSharedSecret, key);
        Span<byte> timestamp = stackalloc byte[WireGuardTai64nTimestamp.Size];
        WireGuardTai64nTimestamp.Write(timeProvider.GetUtcNow(), timestamp);
        var encryptedTimestamp = state.EncryptAndHash(key, timestamp);
        key.Clear();
        sharedSecret.Clear();

        var message = new HandshakeInitiationMessage(
            senderIndex,
            ephemeralPublicKey,
            encryptedStatic,
            encryptedTimestamp,
            new byte[WireGuardMessageAuthentication.MacSize],
            new byte[WireGuardMessageAuthentication.MacSize]).Encode();
        WireGuardMessageAuthentication.WriteMac1(message, peerMac1Key);
        if (cookie is not null && timeProvider.GetElapsedTime(cookieReceivedAt) < CookieLifetime)
        {
            WireGuardMessageAuthentication.WriteMac2(message, cookie);
        }

        pending = new PendingHandshake(senderIndex, ephemeralPrivateKey, state, message[^32..^16]);
        return message;
    }

    /// <summary>
    /// Takes the cookie from a cookie reply to the initiation waiting for its response (section
    /// 5.4.7), so the next <see cref="CreateInitiation" /> puts it in mac2.
    /// </summary>
    /// <returns><c>false</c> for a datagram that is not a cookie reply to that initiation, or one that does not open.</returns>
    public bool TryConsumeCookieReply(ReadOnlySpan<byte> datagram)
    {
        var handshake = pending;
        if (!CookieReplyMessage.TryDecode(datagram, out var message) || handshake is null || handshake.SenderIndex != message.ReceiverIndex)
        {
            return false;
        }

        var receivedCookie = new byte[WireGuardMessageAuthentication.MacSize];
        using var aead = new AeadXChaCha20Poly1305(peerCookieKey);
        if (!aead.TryDecrypt(message.Nonce, message.EncryptedCookie.AsSpan(0, 16), message.EncryptedCookie.AsSpan(16), receivedCookie, handshake.Mac1))
        {
            return false;
        }

        cookie = receivedCookie;
        cookieReceivedAt = timeProvider.GetTimestamp();
        return true;
    }

    /// <summary>
    /// Completes the handshake from the response to the initiation waiting for it (sections
    /// 5.4.3 to 5.4.5): checks mac1, mixes in the responder's ephemeral key and the preshared
    /// key, opens the empty payload and derives the transport keys. A response is taken once.
    /// </summary>
    /// <returns>
    /// <c>false</c>, the waiting handshake kept, for a datagram that is not a response to it, a
    /// bad mac1, a low-order ephemeral key, or a payload that does not open (a responder
    /// without the peer's static private key or the preshared key).
    /// </returns>
    public bool TryConsumeResponse(ReadOnlySpan<byte> datagram, [NotNullWhen(true)] out WireGuardTransportSession? session)
    {
        session = null;
        var handshake = pending;
        if (!HandshakeResponseMessage.TryDecode(datagram, out var message)
            || handshake is null
            || handshake.SenderIndex != message.ReceiverIndex
            || !WireGuardMessageAuthentication.HasValidMac1(datagram, ownMac1Key))
        {
            return false;
        }

        var state = handshake.State.Clone();
        if (!TryMixResponse(state, handshake.EphemeralPrivateKey, message))
        {
            return false;
        }

        Span<byte> sendKey = stackalloc byte[KeySize];
        Span<byte> receiveKey = stackalloc byte[KeySize];
        state.DeriveTransportKeys(sendKey, receiveKey);
        session = new WireGuardTransportSession(handshake.SenderIndex, message.SenderIndex, sendKey, receiveKey, timeProvider);
        sendKey.Clear();
        receiveKey.Clear();
        CryptographicOperations.ZeroMemory(handshake.EphemeralPrivateKey);
        pending = null;
        return true;
    }

    /// <summary>
    /// Sends an initiation over <paramref name="channel" /> and reads datagrams until a response
    /// completes the handshake; a cookie reply sends a new initiation carrying mac2 at once, and
    /// any other datagram is ignored. Retransmission after Rekey-Timeout is the caller's, by
    /// cancelling and calling again.
    /// </summary>
    public async Task<WireGuardTransportSession> ConnectAsync(IWireGuardDatagramChannel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        await channel.SendAsync(CreateInitiation(), cancellationToken).ConfigureAwait(false);
        while (true)
        {
            var datagram = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (TryConsumeResponse(datagram.Span, out var session))
            {
                return session;
            }

            if (TryConsumeCookieReply(datagram.Span))
            {
                await channel.SendAsync(CreateInitiation(), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The initiator's half of section 5.4.3, on a copy of the waiting state.</summary>
    private bool TryMixResponse(NoiseSymmetricState state, byte[] ephemeralPrivateKey, HandshakeResponseMessage message)
    {
        var responderEphemeral = message.UnencryptedEphemeral;
        state.MixKey(responderEphemeral);
        state.MixHash(responderEphemeral);
        Span<byte> sharedSecret = stackalloc byte[KeySize];
        Span<byte> key = stackalloc byte[KeySize];
        try
        {
            if (!X25519.TryComputeSharedSecret(ephemeralPrivateKey, responderEphemeral, sharedSecret))
            {
                return false;
            }

            state.MixKey(sharedSecret);
            _ = X25519.TryComputeSharedSecret(staticPrivateKey, responderEphemeral, sharedSecret);
            state.MixKey(sharedSecret);
            state.MixPresharedKey(presharedKey, key);
            return state.TryDecryptAndHash(key, message.EncryptedNothing, Span<byte>.Empty);
        }
        finally
        {
            sharedSecret.Clear();
            key.Clear();
        }
    }

    private sealed record PendingHandshake(uint SenderIndex, byte[] EphemeralPrivateKey, NoiseSymmetricState State, byte[] Mac1);
}
