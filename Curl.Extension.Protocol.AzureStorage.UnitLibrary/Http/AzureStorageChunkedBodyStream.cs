using System.Globalization;

namespace Curl.Extension.Protocol.AzureStorage.Http;

/// <summary>A body in the chunked transfer coding (RFC 9112 §7.1): chunks until a zero-size one, then trailer fields, which are discarded.</summary>
/// <param name="reader">The connection's reader.</param>
internal sealed class AzureStorageChunkedBodyStream(AzureStorageHttpConnectionReader reader) : AzureStorageHttpBodyStream
{
    private long remainingInChunk;
    private bool ended;

    /// <inheritdoc/>
    protected override async ValueTask<int> ReadBodyAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (remainingInChunk == 0 && (ended || !await StartNextChunkAsync(cancellationToken)))
        {
            return 0;
        }

        var read = await reader.ReadAsync(buffer[..(int)Math.Min(buffer.Length, remainingInChunk)], cancellationToken);
        if (read == 0)
        {
            throw ClosedEarly();
        }

        remainingInChunk -= read;
        if (remainingInChunk == 0 && (await ReadRequiredLineAsync(cancellationToken)).Length > 0)
        {
            throw InvalidChunk("A chunk's data is not followed by CRLF.");
        }

        return read;
    }

    private static AzureStorageHttpException InvalidChunk(string message) => new(AzureStorageHttpFailure.InvalidChunk, message);

    private static bool TryParseChunkSize(string line, out long size)
    {
        var sizeEnd = line.AsSpan().IndexOfAny(';', ' ', '\t');
        var digits = sizeEnd < 0 ? line.AsSpan() : line.AsSpan(0, sizeEnd);
        return long.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out size) && size >= 0;
    }

    private async ValueTask<string> ReadRequiredLineAsync(CancellationToken cancellationToken) =>
        await reader.ReadLineAsync(cancellationToken) ?? throw ClosedEarly();

    private async ValueTask<bool> StartNextChunkAsync(CancellationToken cancellationToken)
    {
        var line = await ReadRequiredLineAsync(cancellationToken);
        if (!TryParseChunkSize(line, out remainingInChunk))
        {
            throw InvalidChunk($"The chunk size line \"{line}\" is not a hexadecimal size.");
        }

        if (remainingInChunk > 0)
        {
            return true;
        }

        while ((await ReadRequiredLineAsync(cancellationToken)).Length > 0)
        {
        }

        ended = true;
        return false;
    }
}
