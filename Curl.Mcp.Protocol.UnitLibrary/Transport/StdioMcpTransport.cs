using System.Buffers;
using Curl.Mcp.Protocol.JsonRpc;

namespace Curl.Mcp.Protocol.Transport;

/// <summary>
/// The Model Context Protocol's stdio transport over an input and an output <see cref="Stream" />: one UTF-8
/// JSON-RPC message per line, each terminated by <c>\n</c>. Reading skips blank lines, accepts <c>\r\n</c> and
/// delivers a final line left unterminated at the end of the input. Writing adds <c>\n</c>, never a byte order
/// mark, and flushes after each message under a lock, so concurrent writers never interleave.
/// </summary>
public sealed class StdioMcpTransport : IMcpTransport, IDisposable
{
    /// <summary>
    /// The longest line read, in bytes, terminator included: 4 MiB (4,194,304). The server's requests carry a
    /// curl command line and its tool arguments, far below this, so the limit only bounds the memory one line
    /// can take; a longer line is discarded up to its newline and read as an invalid request.
    /// </summary>
    public const int MaximumLineLength = 4 * 1024 * 1024;

    private const int ReadBufferSize = 64 * 1024;

    private static readonly JsonRpcError LineTooLong = new(JsonRpcErrorCodes.InvalidRequest, "Invalid Request", null);

    private static readonly byte[] Newline = [(byte)'\n'];

    private readonly Stream input;

    private readonly Stream output;

    private readonly JsonRpcMessageParser parser = new();

    private readonly SemaphoreSlim writeLock = new(1, 1);

    private readonly byte[] readBuffer = new byte[ReadBufferSize];

    private readonly ArrayBufferWriter<byte> line = new();

    private int unreadStart;

    private int unreadEnd;

    private bool lineIsTooLong;

    /// <summary>Initializes a new instance of the <see cref="StdioMcpTransport" /> class.</summary>
    /// <param name="input">The stream the client's messages are read from; the caller keeps ownership.</param>
    /// <param name="output">The stream the server's messages are written to; the caller keeps ownership.</param>
    public StdioMcpTransport(Stream input, Stream output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        this.input = input;
        this.output = output;
    }

    /// <inheritdoc />
    public async ValueTask<JsonRpcIncoming?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        while (await ReadLineAsync(cancellationToken))
        {
            JsonRpcIncoming? message = TakeLine();
            if (message is not null)
            {
                return message;
            }
        }

        return null;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="message" /> contains a newline.</exception>
    public async ValueTask WriteMessageAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        if (message.Span.Contains((byte)'\n'))
        {
            throw new ArgumentException("A message must not contain a newline.", nameof(message));
        }

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await output.WriteAsync(message, cancellationToken);
            await output.WriteAsync(Newline, cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>Releases the write lock. The streams belong to the caller and are left open.</summary>
    public void Dispose() => writeLock.Dispose();

    // True when a whole line, or a final unterminated one, is in the line buffer; false at the end of the input.
    private async ValueTask<bool> ReadLineAsync(CancellationToken cancellationToken)
    {
        while (!TryCompleteLineFromBuffer())
        {
            int read = await input.ReadAsync(readBuffer, cancellationToken);
            if (read == 0)
            {
                return line.WrittenCount > 0 || lineIsTooLong;
            }

            unreadStart = 0;
            unreadEnd = read;
        }

        return true;
    }

    private bool TryCompleteLineFromBuffer()
    {
        ReadOnlySpan<byte> unread = readBuffer.AsSpan(unreadStart, unreadEnd - unreadStart);
        int newline = unread.IndexOf((byte)'\n');
        if (newline < 0)
        {
            AppendToLine(unread);
            unreadStart = unreadEnd;
            return false;
        }

        AppendToLine(unread[..newline]);
        unreadStart += newline + 1;
        return true;
    }

    private void AppendToLine(ReadOnlySpan<byte> bytes)
    {
        if (lineIsTooLong)
        {
            return;
        }

        // The terminator counts toward the limit, so a line may hold one byte less than it.
        if (line.WrittenCount + bytes.Length >= MaximumLineLength)
        {
            lineIsTooLong = true;
            line.Clear();
            return;
        }

        line.Write(bytes);
    }

    // The line's message, or null for a blank line. JSON whitespace includes \r, so a \r\n line parses as is.
    private JsonRpcIncoming? TakeLine()
    {
        if (lineIsTooLong)
        {
            lineIsTooLong = false;
            return new JsonRpcInvalidMessage(LineTooLong, null);
        }

        ReadOnlyMemory<byte> bytes = line.WrittenMemory;
        JsonRpcIncoming? message = bytes.Span.IndexOfAnyExcept(" \t\r"u8) < 0 ? null : parser.Parse(bytes);
        line.ResetWrittenCount();
        return message;
    }
}
