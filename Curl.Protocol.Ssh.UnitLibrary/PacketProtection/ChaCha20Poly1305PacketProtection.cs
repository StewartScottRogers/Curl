using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// <c>chacha20-poly1305@openssh.com</c>, as OpenSSH's <c>PROTOCOL.chacha20poly1305</c>
/// specifies it: the 64-byte derived key is split into <c>K_2</c> (its first 32 bytes, for
/// the packet) and <c>K_1</c> (its last 32, for the length). The nonce is the packet's
/// sequence number as a 64-bit big-endian integer. <c>packet_length</c> is encrypted under
/// <c>K_1</c> from block 0; the one-time Poly1305 key is the first 32 bytes of block 0
/// under <c>K_2</c>; the rest of the packet is encrypted under <c>K_2</c> from block 1; and
/// the 16-byte tag covers the encrypted length and the encrypted rest. No MAC is negotiated
/// beside it. <see cref="ChaCha20" /> and <see cref="Poly1305" /> from
/// <c>Curl.Cryptography.UnitLibrary</c> do the work (ADR-0259).
/// </summary>
internal sealed class ChaCha20Poly1305PacketProtection : ISshPacketProtection
{
    /// <summary>The length of the derived key: <c>K_2</c> then <c>K_1</c>.</summary>
    internal const int KeyLength = 2 * ChaCha20.KeySize;

    private readonly byte[] payloadKey;

    private readonly byte[] lengthKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChaCha20Poly1305PacketProtection" /> class.
    /// </summary>
    /// <param name="key">The derived encryption key: <see cref="KeyLength" /> bytes.</param>
    internal ChaCha20Poly1305PacketProtection(byte[] key)
    {
        payloadKey = key[..ChaCha20.KeySize];
        lengthKey = key[ChaCha20.KeySize..KeyLength];
    }

    /// <inheritdoc />
    public int BlockSize => 8;

    /// <inheritdoc />
    public bool PadsPacketLengthField => false;

    /// <inheritdoc />
    public int LengthBlockLength => sizeof(uint);

    /// <inheritdoc />
    public int TagLength => Poly1305.TagSize;

    /// <inheritdoc />
    public byte[] Seal(uint sequenceNumber, ReadOnlySpan<byte> packet)
    {
        byte[] nonce = NonceFor(sequenceNumber);
        byte[] sealedPacket = new byte[packet.Length + Poly1305.TagSize];
        ChaCha20.ApplyKeyStream(lengthKey, nonce, 0, packet[..sizeof(uint)], sealedPacket.AsSpan(0, sizeof(uint)));
        ChaCha20.ApplyKeyStream(payloadKey, nonce, 1, packet[sizeof(uint)..], sealedPacket.AsSpan(sizeof(uint), packet.Length - sizeof(uint)));
        byte[] poly1305Key = Poly1305KeyFor(nonce);
        Poly1305.ComputeTag(poly1305Key, sealedPacket.AsSpan(0, packet.Length), sealedPacket.AsSpan(packet.Length));
        CryptographicOperations.ZeroMemory(poly1305Key);
        return sealedPacket;
    }

    /// <inheritdoc />
    public uint DecryptPacketLength(uint sequenceNumber, byte[] lengthBlock)
    {
        Span<byte> length = stackalloc byte[sizeof(uint)];
        ChaCha20.ApplyKeyStream(lengthKey, NonceFor(sequenceNumber), 0, lengthBlock, length);
        return BinaryPrimitives.ReadUInt32BigEndian(length);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="lengthBlock" /> is left as received: the tag covers the encrypted
    /// length, and <see cref="DecryptPacketLength" /> decrypts it into a copy.
    /// </remarks>
    public byte[] Open(uint sequenceNumber, byte[] lengthBlock, byte[] remainder)
    {
        byte[] nonce = NonceFor(sequenceNumber);
        int ciphertextLength = remainder.Length - Poly1305.TagSize;
        byte[] poly1305Key = Poly1305KeyFor(nonce);
        bool authentic = Poly1305.Verify(poly1305Key, [.. lengthBlock, .. remainder.AsSpan(0, ciphertextLength)], remainder.AsSpan(ciphertextLength));
        CryptographicOperations.ZeroMemory(poly1305Key);
        if (!authentic)
        {
            throw new SshPacketAuthenticationException(Libssh2ErrorCode.Decrypt);
        }

        byte[] plaintext = new byte[ciphertextLength];
        ChaCha20.ApplyKeyStream(payloadKey, nonce, 1, remainder.AsSpan(0, ciphertextLength), plaintext);
        return plaintext;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(payloadKey);
        CryptographicOperations.ZeroMemory(lengthKey);
    }

    private static byte[] NonceFor(uint sequenceNumber)
    {
        byte[] nonce = new byte[ChaCha20.OriginalNonceSize];
        BinaryPrimitives.WriteUInt64BigEndian(nonce, sequenceNumber);
        return nonce;
    }

    private byte[] Poly1305KeyFor(byte[] nonce)
    {
        byte[] block = new byte[ChaCha20.BlockSize];
        ChaCha20.ComputeBlock(payloadKey, nonce, 0, block);
        byte[] poly1305Key = block[..Poly1305.KeySize];
        CryptographicOperations.ZeroMemory(block);
        return poly1305Key;
    }
}
