using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>Test helpers: split records back apart, and make keys of the lengths a suite needs.</summary>
internal static class Tls12Records
{
    public static List<(TlsContentType ContentType, TlsProtocolVersion Version, byte[] Fragment)> Split(byte[] records)
    {
        List<(TlsContentType, TlsProtocolVersion, byte[])> split = [];
        int offset = 0;
        while (offset < records.Length)
        {
            int length = BinaryPrimitives.ReadUInt16BigEndian(records.AsSpan(offset + 3));
            split.Add(((TlsContentType)records[offset], (TlsProtocolVersion)BinaryPrimitives.ReadUInt16BigEndian(records.AsSpan(offset + 1)), records[(offset + 5)..(offset + 5 + length)]));
            offset += 5 + length;
        }

        return split;
    }

    public static Tls12WriteKeys RandomKeys(Tls12RecordProtectionParameters parameters) =>
        new(
            RandomNumberGenerator.GetBytes(parameters.MacKeyLength),
            RandomNumberGenerator.GetBytes(parameters.KeyLength),
            RandomNumberGenerator.GetBytes(parameters.FixedIvLength));

    public static ReplayTlsRandomSource Replay(params byte[][] fills) => new(fills, []);

    /// <summary>The 13-byte header the MAC and the AEAD additional data cover.</summary>
    public static byte[] AdditionalData(ulong sequenceNumber, TlsContentType contentType, TlsProtocolVersion version, int length)
    {
        byte[] header = new byte[13];
        BinaryPrimitives.WriteUInt64BigEndian(header, sequenceNumber);
        header[8] = (byte)contentType;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(9), (ushort)version);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(11), (ushort)length);
        return header;
    }
}
