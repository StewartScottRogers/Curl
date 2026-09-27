using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// A <c>-T</c> upload source made ready to send, resumed from a <c>-C</c> offset as curl 8.21.0
/// resumes it over HTTP: the length left to send, the <c>Content-Range</c> value, and the
/// failure curl reports instead of sending (measured, BL-332 Notes).
/// </summary>
/// <remarks>
/// With no offset, or an offset of 0, the source is sent from its position and no
/// <c>Content-Range</c> is sent. A source that can seek is moved past the offset, and sends
/// <c>Content-Range: bytes N-L-1/L</c> for its length L; an offset at or past L fails with
/// exit 18 <c>File already completely uploaded</c>, and one into an empty source with exit 26
/// <c>Unable to resume from offset N</c>, each before a byte is sent. A source that cannot seek
/// (standard input) is sent whole, and its unknown length counts as -1, so curl sends
/// <c>Content-Range: bytes N-(N-2)/(N-1)</c>, such as <c>bytes 3-1/2</c>.
/// </remarks>
internal sealed class HttpUploadResume
{
    private HttpUploadResume(long? length, string? contentRange, HttpTransferException? failure)
    {
        Length = length;
        ContentRange = contentRange;
        Failure = failure;
    }

    /// <summary>
    /// Gets the number of bytes left to send, or <see langword="null" /> when the source cannot
    /// seek and its length is unknown.
    /// </summary>
    internal long? Length { get; }

    /// <summary>
    /// Gets the <c>Content-Range</c> value to send, or <see langword="null" /> to send none.
    /// </summary>
    internal string? ContentRange { get; }

    /// <summary>
    /// Gets the failure the transfer ends with once connected, before anything is sent, or
    /// <see langword="null" /> when the upload can be sent.
    /// </summary>
    internal HttpTransferException? Failure { get; }

    /// <summary>
    /// Makes <paramref name="upload" /> ready to send from <paramref name="resumeFrom" />,
    /// moving a source that can seek past the offset.
    /// </summary>
    /// <param name="upload">The <c>-T</c> source, at the position its bytes start from.</param>
    /// <param name="resumeFrom">The <c>-C</c> offset, or <see langword="null" /> without <c>-C</c>.</param>
    /// <returns>The resumed upload.</returns>
    internal static HttpUploadResume Of(Stream upload, long? resumeFrom)
    {
        long? length = upload.CanSeek ? upload.Length - upload.Position : null;
        return resumeFrom is > 0 and long offset ? Resumed(upload, length, offset) : new HttpUploadResume(length, null, null);
    }

    /// <summary>
    /// Resumes <paramref name="upload" />, of <paramref name="length" /> bytes or of unknown
    /// length, from a positive <paramref name="offset" />.
    /// </summary>
    private static HttpUploadResume Resumed(Stream upload, long? length, long offset)
    {
        if (length is not { } known)
        {
            return new HttpUploadResume(null, ContentRangeOf(offset, -1), null);
        }

        if (FailureOf(known, offset) is { } failure)
        {
            return new HttpUploadResume(known, null, failure);
        }

        upload.Seek(offset, SeekOrigin.Current);
        return new HttpUploadResume(known - offset, ContentRangeOf(offset, known - offset), null);
    }

    /// <summary>
    /// Gives the failure curl reports for a source of <paramref name="length" /> bytes resumed
    /// from <paramref name="offset" />: exit 26 for an empty one, exit 18 when the offset is at or
    /// past its end, and <see langword="null" /> otherwise.
    /// </summary>
    private static HttpTransferException? FailureOf(long length, long offset) =>
        length == 0
            ? new HttpTransferException(CurlExitCode.ReadError, HttpTransferMessages.UnableToResumeFrom(offset))
            : length <= offset
                ? new HttpTransferException(CurlExitCode.PartialFile, HttpTransferMessages.FileAlreadyCompletelyUploaded)
                : null;

    /// <summary>
    /// Formats curl's <c>Content-Range</c> value, <c>bytes N-(T-1)/T</c>, where T is
    /// <paramref name="left" /> plus <paramref name="offset" /> and an unknown length is -1.
    /// </summary>
    private static string ContentRangeOf(long offset, long left)
    {
        long total = left + offset;
        return string.Create(CultureInfo.InvariantCulture, $"bytes {offset}-{total - 1}/{total}");
    }
}
