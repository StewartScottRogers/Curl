using System.Globalization;

namespace Curl.Protocol.File;

/// <summary>
/// Every byte of text the <c>file</c> scheme emits: the failure messages behind
/// <c>%{errormsg}</c> and the pseudo-headers curl synthesises for a local file.
/// </summary>
/// <remarks>
/// <para>
/// One home for all of it, because byte fidelity with curl 8.21.0 is the point and a
/// string built at its point of use is a string that drifts. Two asymmetries here are
/// measured, not accidental: the read failure quotes the still-encoded URL path, while
/// the upload failure quotes the decoded native path, and <c>Accept-ranges</c> carries a
/// lowercase <c>r</c> where <c>Content-Length</c> and <c>Last-Modified</c> are
/// capitalised as HTTP spells them.
/// </para>
/// <para>
/// The two messages curl does not print itself — a mid-transfer read failure and a
/// failure writing to an upload destination — carry the text
/// <c>curl_easy_strerror</c> gives for <c>CURLE_READ_ERROR</c> and
/// <c>CURLE_WRITE_ERROR</c>, which is what <c>%{errormsg}</c> falls back to when no
/// more specific message was set.
/// </para>
/// </remarks>
internal static class FileTransferMessages
{
    /// <summary>
    /// The exit 3 message for a <c>file://</c> URL curl will not accept, host and all.
    /// </summary>
    internal const string BadUrl = "URL rejected: Bad file:// URL";

    /// <summary>
    /// The exit 23 message for a destination write that failed part way through.
    /// </summary>
    internal const string OutputWriteFailed = "Failure writing output to destination";

    /// <summary>
    /// The exit 36 message for a resume offset or range start past the end of the file.
    /// </summary>
    internal const string ResumeFailed = "failed to resume file:// transfer";

    /// <summary>
    /// The exit 36 message for a suffix range — <c>-r -12</c> — asking for more trailing
    /// bytes than the file can bear. Measured against curl 8.21.0, which prints this and
    /// not <see cref="ResumeFailed" /> for that one case: on a ten-byte file <c>-r -11</c>
    /// succeeds with the whole file and <c>-r -12</c> fails with this line, so the two
    /// strings are distinct on purpose and neither is a paraphrase of the other.
    /// </summary>
    internal const string CouldNotResumeDownload = "Could not resume download";

    /// <summary>
    /// The exit 26 message for a source that opened and then failed to be read.
    /// </summary>
    internal const string ReadFailed = "Failed to open/read local data from file/application";

    /// <summary>
    /// The exit 23 message for an upload destination that opened and then failed to be
    /// written to.
    /// </summary>
    internal const string DestinationWriteFailed =
        "Failed writing received data to disk/application";

    /// <summary>
    /// The exit 37 message for a source that could not be opened, whichever
    /// <see cref="Abstractions.FileAccessStatus" /> the open reported: curl prints one
    /// line for all four, and it quotes the path as the URL spelled it, still
    /// percent-encoded.
    /// </summary>
    /// <param name="urlPath">The still-encoded path from the URL.</param>
    /// <returns>The message to report.</returns>
    internal static string CouldNotOpenForReading(string urlPath) =>
        $"Could not open file {urlPath}";

    /// <summary>
    /// The exit 23 message for an upload destination that could not be opened. Unlike
    /// the read failure, this quotes the decoded operating-system path, separators and
    /// all.
    /// </summary>
    /// <param name="osPath">The decoded operating-system path.</param>
    /// <returns>The message to report.</returns>
    internal static string CannotOpenForWriting(string osPath) =>
        $"cannot open {osPath} for writing";

    /// <summary>
    /// The three pseudo-headers curl synthesises for a local file, followed by the blank
    /// line that ends a header block.
    /// </summary>
    /// <param name="length">
    /// The length of the whole file. It stays the whole file even when a range was asked
    /// for, which is measured behaviour rather than an oversight.
    /// </param>
    /// <param name="lastWriteTimeUtc">The file's last-write timestamp.</param>
    /// <returns>The header block to write.</returns>
    internal static string PseudoHeaders(long length, DateTimeOffset lastWriteTimeUtc) =>
        "Content-Length: " + length.ToString(CultureInfo.InvariantCulture) + "\r\n"
        + "Accept-ranges: bytes\r\n"
        + "Last-Modified: " + lastWriteTimeUtc.UtcDateTime.ToString("R", CultureInfo.InvariantCulture)
        + "\r\n\r\n";
}
