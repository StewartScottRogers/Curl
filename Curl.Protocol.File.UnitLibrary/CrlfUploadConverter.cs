namespace Curl.Protocol.File;

/// <summary>
/// Converts line feeds to carriage-return line-feed pairs, one upload chunk at a time, for
/// <c>--crlf</c> (<see cref="Abstractions.ITransferContext.ConvertLineEndings" />).
/// </summary>
/// <remarks>
/// Measured on curl 8.21.0: a carriage return is inserted before a line feed only when the
/// byte before that line feed is not already a carriage return, so <c>a\r\nb</c> passes
/// unchanged and <c>a\r\r\nb\rc\n\n</c> becomes <c>a\r\r\nb\rc\r\n\r\n</c>. The byte before
/// is remembered between calls, so a carriage return ending one chunk and a line feed
/// starting the next are still one pair. One instance serves one upload.
/// </remarks>
/// <param name="maximumChunkSize">The largest chunk <see cref="Convert" /> will be given.</param>
internal sealed class CrlfUploadConverter(int maximumChunkSize)
{
    private const byte CarriageReturn = (byte)'\r';

    private const byte LineFeed = (byte)'\n';

    /// <summary>
    /// Holds one converted chunk, which is at most twice the size of the chunk given: a
    /// chunk of nothing but line feeds doubles.
    /// </summary>
    private readonly byte[] converted = new byte[maximumChunkSize * 2];

    private bool previousWasCarriageReturn;

    /// <summary>
    /// Converts one chunk.
    /// </summary>
    /// <param name="chunk">
    /// The chunk as read, no longer than the size this converter was built for.
    /// </param>
    /// <returns>
    /// The converted chunk, backed by a buffer this converter reuses: it is valid only until
    /// the next call.
    /// </returns>
    public ReadOnlyMemory<byte> Convert(ReadOnlyMemory<byte> chunk)
    {
        int length = 0;

        foreach (byte value in chunk.Span)
        {
            if (value == LineFeed && !previousWasCarriageReturn)
            {
                converted[length++] = CarriageReturn;
            }

            converted[length++] = value;
            previousWasCarriageReturn = value == CarriageReturn;
        }

        return converted.AsMemory(0, length);
    }
}
