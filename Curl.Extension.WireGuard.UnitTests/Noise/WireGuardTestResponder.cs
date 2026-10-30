using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Extension.WireGuard.Messages;

namespace Curl.Extension.WireGuard.UnitTests.Noise;

/// <summary>
/// A responder written in the tests straight from whitepaper sections 5.4.2 to 5.4.5 and
/// 5.4.7, on the cryptographic primitives alone (none of the library's Noise code), so the
/// initiator is checked against a second reading of the specification.
/// </summary>
internal sealed class WireGuardTestResponder
{
    public const uint ResponderIndex = 0x0A0B0C0D;

    private readonly byte[] staticPrivateKey;

    private readonly byte[] initiatorPublicKey;

    private readonly byte[] presharedKey;

    private readonly byte[] lastTimestamp = new byte[12];

    private byte[] chainingKey = [];

    private byte[] hash = [];

    private byte[] initiatorEphemeral = [];

    private uint initiatorIndex;

    public WireGuardTestResponder(byte[] staticPrivateKey, byte[] initiatorPublicKey, byte[] presharedKey)
    {
        this.staticPrivateKey = staticPrivateKey;
        this.initiatorPublicKey = initiatorPublicKey;
        this.presharedKey = presharedKey;
        X25519.ComputePublicKey(staticPrivateKey, StaticPublicKey);
    }

    public byte[] StaticPublicKey { get; } = new byte[32];

    /// <summary>The responder's send key, the initiator's receive key.</summary>
    public byte[] SendKey { get; private set; } = [];

    /// <summary>The responder's receive key, the initiator's send key.</summary>
    public byte[] ReceiveKey { get; private set; } = [];

    /// <summary>
    /// Processes an initiation (section 5.4.2). Every step runs even when one fails, so a
    /// response can still be written from what the responder holds.
    /// </summary>
    /// <returns>Whether mac1 matched, the static key opened as the expected initiator's and the timestamp opened and was newer than any before.</returns>
    public bool TryConsumeInitiation(byte[] datagram)
    {
        Assert.IsTrue(HandshakeInitiationMessage.TryDecode(datagram, out var message));
        var mac1Valid = WireGuardMessageAuthentication.HasValidMac1(datagram, Labelled("mac1----", StaticPublicKey));
        initiatorIndex = message.SenderIndex;
        initiatorEphemeral = message.UnencryptedEphemeral;
        chainingKey = Hash(Encoding("Noise_IKpsk2_25519_ChaChaPoly_BLAKE2s"));
        hash = Hash(chainingKey, Encoding("WireGuard v1 zx2c4 Jason@zx2c4.com"));
        hash = Hash(hash, StaticPublicKey);
        chainingKey = Kdf(chainingKey, initiatorEphemeral, 1)[0];
        hash = Hash(hash, initiatorEphemeral);
        var derived = Kdf(chainingKey, SharedSecret(staticPrivateKey, initiatorEphemeral), 2);
        chainingKey = derived[0];
        var decryptedStatic = Open(derived[1], message.EncryptedStatic);
        hash = Hash(hash, message.EncryptedStatic);
        derived = Kdf(chainingKey, SharedSecret(staticPrivateKey, initiatorPublicKey), 2);
        chainingKey = derived[0];
        var timestamp = Open(derived[1], message.EncryptedTimestamp);
        hash = Hash(hash, message.EncryptedTimestamp);
        var staticValid = decryptedStatic is not null && decryptedStatic.AsSpan().SequenceEqual(initiatorPublicKey);
        var timestampValid = timestamp is not null && timestamp.AsSpan().SequenceCompareTo(lastTimestamp) > 0;
        if (timestampValid)
        {
            timestamp!.CopyTo(lastTimestamp, 0);
        }

        return mac1Valid && staticValid && timestampValid;
    }

    /// <summary>Writes the response (section 5.4.3) from an ephemeral key of <paramref name="ephemeralPrivateKey" />, and derives the transport keys (5.4.5).</summary>
    public byte[] CreateResponse(byte[] ephemeralPrivateKey)
    {
        var ephemeralPublicKey = new byte[32];
        X25519.ComputePublicKey(ephemeralPrivateKey, ephemeralPublicKey);
        chainingKey = Kdf(chainingKey, ephemeralPublicKey, 1)[0];
        hash = Hash(hash, ephemeralPublicKey);
        chainingKey = Kdf(chainingKey, SharedSecret(ephemeralPrivateKey, initiatorEphemeral), 1)[0];
        chainingKey = Kdf(chainingKey, SharedSecret(ephemeralPrivateKey, initiatorPublicKey), 1)[0];
        var derived = Kdf(chainingKey, presharedKey, 3);
        chainingKey = derived[0];
        hash = Hash(hash, derived[1]);
        var empty = Seal(derived[2], []);
        hash = Hash(hash, empty);
        var transport = Kdf(chainingKey, [], 2);
        ReceiveKey = transport[0];
        SendKey = transport[1];
        var response = new HandshakeResponseMessage(ResponderIndex, initiatorIndex, ephemeralPublicKey, empty, new byte[16], new byte[16]).Encode();
        WireGuardMessageAuthentication.WriteMac1(response, Labelled("mac1----", initiatorPublicKey));
        return response;
    }

    /// <summary>Writes a cookie reply (section 5.4.7) to <paramref name="initiation" /> carrying <paramref name="cookie" />.</summary>
    public byte[] CreateCookieReply(byte[] initiation, byte[] cookie, byte[] nonce)
    {
        Assert.IsTrue(HandshakeInitiationMessage.TryDecode(initiation, out var message));
        var encryptedCookie = new byte[32];
        using var aead = new AeadXChaCha20Poly1305(Labelled("cookie--", StaticPublicKey));
        aead.Encrypt(nonce, cookie, encryptedCookie.AsSpan(0, 16), encryptedCookie.AsSpan(16), message.Mac1);
        return new CookieReplyMessage(message.SenderIndex, nonce, encryptedCookie).Encode();
    }

    /// <summary>Seals a transport data message to the initiator under <paramref name="counter" /> with the responder's send key.</summary>
    public byte[] SealTransport(uint receiverIndex, ulong counter, byte[] paddedPacket)
    {
        var sealedPacket = new byte[paddedPacket.Length + 16];
        using var aead = new AeadChaCha20Poly1305(SendKey);
        aead.Encrypt(CounterNonce(counter), paddedPacket, sealedPacket.AsSpan(0, paddedPacket.Length), sealedPacket.AsSpan(paddedPacket.Length));
        return new TransportDataMessage(receiverIndex, counter, sealedPacket).Encode();
    }

    /// <summary>Opens a transport data message from the initiator with the responder's receive key.</summary>
    public byte[]? OpenTransport(byte[] datagram)
    {
        Assert.IsTrue(TransportDataMessage.TryDecode(datagram, out var message));
        var plaintext = new byte[message.EncryptedPacket.Length - 16];
        using var aead = new AeadChaCha20Poly1305(ReceiveKey);
        return aead.TryDecrypt(CounterNonce(message.Counter), message.EncryptedPacket.AsSpan(0, plaintext.Length), message.EncryptedPacket.AsSpan(plaintext.Length), plaintext)
            ? plaintext
            : null;
    }

    private byte[]? Open(byte[] key, byte[] sealedMessage)
    {
        var plaintext = new byte[sealedMessage.Length - 16];
        using var aead = new AeadChaCha20Poly1305(key);
        return aead.TryDecrypt(new byte[12], sealedMessage.AsSpan(0, plaintext.Length), sealedMessage.AsSpan(plaintext.Length), plaintext, hash)
            ? plaintext
            : null;
    }

    private byte[] Seal(byte[] key, byte[] plaintext)
    {
        var sealedMessage = new byte[plaintext.Length + 16];
        using var aead = new AeadChaCha20Poly1305(key);
        aead.Encrypt(new byte[12], plaintext, sealedMessage.AsSpan(0, plaintext.Length), sealedMessage.AsSpan(plaintext.Length), hash);
        return sealedMessage;
    }

    private static byte[] CounterNonce(ulong counter)
    {
        var nonce = new byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(nonce.AsSpan(4), counter);
        return nonce;
    }

    private static byte[] SharedSecret(byte[] privateKey, byte[] publicKey)
    {
        var secret = new byte[32];
        _ = X25519.TryComputeSharedSecret(privateKey, publicKey, secret);
        return secret;
    }

    /// <summary>Section 5.4's KDFn: HKDF over HMAC-BLAKE2s.</summary>
    private static byte[][] Kdf(byte[] key, byte[] input, int count)
    {
        var pseudorandomKey = new byte[32];
        HmacBlake2s.HashData(key, input, pseudorandomKey);
        var outputs = new byte[count][];
        byte[] previous = [];
        for (var index = 0; index < count; index++)
        {
            outputs[index] = new byte[32];
            HmacBlake2s.HashData(pseudorandomKey, [.. previous, (byte)(index + 1)], outputs[index]);
            previous = outputs[index];
        }

        return outputs;
    }

    private static byte[] Labelled(string label, byte[] publicKey) => Hash(Encoding(label), publicKey);

    private static byte[] Encoding(string text) => System.Text.Encoding.ASCII.GetBytes(text);

    private static byte[] Hash(byte[] first, byte[]? second = null)
    {
        var digest = new byte[32];
        Blake2s.HashData([.. first, .. second ?? []], digest);
        return digest;
    }

    internal static byte[] NewKey(byte seed)
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        key[0] = seed;
        return key;
    }
}
