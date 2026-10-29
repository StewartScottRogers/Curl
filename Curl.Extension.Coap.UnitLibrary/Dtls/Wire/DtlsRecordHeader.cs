using System.Buffers.Binary;
using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Wire;

/// <summary>
/// The 13-byte header of a DTLS record (RFC 6347 section 4.1): content type, protocol version,
/// epoch, 48-bit sequence number and the length of the fragment that follows.
/// </summary>
/// <param name="ContentType">The record's content type.</param>
/// <param name="Version">The protocol version on the wire; DTLS 1.2 is <see cref="Dtls12Version" />.</param>
/// <param name="Epoch">The epoch, incremented on every cipher state change.</param>
/// <param name="SequenceNumber">The 48-bit sequence number within the epoch.</param>
/// <param name="Length">The length of the record's fragment, at most <see cref="MaximumLength" />.</param>
internal readonly record struct DtlsRecordHeader(TlsContentType ContentType, ushort Version, ushort Epoch, ulong SequenceNumber, ushort Length)
{
    /// <summary>The header's size in bytes.</summary>
    public const int HeaderLength = 13;

    /// <summary>DTLS 1.2's version, {254, 253}, the one's complement of 1.2.</summary>
    public const ushort Dtls12Version = 0xFEFD;

    /// <summary>The largest 48-bit sequence number.</summary>
    public const ulong MaximumSequenceNumber = (1UL << 48) - 1;

    /// <summary>The largest fragment length: 2^14 plus 2048 bytes of expansion (RFC 5246 section 6.2.3).</summary>
    public const int MaximumLength = (1 << 14) + 2048;

    /// <summary>
    /// The 64-bit sequence number the record's MAC or AEAD covers: the epoch followed by the
    /// 48-bit sequence number (RFC 6347 section 4.1.2.1).
    /// </summary>
    public ulong EpochAndSequenceNumber => ((ulong)Epoch << 48) | SequenceNumber;

    /// <summary>Writes the 13 header bytes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The sequence number is past 2^48 - 1 or the length past <see cref="MaximumLength" />.</exception>
    public byte[] Encode()
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(SequenceNumber, MaximumSequenceNumber, nameof(SequenceNumber));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Length, (ushort)MaximumLength, nameof(Length));
        byte[] header = new byte[HeaderLength];
        header[0] = (byte)ContentType;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(1), Version);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(3), EpochAndSequenceNumber);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(11), Length);
        return header;
    }

    /// <summary>
    /// The additional data an AEAD cipher authenticates for this record (RFC 5246 section
    /// 6.2.3.3 as RFC 6347 section 4.1.2.1 amends it): the 64-bit epoch and sequence number,
    /// the content type, the version and the plaintext's length.
    /// </summary>
    /// <param name="plaintextLength">The length of the record's plaintext, not its ciphertext.</param>
    public byte[] AdditionalData(ushort plaintextLength)
    {
        byte[] additionalData = new byte[HeaderLength];
        BinaryPrimitives.WriteUInt64BigEndian(additionalData, EpochAndSequenceNumber);
        additionalData[8] = (byte)ContentType;
        BinaryPrimitives.WriteUInt16BigEndian(additionalData.AsSpan(9), Version);
        BinaryPrimitives.WriteUInt16BigEndian(additionalData.AsSpan(11), plaintextLength);
        return additionalData;
    }

    /// <summary>Reads the header at the start of <paramref name="datagram" />.</summary>
    /// <exception cref="FormatException">
    /// The datagram is shorter than the header, the length is past <see cref="MaximumLength" />, or
    /// the fragment the length names runs past the end of the datagram.
    /// </exception>
    public static DtlsRecordHeader Decode(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < HeaderLength)
        {
            throw new FormatException($"A DTLS record of {datagram.Length} bytes is shorter than its 13-byte header.");
        }

        ulong epochAndSequenceNumber = BinaryPrimitives.ReadUInt64BigEndian(datagram[3..]);
        ushort length = BinaryPrimitives.ReadUInt16BigEndian(datagram[11..]);
        if (length > MaximumLength || length > datagram.Length - HeaderLength)
        {
            throw new FormatException($"A DTLS record length of {length} runs past the record or past {MaximumLength} bytes.");
        }

        return new DtlsRecordHeader(
            (TlsContentType)datagram[0],
            BinaryPrimitives.ReadUInt16BigEndian(datagram[1..]),
            (ushort)(epochAndSequenceNumber >> 48),
            epochAndSequenceNumber & MaximumSequenceNumber,
            length);
    }
}
