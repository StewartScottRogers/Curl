using System.Buffers;

namespace Curl.Extended.Protocol.McpClient.StreamableHttp;

/// <summary>
/// Reads the <c>data</c> of each event of a <c>text/event-stream</c> body (HTML Living Standard, "Server-sent events &gt;
/// Interpreting an event stream"): lines end in CRLF, LF or CR; a blank line dispatches the event; <c>event</c>, <c>id</c>,
/// <c>retry</c>, unknown fields and comments are ignored, since MCP carries each JSON-RPC message in an event's data.
/// </summary>
/// <param name="body">The response body.</param>
internal sealed class McpEventStreamReader(McpHttpBody body)
{
    private readonly byte[] buffer = new byte[16384];
    private int start;
    private int end;
    private bool skipLineFeed;

    /// <summary>Reads the next event that has data, joining its data lines with LF.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The event's data, or <see langword="null"/> when the stream ended; an event the stream ends inside is discarded.</returns>
    public async ValueTask<byte[]?> ReadEventDataAsync(CancellationToken cancellationToken)
    {
        var data = new ArrayBufferWriter<byte>();
        var hasData = false;
        while (await ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0 && hasData)
            {
                return data.WrittenSpan.ToArray();
            }

            var valueStart = FindDataValueStart(line);
            if (valueStart >= 0)
            {
                if (hasData)
                {
                    data.Write("\n"u8);
                }

                data.Write(line.AsSpan(valueStart));
                hasData = true;
            }
        }

        return null;
    }

    /// <summary>Finds where a <c>data</c> field's value starts: after the colon and one space, or at the end of a bare <c>data</c>.</summary>
    /// <returns>The value's start, or -1 when the line is not a <c>data</c> field.</returns>
    private static int FindDataValueStart(byte[] line)
    {
        if (line.AsSpan().SequenceEqual("data"u8))
        {
            return 4;
        }

        if (!line.AsSpan().StartsWith("data:"u8))
        {
            return -1;
        }

        return line.Length > 5 && line[5] == (byte)' ' ? 6 : 5;
    }

    private async ValueTask<byte[]?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new ArrayBufferWriter<byte>();
        while (start < end || await FillAsync(cancellationToken))
        {
            if (skipLineFeed && buffer[start] == (byte)'\n')
            {
                start++;
            }

            skipLineFeed = false;
            var terminator = TakeUntilLineEnd(line);
            if (terminator >= 0)
            {
                skipLineFeed = terminator == '\r';
                return line.WrittenSpan.ToArray();
            }
        }

        return null;
    }

    private int TakeUntilLineEnd(ArrayBufferWriter<byte> line)
    {
        var available = buffer.AsSpan(start, end - start);
        var index = available.IndexOfAny((byte)'\r', (byte)'\n');
        var take = index < 0 ? available.Length : index;
        if (line.WrittenCount + take > McpHttpConnectionReader.MaxLineLength)
        {
            throw new McpHttpException(McpHttpFailure.LineTooLong, $"An event stream line is longer than {McpHttpConnectionReader.MaxLineLength} bytes.");
        }

        line.Write(available[..take]);
        if (index < 0)
        {
            start = end;
            return -1;
        }

        start += index + 1;
        return available[index];
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        start = 0;
        end = await body.ReadAsync(buffer, cancellationToken);
        return end > 0;
    }
}
