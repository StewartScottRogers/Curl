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
/// There is no message for a download source that fails to be read after it opened:
/// curl 8.21.0 treats that as the end of the file and exits 0, measured with the
/// source under another process's byte-range lock.
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
    /// <param name="returned">How many bytes of that chunk the destination accepted.</param>
    /// <returns>The message to report.</returns>
    /// <remarks>
    /// Measured against curl 8.21.0, which reports how many bytes it offered and how many
    /// the destination took. Here the destination is a <see cref="Stream" />, whose
    /// <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)" /> either
    /// takes the whole chunk or throws, so <paramref name="returned" /> comes from the
    /// exception: <see cref="Abstractions.OutputWriteFailedException.BytesAccepted" /> when
    /// the destination threw one, and 0 for any other <see cref="IOException" />. Every
    /// measured <c>file://</c> case prints 0, because curl offers standard output at least
    /// 4096 bytes at once whenever the body is that long.
    /// </remarks>
    internal static string OutputWriteFailed(long passed, long returned) =>
        "Failure writing output to destination, passed "
        + passed.ToString(CultureInfo.InvariantCulture)
        + " returned "
        + returned.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The exit 23 message for a header output (<c>-D</c>) that stopped accepting bytes.
    /// </summary>
    /// <param name="passed">The length of the header line offered, CRLF included.</param>
    /// <returns>The message to report.</returns>
    /// <remarks>
    /// Measured against curl 8.21.0 on Windows on 2026-09-26, with the header output a pipe
    /// whose reader had already exited:
    /// <c>{ sleep 0.3; curl -sS -D - -o body.txt file:///C:/Temp/bl050/ten.txt; } | true</c>
    /// on a ten-byte file printed <c>curl: (23) client returned ERROR on write of 20 bytes</c>,
    /// and on a 100-byte file <c>of 21 bytes</c>: the length of the <c>Content-Length</c>
    /// line and its CRLF. libcurl hands each pseudo-header line to the client writer
    /// separately, so the count is the failing line's, not the whole block's, and the
    /// wording is not <see cref="OutputWriteFailed" />'s.
    /// </remarks>
    internal static string HeaderWriteFailed(long passed) =>
        "client returned ERROR on write of "
        + passed.ToString(CultureInfo.InvariantCulture)
        + " bytes";

    /// <summary>
    /// The exit 63 message for a download that delivered all <paramref name="maxFileSize" />
    /// bytes <c>--max-filesize</c> allows and had more to deliver, measured against curl
    /// 8.21.0: <c>Exceeded the maximum allowed file size (9) with 9 bytes</c>.
    /// </summary>
    /// <param name="maxFileSize">The limit.</param>
    /// <param name="delivered">The body bytes written before the transfer stopped.</param>
    /// <returns>The message.</returns>
    internal static string MaxFileSizeExceeded(long maxFileSize, long delivered) =>
        "Exceeded the maximum allowed file size ("
        + maxFileSize.ToString(CultureInfo.InvariantCulture)
        + ") with "
        + delivered.ToString(CultureInfo.InvariantCulture)
        + " bytes";

    /// <summary>
    /// The exit 36 message for a resume offset or range start past the end of the file.
    /// </summary>
    internal const string ResumeFailed = "failed to resume file:// transfer";

    /// <summary>
    /// The exit 33 message for <c>-r</c>/<c>--range</c> text that names no range, such as
    /// <c>5-2</c>, <c>abc</c> or <c>-0</c>: curl 8.21.0's <c>Curl_range</c> answers it with
    /// <c>CURLE_RANGE_ERROR</c>, whose easy error text this is.
    /// </summary>
    internal const string RangeNotDelivered = "Requested range was not delivered by the server";

    /// <summary>
    /// The exit 36 message for a suffix range — <c>-r -12</c> — asking for more trailing
    /// bytes than the file can bear. Measured against curl 8.21.0, which prints this and
    /// not <see cref="ResumeFailed" /> for that one case: on a ten-byte file <c>-r -11</c>
    /// succeeds with the whole file and <c>-r -12</c> fails with this line, so the two
    /// strings are distinct on purpose and neither is a paraphrase of the other.
    /// </summary>
    internal const string CouldNotResumeDownload = "Could not resume download";

    /// <summary>
    /// The exit 26 message for an upload source of known length that failed to be read
    /// before all of it was sent.
    /// </summary>
    /// <param name="read">
    /// The bytes read from the start of the source before the failed read, which is a whole
    /// number of 65536-byte chunks and counts any skipped by <c>-C</c>.
    /// </param>
    /// <param name="needed">The length of the source, which is what curl expected to send.</param>
    /// <returns>The message to report.</returns>
    /// <remarks>
    /// <para>
    /// Measured against curl 8.21.0 on Windows on 2026-09-26, with the source's bytes from
    /// 99000 on held under a byte-range lock by another process so that the open succeeds
    /// and a later read fails:
    /// <c>curl -sS -T src.txt file:///Z:/repos/Curl.lanes/bl023tmp/out.txt</c> on a
    /// 100000-byte <c>src.txt</c> printed
    /// <c>curl: (26) client read function EOF fail, only 65536/100000 of needed bytes read</c>,
    /// and <c>-C 10</c> and <c>--crlf</c> printed the same line. With the whole file
    /// locked it printed <c>only 0/100000</c>.
    /// </para>
    /// <para>
    /// The wording is libcurl's, not the tool's: the curl tool's read callback turns a
    /// failed <c>read()</c> into end of file, and libcurl then reports an upload that
    /// ended short of its known size. The libcurl <c>strerror</c> text,
    /// <c>Failed to open/read local data from file/application</c>, is never printed on
    /// this path; curl prints it only when the tool cannot open the <c>-T</c> file at all,
    /// which happens before any handler runs. curl reads an upload 65536 bytes at a time
    /// from the start of the file, <c>-C</c> or not, and so does this handler, so the two
    /// report the same <paramref name="read" /> for the same failure point.
    /// </para>
    /// </remarks>
    internal static string UploadSourceReadFailed(long read, long needed) =>
        "client read function EOF fail, only "
        + read.ToString(CultureInfo.InvariantCulture)
        + "/"
        + needed.ToString(CultureInfo.InvariantCulture)
        + " of needed bytes read";

    /// <summary>
    /// The exit 55 message for an upload destination that opened and then failed to be
    /// written to.
    /// </summary>
    /// <remarks>
    /// Measured against curl 8.21.0 on Windows on 2026-09-26, with the destination held
    /// under a byte-range lock by another process from byte 99000 on, so that the open and
    /// the first write succeed and a later write fails:
    /// <c>curl -sS -T src.txt file:///Z:/repos/Curl.lanes/bl023tmp/dest.txt</c> on a
    /// 100000-byte <c>src.txt</c> printed <c>curl: (55) Failed sending data to the peer</c>
    /// with <c>%{size_upload}</c> 65536, and so did <c>-a</c> and a lock from byte 0. It is
    /// exit 55, <c>CURLE_SEND_ERROR</c>, not exit 23: libcurl's <c>file://</c> upload
    /// treats the destination as the peer. The <c>CURLE_WRITE_ERROR</c> text,
    /// <c>Failed writing received data to disk/application</c>, is not printed on this path.
    /// </remarks>
    internal const string DestinationWriteFailed = "Failed sending data to the peer";

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
    /// The information line that ends a <c>file://</c> transfer which got past its open,
    /// when libcurl keeps nothing it has to tear down in a hurry.
    /// </summary>
    /// <param name="connectionNumber">The number libcurl gave the transfer's connection.</param>
    /// <returns>The line, without the <c>* </c> prefix.</returns>
    /// <remarks>
    /// Measured against curl 8.21.0 on Windows on 2026-09-29 (BL-936): a download, a
    /// <c>-T</c> upload, <c>-I</c>, an unmet <c>-z</c>, exit 36, exit 55 and exit 63 all end
    /// <c>* shutting down connection #0</c> under <c>-v</c> and <c>--trace</c>.
    /// </remarks>
    internal static string ShuttingDownConnection(long connectionNumber) =>
        "shutting down connection #" + connectionNumber.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The information line that ends a <c>file://</c> transfer which got past its open and
    /// then failed in a way libcurl's <c>multi_done</c> counts as premature.
    /// </summary>
    /// <param name="connectionNumber">The number libcurl gave the transfer's connection.</param>
    /// <returns>The line, without the <c>* </c> prefix.</returns>
    /// <remarks>
    /// Measured against curl 8.21.0 on Windows on 2026-09-29 (BL-936): exit 23 (an upload
    /// destination that would not open, an output that refused a write) and exit 26 (an
    /// upload source that ran short) end <c>* closing connection #0</c>.
    /// </remarks>
    internal static string ClosingConnection(long connectionNumber) =>
        "closing connection #" + connectionNumber.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The pseudo-header lines curl synthesises for a local file, each with its CRLF,
    /// followed by the blank line that ends a header block. They are kept apart because
    /// curl writes them apart, and a failing header output reports the length of the line
    /// it failed on (<see cref="HeaderWriteFailed" />).
    /// </summary>
    /// <param name="length">
    /// The length of the whole file. It stays the whole file even when a range was asked
    /// for, which is measured behaviour rather than an oversight.
    /// </param>
    /// <param name="lastWriteTimeUtc">
    /// The file's last-write timestamp, or <see langword="null" /> when the file system
    /// could not determine one.
    /// </param>
    /// <returns>The header lines to write, in order.</returns>
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
    internal static string[] PseudoHeaderLines(long length, DateTimeOffset? lastWriteTimeUtc) =>
        [
            .. PseudoHeaders(length, lastWriteTimeUtc).Select(header => header.Key + ": " + header.Value + "\r\n"),
            EndOfHeaders,
        ];

    /// <summary>
    /// The pseudo-headers of <see cref="PseudoHeaderLines" /> as name and value pairs,
    /// without line endings or the blank line, in the order they are written: what the
    /// handler reports as <see cref="Abstractions.TransferReport.PseudoHeaders" />.
    /// </summary>
    /// <param name="length">The length of the whole file.</param>
    /// <param name="lastWriteTimeUtc">
    /// The file's last-write timestamp, or <see langword="null" /> when the file system
    /// could not determine one, which leaves <c>Last-Modified</c> out.
    /// </param>
    /// <returns>Two or three headers.</returns>
    internal static KeyValuePair<string, string>[] PseudoHeaders(long length, DateTimeOffset? lastWriteTimeUtc)
    {
        KeyValuePair<string, string> contentLength = new("Content-Length", length.ToString(CultureInfo.InvariantCulture));

        return lastWriteTimeUtc is { } knownLastWriteTimeUtc
            ? [contentLength, AcceptRanges, LastModified(knownLastWriteTimeUtc)]
            : [contentLength, AcceptRanges];
    }

    /// <summary>
    /// The <c>Accept-ranges</c> pseudo-header.
    /// </summary>
    private static readonly KeyValuePair<string, string> AcceptRanges = new("Accept-ranges", "bytes");

    /// <summary>
    /// The blank line that ends a header block.
    /// </summary>
    private const string EndOfHeaders = "\r\n";

    /// <summary>
    /// The <c>Last-Modified</c> pseudo-header.
    /// </summary>
    /// <param name="lastWriteTimeUtc">The file's last-write timestamp.</param>
    /// <returns>The header, its value in RFC 1123 form.</returns>
    private static KeyValuePair<string, string> LastModified(DateTimeOffset lastWriteTimeUtc) =>
        new("Last-Modified", lastWriteTimeUtc.UtcDateTime.ToString("R", CultureInfo.InvariantCulture));
}
