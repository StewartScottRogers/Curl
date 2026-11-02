using System.Buffers.Binary;

namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// One DTLS 1.2 record (RFC 6347 section 4.1): content type, version, epoch, 48-bit sequence number and length,
/// then the fragment.
/// </summary>
/// <param name="ContentType">The content type: 20 ChangeCipherSpec, 21 Alert, 22 Handshake, 23 application data.</param>
/// <param name="Epoch">The epoch, 0 before the ChangeCipherSpec and 1 after it.</param>
/// <param name="SequenceNumber">The record's sequence number within its epoch.</param>
/// <param name="Fragment">The fragment, protected in epoch 1.</param>
public sealed record DtlsReferenceRecord(byte ContentType, ushort Epoch, ulong SequenceNumber, byte[] Fragment)
{
    /// <summary>The ChangeCipherSpec content type.</summary>
    public const byte ChangeCipherSpec = 20;

    /// <summary>The Handshake content type.</summary>
    public const byte Handshake = 22;

    /// <summary>The application data content type.</summary>
    public const byte ApplicationData = 23;

    /// <summary>The length of the record header.</summary>
    public const int HeaderLength = 13;

    /// <summary>DTLS 1.2's version on the wire, {254, 253}.</summary>
    public const ushort Dtls12Version = 0xFEFD;

    /// <summary>Splits a datagram into the records it carries, one after another (section 4.1.1).</summary>
    /// <param name="datagram">The datagram.</param>
    /// <returns>The records, in order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A record runs past the end of the datagram.</exception>
    public static List<DtlsReferenceRecord> Split(ReadOnlySpan<byte> datagram)
    {
        List<DtlsReferenceRecord> records = [];
        int offset = 0;
        while (offset < datagram.Length)
        {
            ReadOnlySpan<byte> header = datagram.Slice(offset, HeaderLength);
            int length = BinaryPrimitives.ReadUInt16BigEndian(header[11..]);
            ulong epochAndSequence = BinaryPrimitives.ReadUInt64BigEndian(header[3..]);
            records.Add(new DtlsReferenceRecord(header[0], (ushort)(epochAndSequence >> 48), epochAndSequence & 0xFFFF_FFFF_FFFF, datagram.Slice(offset + HeaderLength, length).ToArray()));
            offset += HeaderLength + length;
        }

        return records;
    }

    /// <summary>Writes the record: its header, then its fragment.</summary>
    /// <returns>The record's bytes.</returns>
    public byte[] Encode()
    {
        byte[] record = new byte[HeaderLength + Fragment.Length];
        record[0] = ContentType;
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(1), Dtls12Version);
        BinaryPrimitives.WriteUInt64BigEndian(record.AsSpan(3), ((ulong)Epoch << 48) | SequenceNumber);
        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(11), (ushort)Fragment.Length);
        Fragment.CopyTo(record, HeaderLength);
        return record;
    }
}
