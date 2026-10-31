using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.SignalR.Transport;

/// <summary>
/// Reads an <see cref="IConnection"/> through a buffer, so an HTTP response head can be read line by line and the bytes
/// that arrive after it (a body, or the first WebSocket frames after a <c>101</c>) are kept for the next read.
/// </summary>
internal sealed class BufferedConnectionReader(IConnection connection)
{
    /// <summary>The longest line accepted, so a server cannot make the client buffer without end.</summary>
    public const int MaxLineLength = 64 * 1024;

    private byte[] buffer = new byte[4096];

    private int start;

    private int end;

    /// <summary>Reads one line ended by LF, without the LF or a CR before it, as Latin-1 text.</summary>
    /// <exception cref="SignalRTransportException">The connection ended first, or the line is too long.</exception>
    public async ValueTask<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        int lineFeed;
        while ((lineFeed = Array.IndexOf(buffer, (byte)'\n', start, end - start)) < 0)
        {
            if (end - start >= MaxLineLength)
            {
                throw new SignalRTransportException($"The server sent an HTTP line longer than {MaxLineLength} bytes.");
            }

            await FillOrThrowAsync("in the middle of an HTTP line", cancellationToken);
        }

        string line = Encoding.Latin1.GetString(buffer, start, lineFeed - start).TrimEnd('\r');
        start = lineFeed + 1;
        return line;
    }

    /// <summary>Reads exactly <paramref name="count"/> bytes.</summary>
    /// <exception cref="SignalRTransportException">The connection ended first.</exception>
    public async ValueTask<byte[]> ReadExactlyAsync(int count, CancellationToken cancellationToken)
    {
        while (end - start < count)
        {
            await FillOrThrowAsync("before an HTTP body ended", cancellationToken);
        }

        return Take(count);
    }

    /// <summary>Reads every byte until the server closes the connection.</summary>
    public async ValueTask<byte[]> ReadToEndAsync(CancellationToken cancellationToken)
    {
        while (await FillAsync(cancellationToken))
        {
        }

        return Take(end - start);
    }

    /// <summary>
    /// Gives the bytes already buffered, or when none are, the bytes of one read of the connection; empty once the
    /// server has closed it.
    /// </summary>
    public async ValueTask<byte[]> ReadAvailableAsync(CancellationToken cancellationToken)
    {
        if (end == start)
        {
            await FillAsync(cancellationToken);
        }

        return Take(end - start);
    }

    private byte[] Take(int count)
    {
        byte[] taken = buffer.AsSpan(start, count).ToArray();
        start += count;
        return taken;
    }

    private async ValueTask FillOrThrowAsync(string where, CancellationToken cancellationToken)
    {
        if (!await FillAsync(cancellationToken))
        {
            throw new SignalRTransportException($"The server closed the connection {where}.");
        }
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        Array.Copy(buffer, start, buffer, 0, end - start);
        end -= start;
        start = 0;
        if (end == buffer.Length)
        {
            Array.Resize(ref buffer, buffer.Length * 2);
        }

        int read = await connection.ReadAsync(buffer.AsMemory(end), cancellationToken);
        end += read;
        return read > 0;
    }
}
