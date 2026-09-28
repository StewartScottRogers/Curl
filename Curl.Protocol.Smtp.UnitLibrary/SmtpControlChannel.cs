using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Sends commands on an SMTP connection and reads its replies, a line at a time.
/// </summary>
/// <param name="connection">The connection; the caller owns and disposes it.</param>
/// <param name="cancellationToken">Cancels every send and read.</param>
/// <remarks>
/// Commands and replies are Latin-1, so every byte of a percent-decoded <c>EHLO</c> domain
/// reaches the server unchanged, as curl sends it. As measured on curl 8.21.0 (BL-540): a
/// line ends at LF, with a CR before it dropped; a reply line starts with three digits; a
/// line whose fourth character is a space, or that is exactly four characters long before
/// its LF (<c>220</c> and a CR), ends the reply; a <c>-</c> there continues it; and any
/// other line is skipped.
/// </remarks>
internal sealed class SmtpControlChannel(IConnection connection, CancellationToken cancellationToken)
{
    private const int ReadBufferSize = 4096;

    /// <summary>
    /// The most bytes a reply line may hold before its LF, so that the line with its LF is
    /// at most 65535 bytes: measured on curl 8.21.0, one byte more fails with exit 100.
    /// </summary>
    private const int MaxLineBytes = 65535;

    private readonly byte[] buffer = new byte[ReadBufferSize];

    private int bufferStart;

    private int bufferEnd;

    /// <summary>
    /// Gets the connection commands are sent on and replies read from: the one the channel
    /// was built with until <see cref="SwitchTo(IConnection)" />.
    /// </summary>
    public IConnection Connection => connection;

    /// <summary>
    /// Carries on over <paramref name="secured" />, the connection after <c>STARTTLS</c>
    /// upgraded it to TLS.
    /// </summary>
    /// <param name="secured">The upgraded connection; the caller owns it.</param>
    public void SwitchTo(IConnection secured) => connection = secured;

    /// <summary>
    /// Sends <paramref name="command" /> followed by CRLF. A connection that fails with an
    /// <see cref="IOException" /> is left for the next <see cref="ReadReplyAsync" /> to find
    /// closed.
    /// </summary>
    /// <param name="command">The command line without its line end, such as <c>EHLO x</c>.</param>
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
    /// Reads the next complete reply.
    /// </summary>
    /// <returns>
    /// The reply, or <see langword="null" /> when the server closed the connection or a
    /// read failed with an <see cref="IOException" /> before the reply was complete.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// A line reached 65536 bytes, its CR and LF included, as curl refuses with exit 100.
    /// </exception>
    public async ValueTask<SmtpReply?> ReadReplyAsync()
    {
        var lines = new List<string>();
        while (await ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (!StartsWithCode(line))
            {
                continue;
            }

            bool isFinal = line.Length == 4 || line[3] == ' ';
            if (isFinal || line[3] == '-')
            {
                lines.Add(line.TrimEnd('\r'));
            }

            if (isFinal)
            {
                return new SmtpReply(int.Parse(line.AsSpan(0, 3), provider: null), lines);
            }
        }

        return null;
    }

    private static bool StartsWithCode(string line) =>
        line.Length >= 4 && char.IsAsciiDigit(line[0]) && char.IsAsciiDigit(line[1]) && char.IsAsciiDigit(line[2]);

    /// <summary>
    /// Reads one line up to its LF, without the LF but with any CR before it, so that the
    /// caller can tell <c>220</c> and a CR, a complete reply, from <c>220</c> alone.
    /// </summary>
    private async ValueTask<string?> ReadLineAsync()
    {
        var line = new List<byte>();
        while (bufferStart < bufferEnd || await TryFillAsync().ConfigureAwait(false))
        {
            if (line.Count >= MaxLineBytes)
            {
                throw new InvalidDataException("An SMTP reply line reached 65536 bytes.");
            }

            byte next = buffer[bufferStart++];
            if (next == (byte)'\n')
            {
                return Encoding.Latin1.GetString([.. line]);
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
            bufferEnd = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            bufferEnd = 0;
        }

        return bufferEnd > 0;
    }
}
