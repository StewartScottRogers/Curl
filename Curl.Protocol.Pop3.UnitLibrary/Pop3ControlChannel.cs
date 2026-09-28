using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Sends commands on a POP3 connection and reads its responses, a line at a time.
/// </summary>
/// <param name="connection">The connection; the caller owns and disposes it.</param>
/// <param name="cancellationToken">Cancels every send and read.</param>
/// <remarks>
/// Commands and responses are Latin-1. As measured on curl 8.21.0 (BL-547): a line ends at
/// LF, with a CR before it dropped; a status line starts with <c>+</c> or <c>-ERR</c> and any
/// other line is skipped; while <c>CAPA</c> is answered every line counts, a line that is
/// exactly <c>.</c> ends the list and a line starting <c>-ERR</c> refuses it.
/// </remarks>
internal sealed class Pop3ControlChannel(IConnection connection, CancellationToken cancellationToken)
{
    private const int ReadBufferSize = 4096;

    /// <summary>
    /// The most bytes a line may hold before its LF, so that the line with its LF is at most
    /// 65535 bytes: measured on curl 8.21.0, one byte more fails with exit 100.
    /// </summary>
    private const int MaxLineBytes = 65535;

    private const string ErrorPrefix = "-ERR";

    private const string CapabilitiesEnd = ".";

    private readonly byte[] buffer = new byte[ReadBufferSize];

    private int bufferStart;

    private int bufferEnd;

    /// <summary>
    /// Gets the connection commands are sent on and responses read from: the one the
    /// channel was built with until <see cref="SwitchTo(IConnection)" />.
    /// </summary>
    public IConnection Connection => connection;

    /// <summary>
    /// Carries on over <paramref name="secured" />, the connection after <c>STLS</c>
    /// upgraded it to TLS.
    /// </summary>
    /// <param name="secured">The upgraded connection; the caller owns it.</param>
    public void SwitchTo(IConnection secured) => connection = secured;

    /// <summary>
    /// Sends <paramref name="command" /> followed by CRLF. A connection that fails with an
    /// <see cref="IOException" /> is left for the next read to find closed.
    /// </summary>
    /// <param name="command">The command line without its line end, such as <c>CAPA</c>.</param>
    /// <returns>A task that completes once the command is sent or the send has failed.</returns>
    public async ValueTask SendAsync(string command)
    {
        byte[] line = Encoding.Latin1.GetBytes(command + "\r\n");
        try
        {
            await connection.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// Reads the next status line, skipping every line that is not one.
    /// </summary>
    /// <returns>The status line.</returns>
    /// <exception cref="Pop3ReplyMissingException">The server closed the connection first.</exception>
    /// <exception cref="InvalidDataException">A line reached 65536 bytes, its CR and LF included.</exception>
    public async ValueTask<Pop3Response> ReadResponseAsync()
    {
        while (true)
        {
            string line = await ReadLineAsync().ConfigureAwait(false);
            if (line.StartsWith('+') || line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
            {
                return new Pop3Response(line);
            }
        }
    }

    /// <summary>
    /// Reads the answer to <c>CAPA</c>: every line up to the one that is exactly <c>.</c>.
    /// </summary>
    /// <returns>
    /// The capability lines, or <see langword="null" /> when a line starting <c>-ERR</c>
    /// refused the command; the lines after it are left unread, as curl leaves them.
    /// </returns>
    /// <exception cref="Pop3ReplyMissingException">The server closed the connection first.</exception>
    /// <exception cref="InvalidDataException">A line reached 65536 bytes, its CR and LF included.</exception>
    public async ValueTask<Pop3Capabilities?> ReadCapabilitiesAsync()
    {
        var lines = new List<string>();
        while (true)
        {
            string line = await ReadLineAsync().ConfigureAwait(false);
            if (line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            if (line == CapabilitiesEnd)
            {
                return new Pop3Capabilities(lines);
            }

            lines.Add(line);
        }
    }

    /// <summary>
    /// Reads one line up to its LF, without the LF or a CR before it.
    /// </summary>
    private async ValueTask<string> ReadLineAsync()
    {
        var line = new List<byte>();
        while (bufferStart < bufferEnd || await TryFillAsync().ConfigureAwait(false))
        {
            if (line.Count >= MaxLineBytes)
            {
                throw new InvalidDataException("A POP3 response line reached 65536 bytes.");
            }

            byte next = buffer[bufferStart++];
            if (next == (byte)'\n')
            {
                int length = line.Count > 0 && line[^1] == (byte)'\r' ? line.Count - 1 : line.Count;
                return Encoding.Latin1.GetString([.. line], 0, length);
            }

            line.Add(next);
        }

        throw new Pop3ReplyMissingException();
    }

    private async ValueTask<bool> TryFillAsync()
    {
        bufferStart = 0;
        try
        {
            bufferEnd = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            bufferEnd = 0;
        }

        return bufferEnd > 0;
    }
}
