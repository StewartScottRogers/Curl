using System.Buffers.Binary;
using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// Payload protection under one AEAD key and IV (RFC 9001 section 5.3): the nonce is the
/// IV XORed with the packet number, left-padded to 12 bytes, and the associated data is
/// the unprotected header up to the end of the packet number.
/// </summary>
internal sealed class QuicPayloadProtection : IDisposable
{
    private readonly IQuicPacketAead aead;

    private readonly byte[] iv;

    private QuicPayloadProtection(IQuicPacketAead aead, byte[] iv)
    {
        this.aead = aead;
        this.iv = iv;
    }

    /// <summary>Creates the payload protection of <paramref name="keys" /> under <paramref name="cipherSuite" />'s AEAD.</summary>
    public static QuicPayloadProtection Create(Tls13CipherSuite cipherSuite, QuicPacketKeys keys)
    {
        IQuicPacketAead aead = cipherSuite.Code == Tls13CipherSuite.ChaCha20Poly1305Sha256.Code
            ? new ChaCha20Poly1305QuicPacketAead(keys.Key)
            : new AesGcmQuicPacketAead(keys.Key);
        return new QuicPayloadProtection(aead, keys.Iv);
    }

    /// <summary>Returns the nonce of packet number <paramref name="packetNumber" />.</summary>
    public byte[] ComputeNonce(ulong packetNumber)
    {
        var nonce = (byte[])iv.Clone();
        Span<byte> tail = nonce.AsSpan(QuicPacketKeys.IvLength - sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(tail, BinaryPrimitives.ReadUInt64BigEndian(tail) ^ packetNumber);
        return nonce;
    }

    /// <summary>Encrypts <paramref name="plaintext" /> into <paramref name="destination" />: the ciphertext, then the tag.</summary>
    public void Encrypt(ulong packetNumber, ReadOnlySpan<byte> header, ReadOnlySpan<byte> plaintext, Span<byte> destination) =>
        aead.Encrypt(ComputeNonce(packetNumber), plaintext, destination[..plaintext.Length], destination[plaintext.Length..], header);

    /// <summary>Decrypts <paramref name="ciphertextAndTag" /> into <paramref name="plaintext" /> when its tag matches.</summary>
    public bool TryDecrypt(ulong packetNumber, ReadOnlySpan<byte> header, ReadOnlySpan<byte> ciphertextAndTag, Span<byte> plaintext)
    {
        int ciphertextLength = ciphertextAndTag.Length - QuicPacketProtection.TagLength;
        return aead.TryDecrypt(ComputeNonce(packetNumber), ciphertextAndTag[..ciphertextLength], ciphertextAndTag[ciphertextLength..], plaintext, header);
    }

    public void Dispose() => aead.Dispose();
}
