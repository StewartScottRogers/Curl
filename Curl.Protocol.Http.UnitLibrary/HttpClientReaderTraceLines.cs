using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>[READ]</c> lines curl 8.21.0 writes under <c>-v --trace-config read</c> (or <c>-vvv</c>,
/// <c>all</c>) as its client readers take an HTTP/1.x request body in (measured, BL-1189 and BL-1214
/// Notes): the reader added for the body, then for each read into the upload buffer the reader's
/// lines, the chunk encoder's lines when the body is chunked, and the client's line.
/// </summary>
internal static class HttpClientReaderTraceLines
{
    /// <summary>The chunk encoder's line for the closing <c>0</c> chunk, written with the read that reached the body's end.</summary>
    internal const string AddedLastChunk = "[READ] http_chunk, added last, empty chunk";

    /// <summary>
    /// Gives whether curl reads <paramref name="body" /> through client readers at all: every body but
    /// an empty one, which curl sends without a reader.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <returns><see langword="true" /> when the body's reads are traced.</returns>
    internal static bool IsRead(HttpRequestBody body) =>
        body is BytesBody bytes ? !bytes.Content.IsEmpty : ((StreamBody)body).Length != 0;

    /// <summary>
    /// Gives the line for the reader curl adds for <paramref name="body" />: <c>add buf reader</c> for
    /// a body of bytes (<c>-d</c>), <c>add fread reader</c> for a <c>-T</c> upload, with <c>len=-1</c>
    /// when its length is unknown (stdin); <see langword="null" /> for a multipart body (<c>-F</c>),
    /// whose reader curl adds without a line.
    /// </summary>
    /// <param name="body">The request body, one <see cref="IsRead" /> accepts.</param>
    /// <param name="isUpload">Whether the body is a <c>-T</c> upload.</param>
    /// <returns>The line, such as <c>[READ] add buf reader, len=2 -&gt; 0</c>, or <see langword="null" />.</returns>
    internal static string? AddReader(HttpRequestBody body, bool isUpload)
    {
        if (body is BytesBody bytes)
        {
            return Format($"[READ] add buf reader, len={bytes.Content.Length} -> 0");
        }

        return isUpload ? Format($"[READ] add fread reader, len={((StreamBody)body).Length ?? -1} -> 0") : null;
    }

    /// <summary>
    /// Gives the buffer reader's line for one read of a body of bytes.
    /// </summary>
    /// <param name="room">The bytes the upload buffer had room for, chunk framing taken out.</param>
    /// <param name="read">The bytes read.</param>
    /// <param name="endOfBody">Whether the read reached the body's end.</param>
    /// <returns>The line, such as <c>[READ] cr_buf_read(len=65388) -&gt; 0, nread=2, eos=1</c>.</returns>
    internal static string BufferRead(int room, int read, bool endOfBody) =>
        Format($"[READ] cr_buf_read(len={room}) -> 0, nread={read}, eos={Flag(endOfBody)}");

    /// <summary>
    /// Gives the upload file reader's line for one read of a <c>-T</c> upload.
    /// </summary>
    /// <param name="requested">The bytes asked for: the room left, or less when less of the upload is left.</param>
    /// <param name="total">The upload's length, or <see langword="null" /> when unknown, written as <c>-1</c>.</param>
    /// <param name="readSoFar">The upload's bytes read so far, this read included.</param>
    /// <param name="read">The bytes this read gave.</param>
    /// <param name="endOfBody">Whether the read reached the upload's end.</param>
    /// <returns>The line, such as <c>[READ] cr_in_read(len=3, total=3, read=3) -&gt; 0, nread=3, eos=1</c>.</returns>
    internal static string InputRead(int requested, long? total, long readSoFar, int read, bool endOfBody) =>
        Format($"[READ] cr_in_read(len={requested}, total={total ?? -1}, read={readSoFar}) -> 0, nread={read}, eos={Flag(endOfBody)}");

    /// <summary>
    /// Gives the multipart reader's two lines for one read of a <c>-F</c> body: the encoder's read,
    /// then the reader's tally.
    /// </summary>
    /// <param name="requested">The bytes asked for: the room left, or less when less of the body is left.</param>
    /// <param name="total">The body's length.</param>
    /// <param name="readSoFar">The body's bytes read so far, this read included.</param>
    /// <param name="read">The bytes this read gave.</param>
    /// <returns>
    /// The lines, such as <c>[READ] cr_mime_read(len=149), mime_read() -&gt; 149</c> and
    /// <c>[READ] cr_mime_read(len=149, total=149, read=149) -&gt; 0, 149, 1</c>.
    /// </returns>
    internal static string[] MimeRead(int requested, long total, long readSoFar, int read) =>
    [
        Format($"[READ] cr_mime_read(len={requested}), mime_read() -> {read}"),
        Format($"[READ] cr_mime_read(len={requested}, total={total}, read={readSoFar}) -> 0, {read}, {Flag(readSoFar == total)}"),
    ];

    /// <summary>
    /// Gives the chunk encoder's line for the chunk one read of a chunked body made.
    /// </summary>
    /// <param name="length">The chunk's data bytes.</param>
    /// <returns>The line, such as <c>[READ] http_chunk, made chunk of 3 bytes -&gt; 0</c>.</returns>
    internal static string MadeChunk(int length) =>
        Format($"[READ] http_chunk, made chunk of {length} bytes -> 0");

    /// <summary>
    /// Gives the client's line for one read into the upload buffer, after its reader's lines.
    /// </summary>
    /// <param name="room">The bytes the upload buffer had room for.</param>
    /// <param name="read">The bytes read, chunk framing included.</param>
    /// <param name="endOfBody">Whether the read reached the body's end.</param>
    /// <returns>The line, such as <c>[READ] client_read(len=65388) -&gt; 0, nread=2, eos=1</c>.</returns>
    internal static string ClientRead(int room, int read, bool endOfBody) =>
        Format($"[READ] client_read(len={room}) -> 0, nread={read}, eos={Flag(endOfBody)}");

    private static int Flag(bool value) => value ? 1 : 0;

    private static string Format(FormattableString line) => line.ToString(CultureInfo.InvariantCulture);
}
