using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Sends commands on an FTP control connection and reads its replies, a line at a time.
/// </summary>
/// <param name="connection">The control connection; the caller owns and disposes it.</param>
/// <param name="cancellationToken">Cancels every send and read, until <see cref="CancellationToken" /> is set.</param>
/// <remarks>
/// Commands and replies are Latin-1, so every byte of a percent-decoded path reaches the
/// server unchanged, as curl sends it. A reply ends at the first line that starts with
/// three digits and a space; the lines before it, such as the <c>220-</c> lines of a
/// multi-line greeting or a bare <c>331</c>, are skipped, as curl skips them. A line ends
/// at LF, with a CR before it dropped.
/// </remarks>
internal sealed class FtpControlChannel(IConnection connection, CancellationToken cancellationToken)
{
    private const int ReadBufferSize = 4096;

    /// <summary>
    /// Gets the connection commands are sent on and replies read from: the one the channel
    /// was built with until <see cref="SwitchTo(IConnection)" />.
    /// </summary>
    public IConnection Connection => connection;

    /// <summary>
    /// Carries on over <paramref name="secured" />, the control connection after
    /// <c>AUTH</c> upgraded it to TLS.
    /// </summary>
    /// <param name="secured">The upgraded connection; the caller owns it.</param>
    public void SwitchTo(IConnection secured) => connection = secured;

    /// <summary>
    /// Gets or sets the token that cancels every send and read: the connect phase's limit
    /// while the session logs in, the transfer's afterwards (BL-512).
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = cancellationToken;

    /// <summary>
    /// The most bytes a reply line may hold before its LF, so that the line with its LF is
    /// at most 65535 bytes: measured on curl 8.21.0, one byte more fails with exit 100.
    /// </summary>
    private const int MaxLineBytes = 65535;

    private readonly byte[] buffer = new byte[ReadBufferSize];

    private int bufferStart;

    private int bufferEnd;

    /// <summary>
    /// Sends <paramref name="command" /> followed by CRLF.
    /// </summary>
    /// <param name="command">The command line without its line end, such as <c>TYPE I</c>.</param>
    /// <returns>
    /// <see langword="true" /> when it was sent; <see langword="false" /> when the
    /// connection failed with an <see cref="IOException" />.
    /// </returns>
    public async ValueTask<bool> TrySendAsync(string command)
    {
        byte[] line = Encoding.Latin1.GetBytes(command + "\r\n");
        try
        {
            await connection.WriteAsync(line, CancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(CancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the next complete reply.
    /// </summary>
    /// <returns>
    /// The reply, or <see langword="null" /> when the server closed the connection or a
    /// read failed with an <see cref="IOException" /> before the reply was complete.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// A line reached 65536 bytes, its CR and LF included, as curl refuses with exit 100.
    /// </exception>
    public async ValueTask<FtpReply?> ReadReplyAsync()
    {
        while (await ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (TryParseLastLine(line, out int code))
            {
                return new FtpReply(code, line);
            }
        }

        return null;
    }

    private static bool TryParseLastLine(string line, out int code)
    {
        code = 0;
        return line.Length >= 4
            && line[3] == ' '
            && int.TryParse(line.AsSpan(0, 3), NumberStyles.None, CultureInfo.InvariantCulture, out code);
    }

    private async ValueTask<string?> ReadLineAsync()
    {
        var line = new List<byte>();
        while (bufferStart < bufferEnd || await TryFillAsync().ConfigureAwait(false))
        {
            if (line.Count >= MaxLineBytes)
            {
                throw new InvalidDataException("An FTP reply line reached 65536 bytes.");
            }

            byte next = buffer[bufferStart++];
            if (next == (byte)'\n')
            {
                return Encoding.Latin1.GetString([.. line]).TrimEnd('\r');
            }

            line.Add(next);
        }

        return null;
    }

    private async ValueTask<bool> TryFillAsync()
    {
        bufferStart = 0;
        try
        {
            bufferEnd = await connection.ReadAsync(buffer, CancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            bufferEnd = 0;
        }

        return bufferEnd > 0;
    }
}
