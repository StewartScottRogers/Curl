using System.Buffers.Binary;

namespace Curl.Protocol.Smb;

/// <summary>
/// The SMB_COM_CLOSE request curl 8.21.0 sends once the file is read (<c>smb_send_close</c>):
/// three parameter words, the FID and a zero last-write time, and no bytes.
/// </summary>
internal static class SmbCloseRequest
{
    // Word count, FID (2), last write time (4), byte count (2).
    private const int ParametersLength = 9;

    // SMB_WC_CLOSE.
    private const byte WordCount = 0x03;

    /// <summary>Encodes the request, NetBIOS header first.</summary>
    /// <param name="userId">The UID the session setup response assigned.</param>
    /// <param name="treeId">The TID the tree connect response assigned.</param>
    /// <param name="fileId">The FID the open response assigned.</param>
    /// <returns>The bytes to send.</returns>
    public static byte[] Encode(ushort userId, ushort treeId, ushort fileId)
    {
        Span<byte> parameters = stackalloc byte[ParametersLength];
        parameters.Clear();
        parameters[0] = WordCount;
        BinaryPrimitives.WriteUInt16LittleEndian(parameters[1..], fileId);
        return SmbMessageHeader.Frame(SmbMessageHeader.CloseCommand, userId, treeId, parameters);
    }
}
