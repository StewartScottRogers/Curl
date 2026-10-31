using Curl.Cryptography;

namespace Curl.Extension.WireGuard.Noise;

/// <summary>
/// The chaining key C and hash H of Noise_IKpsk2_25519_ChaChaPoly_BLAKE2s as the WireGuard
/// whitepaper's sections 5.4.2 to 5.4.5 advance them. KDFn is HKDF over HMAC-BLAKE2s, AEAD is
/// ChaCha20-Poly1305 with a zero counter (each handshake key seals one message) and H as the
/// associated data. Both sides of a handshake use it; the tests' responder does too.
/// </summary>
internal sealed class NoiseSymmetricState
{
    /// <summary>The length of C, H and every key.</summary>
    public const int KeySize = Blake2s.HashSize;

    private readonly byte[] chainingKey = new byte[KeySize];

    private readonly byte[] hash = new byte[KeySize];

    private NoiseSymmetricState()
    {
    }

    /// <summary>
    /// The state both sides start an initiation from: C = HASH(CONSTRUCTION),
    /// H = HASH(C || IDENTIFIER), then H = HASH(H || <paramref name="responderStaticPublicKey" />).
    /// </summary>
    public static NoiseSymmetricState ForResponder(ReadOnlySpan<byte> responderStaticPublicKey)
    {
        var state = new NoiseSymmetricState();
        Blake2s.HashData("Noise_IKpsk2_25519_ChaChaPoly_BLAKE2s"u8, state.chainingKey);
        HashConcatenated(state.chainingKey, "WireGuard v1 zx2c4 Jason@zx2c4.com"u8, state.hash);
        state.MixHash(responderStaticPublicKey);
        return state;
    }

    /// <summary>A separate copy, so a message that fails part-way leaves this state as it was.</summary>
    public NoiseSymmetricState Clone()
    {
        var copy = new NoiseSymmetricState();
        chainingKey.CopyTo(copy.chainingKey, 0);
        hash.CopyTo(copy.hash, 0);
        return copy;
    }

    /// <summary>H = HASH(H || <paramref name="data" />).</summary>
    public void MixHash(ReadOnlySpan<byte> data) => HashConcatenated(hash, data, hash);

    /// <summary>C = KDF1(C, <paramref name="input" />).</summary>
    public void MixKey(ReadOnlySpan<byte> input) => DeriveKeys(input, chainingKey, default, default);

    /// <summary>(C, <paramref name="key" />) = KDF2(C, <paramref name="input" />).</summary>
    public void MixKeyAndDeriveKey(ReadOnlySpan<byte> input, Span<byte> key) => DeriveKeys(input, chainingKey, key, default);

    /// <summary>
    /// (C, tau, <paramref name="key" />) = KDF3(C, <paramref name="presharedKey" />), then
    /// H = HASH(H || tau): the psk2 step of section 5.4.3.
    /// </summary>
    public void MixPresharedKey(ReadOnlySpan<byte> presharedKey, Span<byte> key)
    {
        Span<byte> tau = stackalloc byte[KeySize];
        DeriveKeys(presharedKey, chainingKey, tau, key);
        MixHash(tau);
        tau.Clear();
    }

    /// <summary>Seals <paramref name="plaintext" /> as AEAD(key, 0, plaintext, H), then H = HASH(H || ciphertext).</summary>
    public byte[] EncryptAndHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        var sealedMessage = new byte[plaintext.Length + AeadChaCha20Poly1305.TagSize];
        using (var aead = new AeadChaCha20Poly1305(key))
        {
            aead.Encrypt(stackalloc byte[AeadChaCha20Poly1305.NonceSize], plaintext, sealedMessage.AsSpan(0, plaintext.Length), sealedMessage.AsSpan(plaintext.Length), hash);
        }

        MixHash(sealedMessage);
        return sealedMessage;
    }

    /// <summary>
    /// Opens <paramref name="sealedMessage" /> (ciphertext then tag) into <paramref name="plaintext" />
    /// with H as the associated data, then H = HASH(H || sealedMessage) whether or not it opened.
    /// </summary>
    /// <returns><c>false</c> when the tag does not match.</returns>
    public bool TryDecryptAndHash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> sealedMessage, Span<byte> plaintext)
    {
        bool opened;
        using (var aead = new AeadChaCha20Poly1305(key))
        {
            opened = aead.TryDecrypt(stackalloc byte[AeadChaCha20Poly1305.NonceSize], sealedMessage[..^AeadChaCha20Poly1305.TagSize], sealedMessage[^AeadChaCha20Poly1305.TagSize..], plaintext, hash);
        }

        MixHash(sealedMessage);
        return opened;
    }

    /// <summary>
    /// (<paramref name="initiatorSendKey" />, <paramref name="responderSendKey" />) = KDF2(C, empty):
    /// section 5.4.5. C and H are zeroed after, as the handshake is over.
    /// </summary>
    public void DeriveTransportKeys(Span<byte> initiatorSendKey, Span<byte> responderSendKey)
    {
        DeriveKeys(default, initiatorSendKey, responderSendKey, default);
        Array.Clear(chainingKey);
        Array.Clear(hash);
    }

    /// <summary>
    /// HKDF over HMAC-BLAKE2s keyed by C (section 5.4): writes tau1, and tau2 and tau3 when their
    /// destinations are not empty. <paramref name="first" /> may be C itself.
    /// </summary>
    private void DeriveKeys(ReadOnlySpan<byte> input, Span<byte> first, Span<byte> second, Span<byte> third)
    {
        Span<byte> pseudorandomKey = stackalloc byte[KeySize];
        Span<byte> block = stackalloc byte[KeySize + 1];
        HmacBlake2s.HashData(chainingKey, input, pseudorandomKey);
        block[0] = 1;
        HmacBlake2s.HashData(pseudorandomKey, block[..1], block[..KeySize]);
        block[..KeySize].CopyTo(first);
        if (!second.IsEmpty)
        {
            block[KeySize] = 2;
            HmacBlake2s.HashData(pseudorandomKey, block, block[..KeySize]);
            block[..KeySize].CopyTo(second);
        }

        if (!third.IsEmpty)
        {
            block[KeySize] = 3;
            HmacBlake2s.HashData(pseudorandomKey, block, block[..KeySize]);
            block[..KeySize].CopyTo(third);
        }

        pseudorandomKey.Clear();
        block.Clear();
    }

    private static void HashConcatenated(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, Span<byte> destination)
    {
        using var blake = new Blake2s();
        blake.AppendData(first);
        blake.AppendData(second);
        blake.GetHashAndReset(destination);
    }
}
