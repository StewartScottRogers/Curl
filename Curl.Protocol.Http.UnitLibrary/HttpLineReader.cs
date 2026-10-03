using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads head lines from an <see cref="IConnection" />, however the peer's bytes are split
/// across reads, and keeps whatever arrives after the last line for the body.
/// </summary>
/// <param name="connection">The connection to read from.</param>
internal sealed class HttpLineReader(IConnection connection)
{
    /// <summary>
    /// The length, terminator included, at which curl 8.21.0 rejects one head line with exit
    /// 100: a line of 102399 bytes is accepted and one of 102400 is not (measured, BL-169).
    /// </summary>
    internal const int MaximumLineLength = 102400;

    private const int InitialBufferSize = 16384;

    private byte[] buffer = new byte[InitialBufferSize];

    private int start;

    private int end;

    /// <summary>
    /// Gets a value indicating whether any byte has arrived from the peer, which tells a
    /// connection that died before its response began from one that failed partway.
    /// </summary>
    internal bool HasReceived { get; private set; }

    /// <summary>
    /// Gets what is asked, before each read from the connection, whether the lines end here
    /// instead, as if the peer had closed; it is given the bytes of the unfinished line
    /// received so far, possibly none. By default the lines never end early.
    /// </summary>
    internal StopBeforeRead EndsBeforeRead { get; init; } = static _ => false;

    /// <summary>
    /// Decides whether the lines end before the next read from the connection.
    /// </summary>
    /// <param name="unfinishedLine">The bytes of the unfinished line received so far.</param>
    /// <returns><see langword="true" /> to end the lines as if the peer had closed.</returns>
    internal delegate bool StopBeforeRead(ReadOnlySpan<byte> unfinishedLine);

    /// <summary>
    /// Reads the next line, through its line feed.
    /// </summary>
    /// <param name="isStatusLine">
    /// <see langword="true" /> to reject the bytes as HTTP/0.9 as soon as they cannot begin
    /// <c>HTTP/</c>, before the line is whole, as curl does.
    /// </param>
    /// <param name="cancellationToken">Cancels every read.</param>
    /// <returns>
    /// The line, or <see langword="null" /> when the peer closed first or
    /// <see cref="EndsBeforeRead" /> ended the lines.
    /// </returns>
    /// <exception cref="HttpTransferException">
    /// The line reached <see cref="MaximumLineLength" /> (exit 100), a status line cannot
    /// begin <c>HTTP/</c> (exit 1), or a read failed (exit 56).
    /// </exception>
    internal async ValueTask<byte[]?> ReadLineAsync(bool isStatusLine, CancellationToken cancellationToken)
    {
        int scanned = 0;
        while (true)
        {
            int lineFeed = Array.IndexOf(buffer, (byte)'\n', start + scanned, end - start - scanned);
            int lineLength = lineFeed < 0 ? end - start : lineFeed - start + 1;
            RejectTooLong(lineLength);
            if (lineFeed >= 0)
            {
                return Take(lineLength);
            }

            if (isStatusLine)
            {
                HttpStatusLine.RejectHttp09(buffer.AsSpan(start, lineLength));
            }

            scanned = lineLength;
            if (EndsBeforeRead(buffer.AsSpan(start, lineLength)) || !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Reads until the bytes after the last line returned either cannot begin <c>HTTP/</c>, in
    /// any letter case, or are long enough to begin it, or until the peer closes; curl 8.21.0
    /// tells an HTTP/0.9 response from the first bytes that cannot begin a status line.
    /// </summary>
    /// <param name="cancellationToken">Cancels every read.</param>
    /// <returns><see langword="true" /> when the bytes cannot begin <c>HTTP/</c>.</returns>
    /// <exception cref="HttpTransferException">A read failed (exit 56).</exception>
    internal async ValueTask<bool> BeginsOtherThanHttpAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            int received = end - start;
            if (!HttpStatusLine.CanBeginHttp(buffer.AsSpan(start, received)))
            {
                return true;
            }

            if (received >= "HTTP/".Length || !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Takes every byte read past the last line returned.
    /// </summary>
    /// <returns>The bytes, possibly none.</returns>
    internal byte[] TakeRemaining() => Take(end - start);

    private static void RejectTooLong(int lineLength)
    {
        if (lineLength >= MaximumLineLength)
        {
            throw new HttpTransferException(CurlExitCode.TooLarge, HttpTransferMessages.LineTooLarge);
        }
    }

    private byte[] Take(int length)
    {
        byte[] taken = buffer.AsSpan(start, length).ToArray();
        start += length;
        return taken;
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        MakeRoom();
        int read;
        try
        {
            read = await connection.ReadAsync(buffer.AsMemory(end), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new HttpTransferException(CurlExitCode.RecvError, HttpTransferMessages.ReceiveFailure(exception));
        }

        end += read;
        HasReceived |= read > 0;
        return read > 0;
    }

    /// <summary>
    /// Leaves room after <see cref="end" /> for the next read: moves the unread bytes to the
    /// front when earlier lines were taken, or doubles the buffer when a line fills it.
    /// </summary>
    private void MakeRoom()
    {
        if (end < buffer.Length)
        {
            return;
        }

        if (start == 0)
        {
            Array.Resize(ref buffer, buffer.Length * 2);
            return;
        }

        buffer.AsSpan(start, end - start).CopyTo(buffer);
        end -= start;
        start = 0;
    }
}
