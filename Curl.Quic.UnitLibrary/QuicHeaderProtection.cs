using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// Header protection under one header protection key (RFC 9001 section 5.4): a mask
/// computed from 16 bytes of the protected payload, sampled four bytes after the start of
/// the packet number, hides the low bits of the first byte (four in a long header, five
/// in a short one) and the packet number. The key survives key updates.
/// </summary>
public sealed class QuicHeaderProtection : IDisposable
{
    /// <summary>The length of the sample the mask is computed from.</summary>
    public const int SampleLength = 16;

    /// <summary>The length of the mask: one byte for the first byte and up to four for the packet number.</summary>
    public const int MaskLength = 5;

    /// <summary>How far after the start of the packet number the sample begins, as if the packet number were four bytes.</summary>
    public const int SampleOffset = 4;

    private const byte LongHeaderBit = 0x80;

    private readonly IQuicHeaderProtectionMask mask;

    private QuicHeaderProtection(IQuicHeaderProtectionMask mask) => this.mask = mask;

    /// <summary>Creates the header protection of <paramref name="cipherSuite" /> under <paramref name="headerProtectionKey" />: AES-ECB for the AES suites, ChaCha20 for ChaCha20-Poly1305.</summary>
    /// <param name="cipherSuite">The suite; <see cref="QuicPacketProtection.CanProtect" /> must accept it.</param>
    /// <param name="headerProtectionKey">The <c>quic hp</c> key.</param>
    /// <returns>The header protection.</returns>
    /// <exception cref="ArgumentException">The suite is not one QUIC packets can be protected with here.</exception>
    public static QuicHeaderProtection Create(Tls13CipherSuite cipherSuite, byte[] headerProtectionKey)
    {
        QuicPacketProtection.RequireProtectable(cipherSuite);
        ArgumentNullException.ThrowIfNull(headerProtectionKey);
        IQuicHeaderProtectionMask mask = cipherSuite.Code == Tls13CipherSuite.ChaCha20Poly1305Sha256.Code
            ? new ChaCha20QuicHeaderProtectionMask(headerProtectionKey)
            : new AesQuicHeaderProtectionMask(headerProtectionKey);
        return new QuicHeaderProtection(mask);
    }

    /// <summary>Returns the five-byte mask of a sample.</summary>
    /// <param name="sample">16 bytes of protected payload.</param>
    /// <returns>The mask.</returns>
    /// <exception cref="ArgumentException"><paramref name="sample" /> is not <see cref="SampleLength" /> bytes.</exception>
    public byte[] ComputeMask(ReadOnlySpan<byte> sample)
    {
        if (sample.Length != SampleLength)
        {
            throw new ArgumentException($"A header protection sample is {SampleLength} bytes, not {sample.Length}.", nameof(sample));
        }

        var result = new byte[MaskLength];
        mask.ComputeMask(sample, result);
        return result;
    }

    /// <summary>Returns whether a packet whose packet number starts at <paramref name="packetNumberOffset" /> and ends at <paramref name="packetLength" /> leaves room for the sample.</summary>
    /// <param name="packetNumberOffset">Where the packet number starts.</param>
    /// <param name="packetLength">The length of the packet.</param>
    /// <returns>Whether header protection can be applied or removed.</returns>
    public static bool HasRoomForSample(int packetNumberOffset, int packetLength) =>
        packetNumberOffset + SampleOffset + SampleLength <= packetLength;

    /// <summary>Applies header protection to a packet whose payload is already protected, in place.</summary>
    /// <param name="packet">The packet, from its first byte to its end, with the packet number and first byte in the clear.</param>
    /// <param name="packetNumberOffset">Where the packet number starts.</param>
    public void Apply(Span<byte> packet, int packetNumberOffset)
    {
        int packetNumberLength = (packet[0] & 0x03) + 1;
        Span<byte> packetMask = stackalloc byte[MaskLength];
        mask.ComputeMask(packet.Slice(packetNumberOffset + SampleOffset, SampleLength), packetMask);
        MaskPacketNumber(packet.Slice(packetNumberOffset, packetNumberLength), packetMask);
        packet[0] ^= (byte)(packetMask[0] & FirstByteMaskBits(packet[0]));
    }

    /// <summary>Removes header protection from a packet in place.</summary>
    /// <param name="packet">The packet, from its first byte to its end, as received.</param>
    /// <param name="packetNumberOffset">Where the packet number starts.</param>
    /// <returns>The packet number length the unprotected first byte gives, 1 to 4.</returns>
    public int Remove(Span<byte> packet, int packetNumberOffset)
    {
        Span<byte> packetMask = stackalloc byte[MaskLength];
        mask.ComputeMask(packet.Slice(packetNumberOffset + SampleOffset, SampleLength), packetMask);
        packet[0] ^= (byte)(packetMask[0] & FirstByteMaskBits(packet[0]));
        int packetNumberLength = (packet[0] & 0x03) + 1;
        MaskPacketNumber(packet.Slice(packetNumberOffset, packetNumberLength), packetMask);
        return packetNumberLength;
    }

    /// <inheritdoc />
    public void Dispose() => mask.Dispose();

    private static void MaskPacketNumber(Span<byte> packetNumber, ReadOnlySpan<byte> packetMask)
    {
        for (int index = 0; index < packetNumber.Length; index++)
        {
            packetNumber[index] ^= packetMask[1 + index];
        }
    }

    private static int FirstByteMaskBits(byte firstByte) => (firstByte & LongHeaderBit) != 0 ? 0x0f : 0x1f;
}
