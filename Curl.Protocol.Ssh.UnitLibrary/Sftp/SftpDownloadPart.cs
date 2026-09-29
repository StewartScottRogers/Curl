using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// The part of a file an SFTP download reads, chosen from <c>-r</c> and <c>-C</c> and the
/// size <c>STAT</c> gave as curl 8.21.0's <c>sftp_download_stat</c> and
/// <c>Curl_ssh_range</c> choose it (ADR-0253). Measured 2026-09-29 (BL-573) against a
/// 10-byte file: <c>-r 2-5</c> reads 4 bytes at offset 2, <c>-r 7-</c> and <c>-r -3</c> 3 at
/// offset 7, <c>-r 5-100</c> 5 at offset 5, <c>-r -20</c> the whole file, and <c>-C 3</c> 7 at
/// offset 3, as <c>-r 3-</c> would.
/// </summary>
/// <param name="Offset">Where the first read starts.</param>
/// <param name="Length">How many bytes the download reads, or <see langword="null" /> when the size is unknown and it reads to the end.</param>
internal sealed record SftpDownloadPart(long Offset, long? Length)
{
    /// <summary>
    /// Chooses the part to read. curl turns a nonzero <c>-C</c> offset into the range
    /// <c>offset-</c>, in place of any <c>-r</c>; a range applies only when <c>STAT</c> gave a
    /// size, so with none the whole file is read, and a nonzero <c>-C</c> fails as beyond a
    /// size of 0.
    /// </summary>
    /// <param name="range">The <c>-r</c> range, or <see langword="null" />.</param>
    /// <param name="resumeFrom">The <c>-C</c> offset, or <see langword="null" />; 0 is no resume, as in curl.</param>
    /// <param name="size">The size <c>STAT</c> gave, or <see langword="null" /> when unknown.</param>
    /// <returns>The part to read.</returns>
    /// <exception cref="SshTransferException">
    /// Exit 33 for a range that starts beyond the file (<c>Offset (N) was beyond file size
    /// (S)</c>) or at its end (<c>Bad range: start offset larger than end offset</c>); exit 36,
    /// <c>Offset (N) was beyond file size (0)</c>, for <c>-C</c> when the size is unknown.
    /// </exception>
    internal static SftpDownloadPart Choose(ByteRange? range, long? resumeFrom, long? size)
    {
        long resume = resumeFrom ?? 0;
        ByteRange? asked = resume > 0 ? ByteRange.FromOffset(resume) : range;
        if (size is not { } known)
        {
            return resume > 0 ? throw SshTransferException.SftpResumeBeyondFileSize(resume, 0) : new SftpDownloadPart(0, null);
        }

        return asked is null ? new SftpDownloadPart(0, known) : Within(asked, known);
    }

    // Curl_ssh_range: a suffix longer than the file is the whole file, a last position past
    // the end stops at the end, and a start past it or at it is refused.
    private static SftpDownloadPart Within(ByteRange range, long size)
    {
        long first = range.FirstBytePosition ?? size - Math.Min(range.SuffixLength!.Value, size);
        if (first > size)
        {
            throw SshTransferException.SftpRangeBeyondFileSize(first, size);
        }

        long last = Math.Min(range.LastBytePosition ?? size - 1, size - 1);
        return first > last
            ? throw SshTransferException.SftpRangeStartAfterEnd()
            : new SftpDownloadPart(first, last - first + 1);
    }
}
