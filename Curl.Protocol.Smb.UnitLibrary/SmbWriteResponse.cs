using System.Buffers.Binary;

namespace Curl.Protocol.Smb;

/// <summary>
/// The count of bytes written that curl 8.21.0 takes from the server's SMB_COM_WRITE_ANDX
/// response (<c>smb_request_state</c>, <c>SMB_UPLOAD</c>): the parameter word after the
/// AndX block.
/// </summary>
internal static class SmbWriteResponse
{
    // Through the count word: one byte past curl's sizeof(struct smb_header) + 6,
    // which reads the word's high byte from its receive buffer past the reply (BL-1665).
    private const int MinimumLength = CountOffset + sizeof(ushort);

    private const int CountOffset = SmbMessageHeader.Length + 5;

    /// <summary>
    /// Reads the count, or refuses the response as curl does with exit 25: a status other
    /// than success, or too few bytes to hold the count.
    /// </summary>
    /// <param name="received">Every byte received for the message, as curl counts <c>got</c>.</param>
    /// <param name="count">The bytes the server wrote, when the response is accepted.</param>
    /// <returns><see langword="true" /> when the response is accepted.</returns>
    public static bool TryReadCount(ReadOnlySpan<byte> received, out int count)
    {
        if (received.Length < MinimumLength || SmbMessageHeader.ReadStatus(received) != 0)
        {
            count = 0;
            return false;
        }

        count = BinaryPrimitives.ReadUInt16LittleEndian(received[CountOffset..]);
        return true;
    }
}
