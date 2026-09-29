using System.Buffers.Binary;

namespace Curl.Protocol.Smb;

/// <summary>
/// The SMB_COM_READ_ANDX request curl 8.21.0 sends for each piece of the file
/// (<c>smb_send_read</c>): twelve parameter words asking for exactly
/// <see cref="MaxPayloadSize" /> bytes at a 64-bit offset, and no bytes.
/// </summary>
internal static class SmbReadRequest
{
    /// <summary>curl's <c>MAX_PAYLOAD_SIZE</c>: the bytes each read asks for.</summary>
    public const int MaxPayloadSize = 0x8000;

    // Word count, AndX (4), FID (2), offset (4), max count (2), min count (2), timeout (4),
    // remaining (2), offset high (4), byte count (2).
    private const int ParametersLength = 27;

    // SMB_WC_READ_ANDX.
    private const byte WordCount = 0x0c;

    private const int FileIdOffset = 5;
    private const int OffsetOffset = 7;
    private const int MaxCountOffset = 11;
    private const int MinCountOffset = 13;
    private const int OffsetHighOffset = 21;

    /// <summary>Encodes the request, NetBIOS header first.</summary>
    /// <param name="userId">The UID the session setup response assigned.</param>
    /// <param name="treeId">The TID the tree connect response assigned.</param>
    /// <param name="fileId">The FID the open response assigned.</param>
    /// <param name="offset">Where in the file to read from.</param>
    /// <returns>The bytes to send.</returns>
    public static byte[] Encode(ushort userId, ushort treeId, ushort fileId, long offset)
    {
        Span<byte> parameters = stackalloc byte[ParametersLength];
        parameters.Clear();
        parameters[0] = WordCount;
        parameters[1] = SmbMessageHeader.NoAndXCommand;
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[FileIdOffset..], fileId);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters[OffsetOffset..], (uint)offset);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[MaxCountOffset..], MaxPayloadSize);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[MinCountOffset..], MaxPayloadSize);
        BinaryPrimitives.WriteUInt32LittleEndian(parameters[OffsetHighOffset..], (uint)(offset >> 32));
        return SmbMessageHeader.Frame(SmbMessageHeader.ReadAndXCommand, userId, treeId, parameters);
    }
}
