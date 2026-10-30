using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Stomp.Wire;

/// <summary>
/// Reads STOMP 1.2 frames off a connection (STOMP 1.2, "STOMP Frames"): skips the EOLs a server sends between frames
/// as heart-beats ("Heart-beating"), takes lines ending in LF or CR LF, and reads the body as <c>content-length</c>
/// octets followed by NUL, or up to the first NUL when the header is absent.
/// </summary>
/// <param name="connection">The connection to read from.</param>
/// <param name="cancellationToken">Cancels every read.</param>
internal sealed class StompFrameReader(IConnection connection, CancellationToken cancellationToken)
{
    private const int InitialBufferSize = 4096;

    private byte[] buffer = new byte[InitialBufferSize];

    private int start;

    private int end;

    /// <summary>Reads the next frame.</summary>
    /// <returns>The frame, or <see langword="null"/> when the connection ended between frames.</returns>
    /// <exception cref="FormatException">The frame breaks the frame format.</exception>
    /// <exception cref="EndOfStreamException">The connection ended inside a frame.</exception>
    internal async ValueTask<StompFrame?> ReadAsync()
    {
        if (!await SkipHeartBeatsAsync().ConfigureAwait(false))
        {
            return null;
        }

        string command = await ReadLineAsync().ConfigureAwait(false);
        bool escaped = StompFrameEncoding.IsEscaped(command);
        List<StompHeader> headers = [];
        for (string line = await ReadLineAsync().ConfigureAwait(false); line.Length > 0; line = await ReadLineAsync().ConfigureAwait(false))
        {
            headers.Add(StompFrameEncoding.ParseHeader(line, escaped));
        }

        StompFrame frame = new(command, headers, []);
        return frame with { Body = await ReadBodyAsync(ContentLengthOf(frame)).ConfigureAwait(false) };
    }

    /// <summary>Gives the frame's <c>content-length</c>, or <see langword="null"/> when it has none.</summary>
    /// <param name="frame">The frame, its body not yet read.</param>
    /// <returns>The length.</returns>
    /// <exception cref="FormatException">The header is not a non-negative decimal integer.</exception>
    internal static int? ContentLengthOf(StompFrame frame) =>
        frame.HeaderValue("content-length") switch
        {
            null => null,
            string text when int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int length) => length,
            string text => throw new FormatException($"Invalid STOMP content-length: {text}"),
        };

    private async ValueTask<bool> SkipHeartBeatsAsync()
    {
        while (true)
        {
            if (start == end && !await FillAsync().ConfigureAwait(false))
            {
                return false;
            }

            if (buffer[start] is not ((byte)'\n' or (byte)'\r'))
            {
                return true;
            }

            start++;
        }
    }

    private async ValueTask<string> ReadLineAsync()
    {
        int newline = await IndexOfAsync((byte)'\n').ConfigureAwait(false);
        int length = newline > start && buffer[newline - 1] == '\r' ? newline - 1 - start : newline - start;
        string line = Encoding.UTF8.GetString(buffer, start, length);
        start = newline + 1;
        return line;
    }

    private async ValueTask<byte[]> ReadBodyAsync(int? contentLength)
    {
        int terminator = contentLength is { } length ? await NulAfterAsync(length).ConfigureAwait(false) : await IndexOfAsync(0).ConfigureAwait(false);
        byte[] body = buffer[start..terminator];
        start = terminator + 1;
        return body;
    }

    private async ValueTask<int> NulAfterAsync(int length)
    {
        while (end - start <= length)
        {
            await FillOrThrowAsync().ConfigureAwait(false);
        }

        return buffer[start + length] == 0 ? start + length : throw new FormatException("STOMP frame body not followed by NUL");
    }

    private async ValueTask<int> IndexOfAsync(byte value)
    {
        int searchedFromStart = 0;
        while (true)
        {
            int found = Array.IndexOf(buffer, value, start + searchedFromStart, end - start - searchedFromStart);
            if (found >= 0)
            {
                return found;
            }

            searchedFromStart = end - start;
            await FillOrThrowAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask FillOrThrowAsync()
    {
        if (!await FillAsync().ConfigureAwait(false))
        {
            throw new EndOfStreamException("The connection ended inside a STOMP frame");
        }
    }

    private async ValueTask<bool> FillAsync()
    {
        if (start > 0)
        {
            Array.Copy(buffer, start, buffer, 0, end - start);
            end -= start;
            start = 0;
        }

        if (end == buffer.Length)
        {
            Array.Resize(ref buffer, buffer.Length * 2);
        }

        int read = await connection.ReadAsync(buffer.AsMemory(end), cancellationToken).ConfigureAwait(false);
        end += read;
        return read > 0;
    }
}
