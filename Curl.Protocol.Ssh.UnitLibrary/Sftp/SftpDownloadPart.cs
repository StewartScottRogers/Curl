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
    /// <param name="rangeText">
    /// The <c>-r</c> text, read only when <paramref name="range" /> is <see langword="null" />:
    /// the command line could not read it as a range, so it is read here as
    /// <c>Curl_ssh_range</c> reads it (BL-1396).
    /// </param>
    /// <returns>The part to read.</returns>
    /// <exception cref="SshTransferException">
    /// Exit 33 for a range that starts beyond the file (<c>Offset (N) was beyond file size
    /// (S)</c>) or after its end (<c>Bad range: start offset larger than end offset</c>), or
    /// for range text <c>Curl_ssh_range</c> cannot read (<c>Requested range was not delivered
    /// by the server</c>); exit 36, <c>Offset (N) was beyond file size (0)</c>, for <c>-C</c>
    /// when the size is unknown.
    /// </exception>
    internal static SftpDownloadPart Choose(ByteRange? range, long? resumeFrom, long? size, string? rangeText = null)
    {
        long resume = resumeFrom ?? 0;
        if (size is not { } known)
        {
            return resume > 0 ? throw SshTransferException.SftpResumeBeyondFileSize(resume, 0) : new SftpDownloadPart(0, null);
        }

        return resume > 0 ? Within(ByteRange.FromOffset(resume), known) : Asked(range, rangeText, known);
    }

    // The -r range when the command line read it, else its text, else the whole file.
    private static SftpDownloadPart Asked(ByteRange? range, string? rangeText, long size)
    {
        if (range is not null)
        {
            return Within(range, size);
        }

        return rangeText is null ? new SftpDownloadPart(0, size) : WithinText(rangeText, size);
    }

    // Curl_ssh_range: a suffix longer than the file is the whole file, a last position past
    // the end stops at the end, and a start past it or at it is refused.
    private static SftpDownloadPart Within(ByteRange range, long size)
    {
        long first = range.FirstBytePosition ?? size - Math.Min(range.SuffixLength!.Value, size);
        return Between(first, range.LastBytePosition, size);
    }

    private static SftpDownloadPart Between(long first, long? lastAsked, long size)
    {
        if (first > size)
        {
            throw SshTransferException.SftpRangeBeyondFileSize(first, size);
        }

        long last = Math.Min(lastAsked ?? size - 1, size - 1);
        return first > last
            ? throw SshTransferException.SftpRangeStartAfterEnd()
            : new SftpDownloadPart(first, last - first + 1);
    }

    // Curl_ssh_range on text: a number, blanks, one optional '-', blanks and a second
    // number, and nothing after; an overflowing number, no number at all, leftover text or
    // "-0" is exit 33 with curl's text for CURLE_RANGE_ERROR. "-N" is the last N bytes.
    private static SftpDownloadPart WithinText(string text, long size)
    {
        int position = 0;
        long? first = ReadNumber(text, ref position);
        SkipDash(text, ref position);
        long? last = ReadNumber(text, ref position);
        if (position < text.Length)
        {
            throw SshTransferException.SftpRangeNotDelivered();
        }

        return first is { } start ? Between(start, last, size) : LastBytes(last, size);
    }

    // Blanks, one optional '-', and the blanks curlx_str_numblanks skips before the second number.
    private static void SkipDash(string text, ref int position)
    {
        SkipBlanks(text, ref position);
        if (position < text.Length && text[position] == '-')
        {
            position++;
        }

        SkipBlanks(text, ref position);
    }

    // No first number: with no second either, or with "-0", there is no range.
    private static SftpDownloadPart LastBytes(long? count, long size) =>
        count is null or 0
            ? throw SshTransferException.SftpRangeNotDelivered()
            : Between(size - Math.Min(count.Value, size), null, size);

    private static void SkipBlanks(string text, ref int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t')
        {
            position++;
        }
    }

    // curlx_str_number: decimal digits up to long.MaxValue; more is an overflow.
    private static long? ReadNumber(string text, ref int position)
    {
        long? number = null;
        while (position < text.Length && char.IsAsciiDigit(text[position]))
        {
            long digit = text[position] - '0';
            long value = number ?? 0;
            number = value > (long.MaxValue - digit) / 10 ? throw SshTransferException.SftpRangeNotDelivered() : (value * 10) + digit;
            position++;
        }

        return number;
    }
}
