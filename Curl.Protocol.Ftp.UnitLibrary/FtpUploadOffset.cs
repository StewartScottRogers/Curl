namespace Curl.Protocol.Ftp;

/// <summary>
/// Skips the part of a <c>-T</c> upload a <c>-C</c> offset says the server already holds,
/// as curl 8.21.0 does before <c>APPE</c> (measured, ADR-0093's BL-439 addendum).
/// </summary>
/// <remarks>
/// A source that can seek, and is not empty, is moved past the offset; when the offset
/// reaches its end there is nothing left to send, and curl sends <c>QUIT</c> instead of
/// <c>APPE</c> and succeeds. An empty source is appended as it is. A source that cannot
/// seek (standard input) is appended whole: curl 8.21.0 skips none of it.
/// </remarks>
internal static class FtpUploadOffset
{
    /// <summary>
    /// Moves <paramref name="upload" /> past <paramref name="offset" /> bytes when it can.
    /// </summary>
    /// <param name="upload">The <c>-T</c> source, at the position its bytes start from.</param>
    /// <param name="offset">The positive <c>-C</c> offset.</param>
    /// <returns>
    /// <see langword="false" /> when the offset covers the whole of a non-empty source that
    /// can seek, so nothing is to be sent; otherwise <see langword="true" />.
    /// </returns>
    internal static bool TrySkip(Stream upload, long offset)
    {
        long remaining = upload.CanSeek ? upload.Length - upload.Position : 0;
        if (remaining <= 0)
        {
            return true;
        }

        if (offset >= remaining)
        {
            return false;
        }

        upload.Seek(offset, SeekOrigin.Current);
        return true;
    }
}
