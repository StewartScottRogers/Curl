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
    /// The exit 23 message for a download destination that stopped accepting bytes.
    /// </summary>
    /// <param name="passed">The size of the chunk offered to the destination.</param>
    /// <returns>The message to report.</returns>
    /// <remarks>
    /// Measured against curl 8.21.0, which reports how many bytes it offered and how many
    /// the destination took. Here the destination is a <see cref="Stream" />, and
    /// <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)" /> either
    /// takes the whole chunk or throws, so a partial write is not observable and the
    /// <c>returned</c> count is always 0.
    /// </remarks>
    internal static string OutputWriteFailed(long passed) =>
        "Failure writing output to destination, passed "
        + passed.ToString(CultureInfo.InvariantCulture)
        + " returned 0";

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
    /// The pseudo-headers curl synthesises for a local file, followed by the blank line
    /// that ends a header block.
    /// </summary>
    /// <param name="length">
    /// The length of the whole file. It stays the whole file even when a range was asked
    /// for, which is measured behaviour rather than an oversight.
    /// </param>
    /// <param name="lastWriteTimeUtc">
    /// The file's last-write timestamp, or <see langword="null" /> when the file system
    /// could not determine one.
    /// </param>
    /// <returns>The header block to write.</returns>
    /// <remarks>
    /// <para>
    /// With a timestamp the block is three lines: <c>Content-Length</c>,
    /// <c>Accept-ranges</c> and <c>Last-Modified</c>. Without one the whole
    /// <c>Last-Modified</c> line is left out, so the block is
    /// <c>Content-Length: &lt;n&gt;\r\nAccept-ranges: bytes\r\n\r\n</c>. The premise is
    /// upstream libcurl 8.21.0's <c>lib/file.c</c>, which writes <c>Last-Modified</c> only
    /// when the stat of the opened handle succeeded and so gave a usable modification
    /// time; a year-0001 date is not something curl can print.
    /// </para>
    /// <para>
    /// That case could not be produced from the curl 8.21.0 binary on Windows: even
    /// <c>curl -sI file:///NUL</c>, a device with no real modification time, prints
    /// <c>Last-Modified: Thu, 01 Jan 1970 00:00:00 GMT</c>, because the stat succeeds and
    /// reports zero. The omission therefore rests on the upstream source, not on a
    /// measurement.
    /// </para>
    /// </remarks>
    internal static string PseudoHeaders(long length, DateTimeOffset? lastWriteTimeUtc) =>
        "Content-Length: " + length.ToString(CultureInfo.InvariantCulture) + "\r\n"
        + "Accept-ranges: bytes\r\n"
        + LastModifiedLine(lastWriteTimeUtc)
        + "\r\n";

    /// <summary>
    /// The <c>Last-Modified</c> pseudo-header line, or nothing when the timestamp is
    /// unknown.
    /// </summary>
    /// <param name="lastWriteTimeUtc">
    /// The file's last-write timestamp, or <see langword="null" /> when unknown.
    /// </param>
    /// <returns>The line with its line ending, or an empty string.</returns>
    private static string LastModifiedLine(DateTimeOffset? lastWriteTimeUtc) =>
        lastWriteTimeUtc is { } knownLastWriteTimeUtc
            ? "Last-Modified: " + knownLastWriteTimeUtc.UtcDateTime.ToString("R", CultureInfo.InvariantCulture) + "\r\n"
            : string.Empty;
}
