using System.Buffers;
using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Http;

/// <summary>Reads a whole response body framed by <c>Content-Length</c>, the chunked coding or the close (RFC 9112 §6.3, §7.1).</summary>
internal static class PrivacyPassHttpBodyReader
{
    /// <summary>The longest body, in bytes, before the exchange fails with <see cref="PrivacyPassHttpFailure.BodyTooLarge"/>: 1 GiB, as the whole body is held in memory.</summary>
    public const int MaxBodyLength = 1073741824;

    private const int CloseDelimitedReadSize = 4096;

    /// <summary>Reads the body whole.</summary>
    /// <param name="reader">The connection's reader, positioned after the head.</param>
    /// <param name="bodyLength">The framing <see cref="PrivacyPassHttpResponseHead.FindBodyLength"/> decided.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The body.</returns>
    public static async ValueTask<byte[]> ReadAsync(PrivacyPassHttpConnectionReader reader, long? bodyLength, CancellationToken cancellationToken)
    {
        var body = new ArrayBufferWriter<byte>();
        if (bodyLength is null)
        {
            await ReadChunkedAsync(reader, body, cancellationToken);
        }
        else if (bodyLength == -1)
        {
            await ReadUntilCloseAsync(reader, body, cancellationToken);
        }
        else
        {
            await ReadExactlyAsync(reader, body, bodyLength.Value, cancellationToken);
        }

        return body.WrittenSpan.ToArray();
    }

    private static void RequireRoomFor(ArrayBufferWriter<byte> body, long length)
    {
        if (body.WrittenCount + length > MaxBodyLength)
        {
            throw new PrivacyPassHttpException(
                PrivacyPassHttpFailure.BodyTooLarge, CurlExitCode.FilesizeExceeded, $"The response body is longer than {MaxBodyLength} bytes.");
        }
    }

    private static async ValueTask ReadExactlyAsync(PrivacyPassHttpConnectionReader reader, ArrayBufferWriter<byte> body, long length, CancellationToken cancellationToken)
    {
        RequireRoomFor(body, length);
        for (var remaining = (int)length; remaining > 0;)
        {
            var read = await reader.ReadAsync(body.GetMemory(remaining)[..remaining], cancellationToken);
            if (read == 0)
            {
                throw PrivacyPassHttpException.ClosedEarly();
            }

            body.Advance(read);
            remaining -= read;
        }
    }

    private static async ValueTask ReadUntilCloseAsync(PrivacyPassHttpConnectionReader reader, ArrayBufferWriter<byte> body, CancellationToken cancellationToken)
    {
        for (var read = await reader.ReadAsync(body.GetMemory(CloseDelimitedReadSize), cancellationToken); read > 0;
            read = await reader.ReadAsync(body.GetMemory(CloseDelimitedReadSize), cancellationToken))
        {
            body.Advance(read);
            RequireRoomFor(body, 0);
        }
    }

    private static async ValueTask ReadChunkedAsync(PrivacyPassHttpConnectionReader reader, ArrayBufferWriter<byte> body, CancellationToken cancellationToken)
    {
        for (var size = await ReadChunkSizeAsync(reader, cancellationToken); size > 0; size = await ReadChunkSizeAsync(reader, cancellationToken))
        {
            await ReadExactlyAsync(reader, body, size, cancellationToken);
            if ((await ReadRequiredLineAsync(reader, cancellationToken)).Length > 0)
            {
                throw InvalidChunk("A chunk's data is not followed by CRLF.");
            }
        }

        // RFC 9112 §7.1.2: trailer fields end at an empty line; the client has no use for them.
        while ((await ReadRequiredLineAsync(reader, cancellationToken)).Length > 0)
        {
        }
    }

    private static async ValueTask<long> ReadChunkSizeAsync(PrivacyPassHttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var line = await ReadRequiredLineAsync(reader, cancellationToken);
        var sizeEnd = line.AsSpan().IndexOfAny(';', ' ', '\t');
        var digits = sizeEnd < 0 ? line.AsSpan() : line.AsSpan(0, sizeEnd);
        return long.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size) && size >= 0
            ? size
            : throw InvalidChunk($"The chunk size line \"{line}\" is not a hexadecimal size.");
    }

    private static async ValueTask<string> ReadRequiredLineAsync(PrivacyPassHttpConnectionReader reader, CancellationToken cancellationToken) =>
        await reader.ReadLineAsync(cancellationToken) ?? throw PrivacyPassHttpException.ClosedEarly();

    private static PrivacyPassHttpException InvalidChunk(string message) => PrivacyPassHttpException.Malformed(PrivacyPassHttpFailure.InvalidChunk, message);
}
