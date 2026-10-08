using System.Buffers.Binary;

namespace Curl.Protocol.Smb;

/// <summary>
/// The file's bytes in the server's SMB_COM_READ_ANDX response, found as curl 8.21.0's
/// <c>smb_request_state</c> finds them: the data length and offset words, the offset
/// counted from the SMB header.
/// </summary>
internal static class SmbReadResponse
{
    // Through the data offset word: one byte past curl's sizeof(struct smb_header) + 14,
    // which reads the word's high byte from its receive buffer past the reply (BL-1665).
    private const int MinimumLength = DataOffsetOffset + sizeof(ushort);

    private const int DataLengthOffset = SmbMessageHeader.Length + 11;
    private const int DataOffsetOffset = SmbMessageHeader.Length + 13;

    /// <summary>
    /// Finds the data, or gives curl's exit 56 message: <see cref="SmbMessages.ReceiveFailed" />
    /// for an error status or a response too short to hold the data words,
    /// <see cref="SmbMessages.InvalidInputPacket" /> for data that runs past the bytes received.
    /// </summary>
    /// <param name="received">Every byte received for the message, as curl counts <c>got</c>.</param>
    /// <param name="data">The data, when the response is accepted; empty for a read at the end of the file.</param>
    /// <returns><see langword="null" /> when the response is accepted, else the error message.</returns>
    public static string? TryGetData(byte[] received, out ReadOnlyMemory<byte> data)
    {
        data = ReadOnlyMemory<byte>.Empty;
        if (received.Length < MinimumLength || SmbMessageHeader.ReadStatus(received) != 0)
        {
            return SmbMessages.ReceiveFailed;
        }

        int length = BinaryPrimitives.ReadUInt16LittleEndian(received.AsSpan(DataLengthOffset));
        if (length == 0)
        {
            return null;
        }

        int start = BinaryPrimitives.ReadUInt16LittleEndian(received.AsSpan(DataOffsetOffset)) + SmbMessageHeader.NetBiosHeaderLength;
        if (start + length > received.Length)
        {
            return SmbMessages.InvalidInputPacket;
        }

        data = received.AsMemory(start, length);
        return null;
    }
}
