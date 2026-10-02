using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>[READ]</c> lines curl 8.21.0 writes under <c>-v --trace-config read</c> (or <c>-vvv</c>,
/// <c>all</c>) as its client readers take an HTTP/1.x request body in (measured, BL-1189 Notes): the
/// reader added for the body, then two lines for each read into the upload buffer.
/// </summary>
internal static class HttpClientReaderTraceLines
{
    /// <summary>
    /// Gives the line for the reader curl adds for <paramref name="body" />: <c>add buf reader</c> for
    /// a body of bytes (<c>-d</c>), <c>add fread reader</c> for a <c>-T</c> upload of known length;
    /// <see langword="null" /> for an empty body, which curl sends without a reader, and for any other
    /// body, whose lines were not measured.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <param name="isUpload">Whether the body is a <c>-T</c> upload.</param>
    /// <returns>The line, such as <c>[READ] add buf reader, len=2 -&gt; 0</c>, or <see langword="null" />.</returns>
    internal static string? AddReader(HttpRequestBody body, bool isUpload)
    {
        if (body is BytesBody bytes)
        {
            return bytes.Content.IsEmpty ? null : Format($"[READ] add buf reader, len={bytes.Content.Length} -> 0");
        }

        return isUpload ? AddUploadReader(((StreamBody)body).Length.GetValueOrDefault()) : null;
    }

    /// <summary>Gives the line for a <c>-T</c> upload's reader, or <see langword="null" /> for an upload of no or unknown length.</summary>
    private static string? AddUploadReader(long length) =>
        length > 0 ? Format($"[READ] add fread reader, len={length} -> 0") : null;

    /// <summary>
    /// Gives the buffer reader's line for one read of a body of bytes.
    /// </summary>
    /// <param name="room">The bytes the upload buffer had room for.</param>
    /// <param name="read">The bytes read.</param>
    /// <param name="endOfBody">Whether the read reached the body's end.</param>
    /// <returns>The line, such as <c>[READ] cr_buf_read(len=65388) -&gt; 0, nread=2, eos=1</c>.</returns>
    internal static string BufferRead(int room, int read, bool endOfBody) =>
        Format($"[READ] cr_buf_read(len={room}) -> 0, nread={read}, eos={Flag(endOfBody)}");

    /// <summary>
    /// Gives the upload file reader's line for one read of a <c>-T</c> upload.
    /// </summary>
    /// <param name="requested">The bytes asked for: the room left, or less when less of the upload is left.</param>
    /// <param name="total">The upload's length.</param>
    /// <param name="readSoFar">The upload's bytes read so far, this read included.</param>
    /// <param name="read">The bytes this read gave.</param>
    /// <returns>The line, such as <c>[READ] cr_in_read(len=3, total=3, read=3) -&gt; 0, nread=3, eos=1</c>.</returns>
    internal static string InputRead(int requested, long total, long readSoFar, int read) =>
        Format($"[READ] cr_in_read(len={requested}, total={total}, read={readSoFar}) -> 0, nread={read}, eos={Flag(readSoFar == total)}");

    /// <summary>
    /// Gives the client's line for one read into the upload buffer, after its reader's line.
    /// </summary>
    /// <param name="room">The bytes the upload buffer had room for.</param>
    /// <param name="read">The bytes read.</param>
    /// <param name="endOfBody">Whether the read reached the body's end.</param>
    /// <returns>The line, such as <c>[READ] client_read(len=65388) -&gt; 0, nread=2, eos=1</c>.</returns>
    internal static string ClientRead(int room, int read, bool endOfBody) =>
        Format($"[READ] client_read(len={room}) -> 0, nread={read}, eos={Flag(endOfBody)}");

    private static int Flag(bool value) => value ? 1 : 0;

    private static string Format(FormattableString line) => line.ToString(CultureInfo.InvariantCulture);
}
