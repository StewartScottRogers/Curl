using System.Buffers.Binary;

namespace Curl.Protocol.Smb;

/// <summary>
/// What curl 8.21.0 takes from the server's SMB_COM_NT_CREATE_ANDX response: the FID,
/// the file's size (its end of file) and its last change time, which <c>-R</c> gives the
/// output file.
/// </summary>
/// <param name="FileId">The FID later requests name.</param>
/// <param name="EndOfFile">The file's size; negative when the server sent a size with its top bit set.</param>
/// <param name="LastChangeTimeUtc">The last change time, in whole seconds as curl's <c>get_posix_time</c> keeps it.</param>
internal sealed record SmbOpenResponse(ushort FileId, long EndOfFile, DateTimeOffset LastChangeTimeUtc)
{
    /// <summary>
    /// <c>sizeof(struct smb_nt_create_response)</c>: the fewest bytes curl accepts,
    /// NetBIOS header included.
    /// </summary>
    public const int Length = 100;

    private const int FileIdOffset = 42;
    private const int LastChangeTimeOffset = 72;
    private const int EndOfFileOffset = 92;

    // 1970-01-01 as a Windows FILETIME, in 100-nanosecond ticks since 1601-01-01.
    private const long UnixEpochFileTime = 116444736000000000;

    private const long TicksPerSecond = 10_000_000;

    private static readonly long MaxUnixSeconds = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    /// <summary>
    /// Reads a received open response, or refuses it as curl does: a status other than
    /// success, or fewer bytes received than <see cref="Length" />.
    /// </summary>
    /// <param name="received">Every byte received for the message, as curl counts <c>got</c>.</param>
    /// <param name="response">The FID, size and time when the response is accepted.</param>
    /// <returns><see langword="true" /> when the response is accepted.</returns>
    public static bool TryRead(ReadOnlySpan<byte> received, out SmbOpenResponse? response)
    {
        if (received.Length < Length || SmbMessageHeader.ReadStatus(received) != 0)
        {
            response = null;
            return false;
        }

        response = new SmbOpenResponse(
            BinaryPrimitives.ReadUInt16LittleEndian(received[FileIdOffset..]),
            BinaryPrimitives.ReadInt64LittleEndian(received[EndOfFileOffset..]),
            FromFileTime(BinaryPrimitives.ReadInt64LittleEndian(received[LastChangeTimeOffset..])));
        return true;
    }

    // get_posix_time: a time before 1970 is 1970 itself; one past DateTimeOffset's range is
    // its last second.
    private static DateTimeOffset FromFileTime(long fileTime) =>
        fileTime < UnixEpochFileTime
            ? DateTimeOffset.UnixEpoch
            : DateTimeOffset.FromUnixTimeSeconds(Math.Min((fileTime - UnixEpochFileTime) / TicksPerSecond, MaxUnixSeconds));
}
