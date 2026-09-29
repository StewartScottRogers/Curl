using System.Buffers.Binary;

namespace Curl.Protocol.Smb;

/// <summary>
/// The SMB_COM_WRITE_ANDX request curl 8.21.0 sends for each piece of an upload
/// (<c>smb_send_write</c>): fourteen parameter words naming the FID, a 64-bit offset and
/// the data's length, then a byte of padding and at most <see cref="MaxDataLength" /> bytes
/// of the file.
/// </summary>
internal static class SmbWriteRequest
{
    /// <summary>curl's <c>MAX_PAYLOAD_SIZE - 1</c>: the most bytes one write carries, one byte being padding.</summary>
    public const int MaxDataLength = 0x7fff;

    // Word count, AndX (4), FID (2), offset (4), timeout (4), write mode (2), remaining (2),
    // pad (2), data length (2), data offset (2), offset high (4), byte count (2), pad.
    private const int ParametersLength = 32;

    // SMB_WC_WRITE_ANDX.
    private const byte WordCount = 0x0e;

    private const int FileIdOffset = 5;
    private const int OffsetOffset = 7;
    private const int DataLengthOffset = 21;
    private const int DataOffsetOffset = 23;
    private const int OffsetHighOffset = 25;
    private const int ByteCountOffset = 29;

    // sizeof(struct smb_write) - sizeof(unsigned int): where the data starts, counted from
    // the SMB header.
    private const ushort DataOffset = 0x40;

    /// <summary>
    /// Encodes the request, NetBIOS header first. Its lengths declare
    /// <paramref name="dataLength" /> bytes of data even when <paramref name="data" /> holds
    /// fewer, as curl's do when the source ends before the size it gave.
    /// </summary>
    /// <param name="userId">The UID the session setup response assigned.</param>
    /// <param name="treeId">The TID the tree connect response assigned.</param>
    /// <param name="fileId">The FID the open response assigned.</param>
    /// <param name="offset">Where in the file the data goes.</param>
    /// <param name="dataLength">The bytes of data the request declares, at most <see cref="MaxDataLength" />.</param>
    /// <param name="data">The bytes of data sent, at most <paramref name="dataLength" />.</param>
    /// <returns>The bytes to send.</returns>
    public static byte[] Encode(ushort userId, ushort treeId, ushort fileId, long offset, int dataLength, ReadOnlySpan<byte> data)
    {
        var message = new byte[SmbMessageHeader.Length + ParametersLength + data.Length];
        SmbMessageHeader.Write(message, SmbMessageHeader.WriteAndXCommand, userId, treeId, ParametersLength + dataLength);
        Span<byte> parameters = message.AsSpan(SmbMessageHeader.Length, ParametersLength);
        parameters[0] = WordCount;
        parameters[1] = SmbMessageHeader.NoAndXCommand;
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[FileIdOffset..], fileId);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters[OffsetOffset..], (uint)offset);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[DataLengthOffset..], (ushort)dataLength);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[DataOffsetOffset..], DataOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters[OffsetHighOffset..], (uint)(offset >> 32));
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[ByteCountOffset..], (ushort)(dataLength + 1));
        data.CopyTo(message.AsSpan(SmbMessageHeader.Length + ParametersLength));
        return message;
    }
}
