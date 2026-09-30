using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// One direction's MAC (RFC 4253 section 6.4): an HMAC keyed with the derived integrity
/// key (an <see cref="ISshHmac" />) over the packet's <c>uint32</c> sequence number and then the packet, cut to
/// <see cref="Length" /> bytes. Under the <c>-etm@openssh.com</c> forms (OpenSSH's
/// <c>PROTOCOL</c>, section 1.7) the packet it covers is the encrypted one.
/// </summary>
internal sealed class SshMac : IDisposable
{
    private readonly ISshHmac hmac;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshMac" /> class over a BCL hash.
    /// </summary>
    /// <param name="hash">The HMAC's hash.</param>
    /// <param name="key">The derived integrity key.</param>
    /// <param name="length">How many bytes of the HMAC are sent.</param>
    /// <param name="isEncryptThenMac">Whether the MAC covers the encrypted packet.</param>
    internal SshMac(HashAlgorithmName hash, byte[] key, int length, bool isEncryptThenMac)
        : this(new BclSshHmac(hash, key), length, isEncryptThenMac)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SshMac" /> class.
    /// </summary>
    /// <param name="hmac">The keyed HMAC, which the MAC disposes.</param>
    /// <param name="length">How many bytes of the HMAC are sent.</param>
    /// <param name="isEncryptThenMac">Whether the MAC covers the encrypted packet.</param>
    internal SshMac(ISshHmac hmac, int length, bool isEncryptThenMac)
    {
        this.hmac = hmac;
        Length = length;
        IsEncryptThenMac = isEncryptThenMac;
    }

    /// <summary>Gets how many MAC bytes follow each packet.</summary>
    internal int Length { get; }

    /// <summary>
    /// Gets a value indicating whether the MAC covers the encrypted packet, whose length
    /// then travels unencrypted, rather than the unencrypted one.
    /// </summary>
    internal bool IsEncryptThenMac { get; }

    /// <summary>
    /// Computes the MAC of one packet.
    /// </summary>
    /// <param name="sequenceNumber">The packet's sequence number.</param>
    /// <param name="packetStart">The packet's first bytes.</param>
    /// <param name="packetRest">The rest of the packet.</param>
    /// <returns>The <see cref="Length" /> MAC bytes.</returns>
    internal byte[] Compute(uint sequenceNumber, ReadOnlySpan<byte> packetStart, ReadOnlySpan<byte> packetRest)
    {
        Span<byte> sequence = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(sequence, sequenceNumber);
        hmac.AppendData(sequence);
        hmac.AppendData(packetStart);
        hmac.AppendData(packetRest);
        return hmac.GetHashAndReset()[..Length];
    }

    /// <summary>
    /// Checks a received MAC in constant time.
    /// </summary>
    /// <param name="sequenceNumber">The packet's sequence number.</param>
    /// <param name="packetStart">The packet's first bytes.</param>
    /// <param name="packetRest">The rest of the packet.</param>
    /// <param name="received">The MAC that came with it.</param>
    /// <exception cref="SshPacketAuthenticationException">The MAC does not match.</exception>
    internal void Verify(uint sequenceNumber, ReadOnlySpan<byte> packetStart, ReadOnlySpan<byte> packetRest, ReadOnlySpan<byte> received)
    {
        if (!CryptographicOperations.FixedTimeEquals(Compute(sequenceNumber, packetStart, packetRest), received))
        {
            throw new SshPacketAuthenticationException(Libssh2ErrorCode.InvalidMac);
        }
    }

    /// <inheritdoc />
    public void Dispose() => hmac.Dispose();
}
