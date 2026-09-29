using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// <c>aes128-gcm@openssh.com</c> and <c>aes256-gcm@openssh.com</c> (RFC 5647, as OpenSSH's
/// <c>PROTOCOL</c> section 1.6 amends it): the <c>packet_length</c> travels unencrypted as
/// the associated data, the rest of the packet is encrypted with AES-GCM under a 16-byte
/// tag, and no MAC is negotiated. The 12-byte nonce is the derived IV's four-byte fixed
/// field and an eight-byte invocation counter that starts at the IV's last eight bytes and
/// rises by one per packet, wrapping. The BCL's <see cref="AesGcm" /> does the work
/// (ADR-0118: supported on Windows, Linux and macOS with a 16-byte tag).
/// </summary>
internal sealed class AesGcmPacketProtection : ISshPacketProtection
{
    /// <summary>The length of the derived IV and of the nonce.</summary>
    internal const int NonceLength = 12;

    private const int AuthenticationTagLength = 16;

    private const int FixedFieldLength = 4;

    private readonly AesGcm aesGcm;

    private readonly byte[] nonce;

    /// <summary>
    /// Initializes a new instance of the <see cref="AesGcmPacketProtection" /> class.
    /// </summary>
    /// <param name="key">The derived encryption key: 16 or 32 bytes.</param>
    /// <param name="initialNonce">The derived IV: <see cref="NonceLength" /> bytes.</param>
    internal AesGcmPacketProtection(byte[] key, byte[] initialNonce)
    {
        aesGcm = new AesGcm(key, AuthenticationTagLength);
        nonce = [.. initialNonce];
    }

    /// <inheritdoc />
    public int BlockSize => 16;

    /// <inheritdoc />
    public bool PadsPacketLengthField => false;

    /// <inheritdoc />
    public int LengthBlockLength => sizeof(uint);

    /// <inheritdoc />
    public int TagLength => AuthenticationTagLength;

    /// <inheritdoc />
    public byte[] Seal(uint sequenceNumber, ReadOnlySpan<byte> packet)
    {
        byte[] sealedPacket = new byte[packet.Length + AuthenticationTagLength];
        packet[..sizeof(uint)].CopyTo(sealedPacket);
        aesGcm.Encrypt(
            nonce,
            packet[sizeof(uint)..],
            sealedPacket.AsSpan(sizeof(uint), packet.Length - sizeof(uint)),
            sealedPacket.AsSpan(packet.Length),
            packet[..sizeof(uint)]);
        AdvanceInvocationCounter();
        return sealedPacket;
    }

    /// <inheritdoc />
    public uint DecryptPacketLength(uint sequenceNumber, byte[] lengthBlock) =>
        BinaryPrimitives.ReadUInt32BigEndian(lengthBlock);

    /// <inheritdoc />
    public byte[] Open(uint sequenceNumber, byte[] lengthBlock, byte[] remainder)
    {
        int ciphertextLength = remainder.Length - AuthenticationTagLength;
        byte[] plaintext = new byte[ciphertextLength];
        try
        {
            aesGcm.Decrypt(nonce, remainder.AsSpan(0, ciphertextLength), remainder.AsSpan(ciphertextLength), plaintext, lengthBlock);
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new SshPacketAuthenticationException(Libssh2ErrorCode.Decrypt);
        }

        AdvanceInvocationCounter();
        return plaintext;
    }

    /// <inheritdoc />
    public void Dispose() => aesGcm.Dispose();

    private void AdvanceInvocationCounter()
    {
        Span<byte> counter = nonce.AsSpan(FixedFieldLength);
        BinaryPrimitives.WriteUInt64BigEndian(counter, unchecked(BinaryPrimitives.ReadUInt64BigEndian(counter) + 1));
    }
}
