namespace Curl.Core;

/// <summary>
/// Reads a file line by line the way curl 8.22.0's <c>Curl_get_line</c>
/// (<c>lib/curl_get_line.c</c>) does for cookie, alt-svc, HSTS and netrc files: every line it
/// returns ends with a newline, a last line without one gets one, a line that does not fit the
/// buffer limit is reported as too large, and every byte, <c>^Z</c> (0x1A) included, is kept.
/// </summary>
/// <remarks>
/// <para>
/// curl reads with <c>fgets</c> into a 128-byte buffer, so it takes the file in chunks of at
/// most 127 bytes, each ending at a newline, and appends each to a <c>dynbuf</c> whose limit is
/// the caller's (4096 in <c>unit3200.c</c>). A <c>dynbuf</c> holds at most one byte fewer than
/// its limit, so a line, newline included, of <c>bufferLimit</c> bytes or more fails: curl frees
/// the buffer and returns <c>CURLE_TOO_LARGE</c>, which here is <see langword="null" /> from
/// <see cref="ReadLineAsync" />. The file position is then just past the chunk that did not fit,
/// as curl's is; curl's callers stop reading there.
/// </para>
/// <para>
/// <see cref="EndOfFile" /> is <c>feof</c>: it turns true once a read has met the end of the
/// file, not when the last newline was read. So after a file's last <c>LINE\n</c>, one more read
/// meets the end with nothing in the buffer and, as in curl, returns a lone <c>\n</c> with
/// <see cref="EndOfFile" /> true.
/// </para>
/// </remarks>
public sealed class CurlFileLineReader
{
    /// <summary>The <c>dynbuf</c> limit <c>unit3200.c</c> reads with.</summary>
    public const int DefaultBufferLimit = 4096;

    /// <summary>The most bytes one <c>fgets</c> into curl's 128-byte buffer returns.</summary>
    private const int ChunkLength = 127;

    private const byte LineFeed = (byte)'\n';

    private readonly Stream input;
    private readonly int bufferLimit;
    private readonly byte[] readBuffer = new byte[4096];
    private int readStart;
    private int readEnd;

    /// <summary>Starts reading <paramref name="input" /> from its current position.</summary>
    /// <param name="input">The file's bytes.</param>
    /// <param name="bufferLimit">
    /// The <c>dynbuf</c> limit: a line of this many bytes or more, its newline included, is too large.
    /// </param>
    public CurlFileLineReader(Stream input, int bufferLimit = DefaultBufferLimit)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferLimit);
        this.input = input;
        this.bufferLimit = bufferLimit;
    }

    /// <summary>True once a read has met the end of the file (curl's <c>feof</c>).</summary>
    public bool EndOfFile { get; private set; }

    /// <summary>
    /// Reads the next line, ending in a newline, or <see langword="null" /> when it does not fit
    /// the buffer limit (curl's <c>CURLE_TOO_LARGE</c>, with the buffer left empty).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The line's bytes, its newline included, or <see langword="null" /> when too large.</returns>
    public async ValueTask<byte[]?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var line = new List<byte>();
        while (true)
        {
            var chunk = await ReadChunkAsync(cancellationToken).ConfigureAwait(false);
            if (line.Count + chunk.Count >= bufferLimit)
            {
                return null;
            }

            line.AddRange(chunk);
            if (line.Count > 0 && line[^1] == LineFeed)
            {
                return [.. line];
            }

            if (EndOfFile)
            {
                return line.Count + 1 >= bufferLimit ? null : [.. line, LineFeed];
            }
        }
    }

    /// <summary>One <c>fgets</c>: up to <see cref="ChunkLength" /> bytes, ending after a newline.</summary>
    private async ValueTask<List<byte>> ReadChunkAsync(CancellationToken cancellationToken)
    {
        var chunk = new List<byte>(ChunkLength);
        while (chunk.Count < ChunkLength)
        {
            if (readStart == readEnd && !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                EndOfFile = true;
                break;
            }

            var next = readBuffer[readStart++];
            chunk.Add(next);
            if (next == LineFeed)
            {
                break;
            }
        }

        return chunk;
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        readStart = 0;
        readEnd = await input.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
        return readEnd > 0;
    }
}
