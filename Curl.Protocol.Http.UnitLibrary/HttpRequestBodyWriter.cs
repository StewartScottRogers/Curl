using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Writes a request body to the connection after its head, as curl 8.21.0 sends it: the
/// bytes as they are, or in chunks of chunked transfer coding with lower-case hexadecimal
/// sizes and a closing <c>0</c> chunk (BL-175 Notes).
/// </summary>
/// <remarks>
/// A <see cref="StreamBody" /> is read into curl's <see cref="UploadBufferSize" />-byte upload
/// buffer, and each read becomes one chunk when the body is chunked. The first read shares
/// the buffer with the request head (<see cref="SharedHeadLength" />), and a chunked read
/// leaves <see cref="ChunkFramingReserve" /> bytes of it for the chunk's framing, so a body
/// is read in the same pieces curl reads it in (measured, BL-184 Notes). A stream of known
/// length is read up to that length and no further; if a read fails or the stream ends
/// first, the transfer ends with exit 26. A stream of unknown length ends at its end or at
/// its first failed read, as curl's body reader treats both. Under
/// <see cref="ExpectationWatch" /> every piece, a <see cref="BytesBody" /> cut into the same
/// buffer-sized pieces, is raced against a <c>417</c>, and sending stops at the first piece the
/// <c>417</c> beats (<see cref="CutShort" />, measured, BL-319 Notes).
/// </remarks>
/// <param name="connection">The connection the request head was written to.</param>
internal sealed class HttpRequestBodyWriter(IConnection connection)
{
    /// <summary>
    /// The size of curl's upload buffer, 64 KiB: the most bytes read from a
    /// <see cref="StreamBody" /> at a time.
    /// </summary>
    internal const int UploadBufferSize = 65536;

    /// <summary>
    /// The bytes of the upload buffer curl keeps back from each chunked read for the chunk's
    /// size line and closing CRLF: a stdin upload is sent in chunks of 65524 bytes (measured,
    /// BL-184 Notes).
    /// </summary>
    internal const int ChunkFramingReserve = 12;

    private bool headSent;

    private static readonly byte[] LastChunk = "0\r\n\r\n"u8.ToArray();

    /// <summary>
    /// Gets how many body bytes have been written, chunk framing excluded.
    /// </summary>
    internal long BytesWritten { get; private set; }

    /// <summary>
    /// Gets the length of the request head that shares the upload buffer with the first read:
    /// the whole head when the body follows it at once, 0 when the head went out alone to wait
    /// for <c>100 Continue</c>. A head that fills the buffer leaves the first read a whole one.
    /// </summary>
    internal int SharedHeadLength { get; init; }

    /// <summary>
    /// Gets the request head this writer sends, written and flushed by
    /// <see cref="WriteHeldHeadAsync" /> or just before the first body bytes, so a body whose
    /// first read fails sends no request bytes at all, as curl 8.21.0 holds the head in its
    /// upload buffer with the first read (measured, BL-184 Notes). Empty when the head was
    /// sent elsewhere.
    /// </summary>
    internal ReadOnlyMemory<byte> HeldHead
    {
        get => heldHead;
        init => heldHead = value;
    }

    private ReadOnlyMemory<byte> heldHead;

    /// <summary>
    /// Gets a value indicating whether the body is a <c>-T</c> upload, whose short read curl
    /// reports as <c>client read function EOF fail</c> rather than
    /// <c>client mime read EOF fail</c> (measured, BL-184 Notes).
    /// </summary>
    internal bool IsUpload { get; init; }

    /// <summary>
    /// Gets where the body bytes sent so far are reported, with the body's length when it is
    /// known: once before the first byte and after each piece sent.
    /// </summary>
    internal HttpTransferProgress Progress { get; init; } = HttpTransferProgress.Silent;

    private long? expectedLength;

    private long? startPosition;

    private ReadOnlyMemory<byte> unsent;

    /// <summary>
    /// Gets the connection that waited for <c>100 Continue</c> and so watches for a
    /// <c>417</c> while the body is sent, or <see langword="null" /> when the request waited
    /// for nothing and the body is sent whole.
    /// </summary>
    internal HttpContinueWaitConnection? ExpectationWatch { get; init; }

    /// <summary>
    /// Gets a value indicating whether sending stopped before the end of the body because a
    /// <c>417</c> arrived first.
    /// </summary>
    internal bool CutShort { get; private set; }

    /// <summary>
    /// Writes <paramref name="body" /> and flushes the connection.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="isChunked">Whether the body is sent with chunked transfer coding.</param>
    /// <param name="cancellationToken">Cancels the writes and reads.</param>
    /// <returns>A task that completes when the body has been written.</returns>
    /// <exception cref="HttpTransferException">
    /// A stream of known length failed a read or ended before its length was read (exit 26),
    /// or the connection failed a write (exit 55).
    /// </exception>
    internal async ValueTask WriteAsync(HttpRequestBody body, bool isChunked, CancellationToken cancellationToken)
    {
        expectedLength = body is BytesBody known ? known.Content.Length : ((StreamBody)body).Length;
        Progress.ReportUploaded(BytesWritten, expectedLength);
        if (body is BytesBody bytes)
        {
            await WriteBytesAsync(bytes.Content, isChunked, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteStreamAsync((StreamBody)body, isChunked, cancellationToken).ConfigureAwait(false);
        }

        await WriteHeldHeadAsync(cancellationToken).ConfigureAwait(false);
        if (isChunked && !CutShort)
        {
            await HttpConnectionSend.WriteAsync(connection, LastChunk, cancellationToken).ConfigureAwait(false);
        }

        await HttpConnectionSend.FlushAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes and flushes <see cref="HeldHead" /> if it has not been sent yet; does nothing
    /// after that.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write and the flush.</param>
    /// <returns>A task that completes when the head has been sent.</returns>
    /// <exception cref="HttpTransferException">The connection failed the write (exit 55).</exception>
    internal async ValueTask WriteHeldHeadAsync(CancellationToken cancellationToken)
    {
        if (heldHead.IsEmpty)
        {
            return;
        }

        ReadOnlyMemory<byte> head = heldHead;
        heldHead = ReadOnlyMemory<byte>.Empty;
        await HttpConnectionSend.WriteAsync(connection, head, cancellationToken).ConfigureAwait(false);
        await HttpConnectionSend.FlushAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the body a resend of this request sends: the same bytes, or the same stream
    /// rewound to where this writer began reading it; a stream that cannot seek goes on from
    /// where it is, after the bytes of a piece a <c>417</c> cut short, as curl 8.21.0 goes on
    /// reading stdin rather than failing (measured, BL-319 Notes).
    /// </summary>
    /// <param name="body">The body this writer was given.</param>
    /// <returns>The body to resend.</returns>
    internal HttpRequestBody Rewound(HttpRequestBody body)
    {
        if (body is not StreamBody stream)
        {
            return body;
        }

        if (startPosition is { } start)
        {
            stream.Content.Position = start;
            return body;
        }

        return stream with { Content = new HttpPrefixedStream(unsent, stream.Content) };
    }

    /// <summary>
    /// Writes a body of bytes: whole, or under <see cref="ExpectationWatch" /> in the pieces
    /// curl's upload buffer holds, until they are all sent or one is cut short.
    /// </summary>
    private async ValueTask WriteBytesAsync(ReadOnlyMemory<byte> content, bool isChunked, CancellationToken cancellationToken)
    {
        if (ExpectationWatch is null)
        {
            await WritePieceAsync(content, isChunked, cancellationToken).ConfigureAwait(false);
            return;
        }

        while (BytesWritten < content.Length && !CutShort)
        {
            int length = (int)Math.Min(NextReadRoom(isChunked), content.Length - BytesWritten);
            await WritePieceAsync(content.Slice((int)BytesWritten, length), isChunked, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask WriteStreamAsync(StreamBody body, bool isChunked, CancellationToken cancellationToken)
    {
        startPosition = body.Content.CanSeek ? body.Content.Position : null;
        byte[] buffer = new byte[UploadBufferSize];
        while (!CutShort)
        {
            int room = NextReadRoom(isChunked);
            long remaining = body.Length is { } length ? length - BytesWritten : room;
            if (remaining == 0)
            {
                return;
            }

            int read = await ReadAsync(body.Content, buffer.AsMemory(0, (int)Math.Min(remaining, room)), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                ThrowIfShort(body.Length);
                return;
            }

            await WritePieceAsync(buffer.AsMemory(0, read), isChunked, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns how many bytes the next read may take: what the upload buffer has left once the
    /// head still sharing it and, for a chunk, <see cref="ChunkFramingReserve" /> are taken
    /// out. Only the first read shares the buffer with the head.
    /// </summary>
    private int NextReadRoom(bool isChunked)
    {
        int reserve = isChunked ? ChunkFramingReserve : 0;
        int room = UploadBufferSize - (headSent ? 0 : SharedHeadLength) - reserve;
        headSent = true;
        return room > 0 ? room : UploadBufferSize - reserve;
    }

    /// <summary>
    /// Reads from the body stream, taking a failed read as the end of the stream, as curl
    /// does.
    /// </summary>
    private static async ValueTask<int> ReadAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private void ThrowIfShort(long? length)
    {
        if (length is { } needed)
        {
            string message = IsUpload
                ? HttpTransferMessages.UploadEndedEarly(BytesWritten, needed)
                : HttpTransferMessages.BodyStreamEndedEarly(BytesWritten, needed);
            throw new HttpTransferException(CurlExitCode.ReadError, message);
        }
    }

    private async ValueTask WritePieceAsync(ReadOnlyMemory<byte> piece, bool isChunked, CancellationToken cancellationToken)
    {
        if (piece.IsEmpty)
        {
            return;
        }

        if (ExpectationWatch is not { } watch)
        {
            await WriteHeldHeadAsync(cancellationToken).ConfigureAwait(false);
            await WriteFramedAsync(piece, isChunked, cancellationToken).ConfigureAwait(false);
        }
        else if (!await watch.SendUnlessExpectationFailedAsync(Framed(piece, isChunked), cancellationToken).ConfigureAwait(false))
        {
            CutShort = true;
            unsent = piece.ToArray();
            return;
        }

        BytesWritten += piece.Length;
        Progress.ReportUploaded(BytesWritten, expectedLength);
    }

    /// <summary>
    /// Writes a piece, for a chunked body with its chunk's size line and closing CRLF as
    /// writes of their own.
    /// </summary>
    private async ValueTask WriteFramedAsync(ReadOnlyMemory<byte> piece, bool isChunked, CancellationToken cancellationToken)
    {
        if (isChunked)
        {
            await HttpConnectionSend.WriteAsync(connection, ChunkSizeLine(piece.Length), cancellationToken).ConfigureAwait(false);
        }

        await HttpConnectionSend.WriteAsync(connection, piece, cancellationToken).ConfigureAwait(false);
        if (isChunked)
        {
            await HttpConnectionSend.WriteAsync(connection, "\r\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gives a piece as one write: itself, or for a chunked body the whole chunk, size line
    /// and closing CRLF included.
    /// </summary>
    private static ReadOnlyMemory<byte> Framed(ReadOnlyMemory<byte> piece, bool isChunked) =>
        isChunked ? (byte[])[.. ChunkSizeLine(piece.Length), .. piece.Span, .. "\r\n"u8] : piece;

    private static byte[] ChunkSizeLine(int length) =>
        Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{length:x}\r\n"));
}
