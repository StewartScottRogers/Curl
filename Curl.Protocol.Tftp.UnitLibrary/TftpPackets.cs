using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Builds and reads the TFTP packets a download uses: the read request and
/// acknowledgement it sends (RFC 1350 section 5), and the block size an option
/// acknowledgement carries (RFC 2347, RFC 2348).
/// </summary>
internal static class TftpPackets
{
    /// <summary>The opcode of a read request.</summary>
    internal const ushort ReadRequestOpcode = 1;

    /// <summary>The opcode of a DATA packet.</summary>
    internal const ushort DataOpcode = 3;

    /// <summary>The opcode of an acknowledgement.</summary>
    internal const ushort AcknowledgementOpcode = 4;

    /// <summary>The opcode of an ERROR packet.</summary>
    internal const ushort ErrorOpcode = 5;

    /// <summary>The opcode of an option acknowledgement.</summary>
    internal const ushort OptionAcknowledgementOpcode = 6;

    /// <summary>
    /// The length of the opcode and block number that precede a DATA packet's payload.
    /// </summary>
    internal const int DataHeaderLength = 4;

    /// <summary>The block size RFC 1350 fixes when no option says otherwise.</summary>
    internal const int DefaultBlockSize = 512;

    /// <summary>The smallest block size RFC 2348 allows.</summary>
    internal const int MinimumBlockSize = 8;

    /// <summary>The largest block size RFC 2348 allows.</summary>
    internal const int MaximumBlockSize = 65464;

    /// <summary>
    /// The <c>timeout</c> option value curl 8.21.0 sends when no timeout option was given,
    /// measured on the wire.
    /// </summary>
    private const string DefaultTimeoutSeconds = "6";

    /// <summary>
    /// Builds the read request curl 8.21.0 sends by default: octet mode, then
    /// <c>tsize 0</c>, <c>blksize 512</c> and <c>timeout 6</c>, each string
    /// null-terminated.
    /// </summary>
    /// <param name="fileName">The file name from the URL path, decoded.</param>
    /// <returns>The whole datagram.</returns>
    internal static byte[] BuildReadRequest(string fileName)
    {
        string[] fields =
        [
            fileName,
            "octet",
            "tsize",
            "0",
            "blksize",
            DefaultBlockSize.ToString(CultureInfo.InvariantCulture),
            "timeout",
            DefaultTimeoutSeconds,
        ];

        using var packet = new MemoryStream();
        packet.WriteByte(0);
        packet.WriteByte((byte)ReadRequestOpcode);
        foreach (var field in fields)
        {
            packet.Write(Encoding.UTF8.GetBytes(field));
            packet.WriteByte(0);
        }

        return packet.ToArray();
    }

    /// <summary>
    /// Builds the acknowledgement of <paramref name="blockNumber" />.
    /// </summary>
    /// <param name="blockNumber">The block acknowledged; 0 acknowledges an OACK.</param>
    /// <returns>The four-byte datagram.</returns>
    internal static byte[] BuildAcknowledgement(ushort blockNumber)
    {
        var packet = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(packet, AcknowledgementOpcode);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), blockNumber);
        return packet;
    }

    /// <summary>
    /// Reads the 16-bit big-endian field at <paramref name="offset" />: the opcode at 0,
    /// the block number or error code at 2.
    /// </summary>
    /// <param name="packet">The received datagram, at least four bytes long.</param>
    /// <param name="offset">The byte offset of the field.</param>
    /// <returns>The field's value.</returns>
    internal static ushort ReadField(ReadOnlySpan<byte> packet, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(packet[offset..]);

    /// <summary>
    /// Reads the block size an option acknowledgement grants.
    /// </summary>
    /// <param name="options">
    /// The OACK's body after its opcode: null-terminated name and value pairs.
    /// </param>
    /// <returns>
    /// The <c>blksize</c> value when it is present and within 8 to 65464, otherwise
    /// <see cref="DefaultBlockSize" />.
    /// </returns>
    internal static int ReadAcknowledgedBlockSize(ReadOnlySpan<byte> options)
    {
        var fields = Encoding.UTF8.GetString(options).Split('\0');
        for (var index = 0; index + 1 < fields.Length; index += 2)
        {
            if (string.Equals(fields[index], "blksize", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(fields[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var blockSize)
                && blockSize is >= MinimumBlockSize and <= MaximumBlockSize)
            {
                return blockSize;
            }
        }

        return DefaultBlockSize;
    }
}
