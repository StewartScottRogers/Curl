using System.Buffers;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Http;

/// <summary>Reads a whole response body framed by <c>Content-Length</c>, the chunked coding or the close (RFC 9112 §6.3, §7.1).</summary>
internal static class OAuth2HttpBodyReader
{
    /// <summary>The longest body, in bytes, before the exchange fails with <see cref="OAuth2HttpFailure.BodyTooLarge"/>; a token response is far smaller.</summary>
    public const int MaxBodyLength = 1048576;

    private const int CloseDelimitedReadSize = 4096;

    /// <summary>Reads the body and decodes it as UTF-8.</summary>
    /// <param name="reader">The connection's reader, positioned after the head.</param>
    /// <param name="bodyLength">The framing <see cref="OAuth2HttpResponseHead.FindBodyLength"/> decided.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The body.</returns>
    public static async ValueTask<string> ReadAsync(OAuth2HttpConnectionReader reader, long? bodyLength, CancellationToken cancellationToken)
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

        return Encoding.UTF8.GetString(body.WrittenSpan);
    }

    private static void RequireRoomFor(ArrayBufferWriter<byte> body, long length)
    {
        if (body.WrittenCount + length > MaxBodyLength)
        {
            throw new OAuth2HttpException(
                OAuth2HttpFailure.BodyTooLarge, CurlExitCode.FilesizeExceeded, $"The response body is longer than {MaxBodyLength} bytes.");
        }
    }

    private static async ValueTask ReadExactlyAsync(OAuth2HttpConnectionReader reader, ArrayBufferWriter<byte> body, long length, CancellationToken cancellationToken)
    {
        RequireRoomFor(body, length);
        for (var remaining = (int)length; remaining > 0;)
        {
            var read = await reader.ReadAsync(body.GetMemory(remaining)[..remaining], cancellationToken);
            if (read == 0)
            {
                throw OAuth2HttpException.ClosedEarly();
            }

            body.Advance(read);
            remaining -= read;
        }
    }

    private static async ValueTask ReadUntilCloseAsync(OAuth2HttpConnectionReader reader, ArrayBufferWriter<byte> body, CancellationToken cancellationToken)
    {
        for (var read = await reader.ReadAsync(body.GetMemory(CloseDelimitedReadSize), cancellationToken); read > 0;
            read = await reader.ReadAsync(body.GetMemory(CloseDelimitedReadSize), cancellationToken))
        {
            body.Advance(read);
            RequireRoomFor(body, 0);
        }
    }

    private static async ValueTask ReadChunkedAsync(OAuth2HttpConnectionReader reader, ArrayBufferWriter<byte> body, CancellationToken cancellationToken)
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

    private static async ValueTask<long> ReadChunkSizeAsync(OAuth2HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var line = await ReadRequiredLineAsync(reader, cancellationToken);
        var sizeEnd = line.AsSpan().IndexOfAny(';', ' ', '\t');
        var digits = sizeEnd < 0 ? line.AsSpan() : line.AsSpan(0, sizeEnd);
        return long.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size) && size >= 0
            ? size
            : throw InvalidChunk($"The chunk size line \"{line}\" is not a hexadecimal size.");
    }

    private static async ValueTask<string> ReadRequiredLineAsync(OAuth2HttpConnectionReader reader, CancellationToken cancellationToken) =>
        await reader.ReadLineAsync(cancellationToken) ?? throw OAuth2HttpException.ClosedEarly();

    private static OAuth2HttpException InvalidChunk(string message) => OAuth2HttpException.Malformed(OAuth2HttpFailure.InvalidChunk, message);
}
