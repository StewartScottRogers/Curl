using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Sends commands on an SMTP connection and reads its replies, a line at a time.
/// </summary>
/// <param name="connection">The connection; the caller owns and disposes it.</param>
/// <param name="events">
/// Where <c>-v</c> and <c>--trace</c> learn of each command sent, as a request header, each
/// line read, as a response header, and each piece of the message, as data sent.
/// </param>
/// <param name="cancellationToken">Cancels every send and read.</param>
/// <param name="diagnosticLog">
/// Where each command sent and each complete reply's code are logged at <c>verbose</c>
/// (ADR-0222), a command as its sender says it may be logged.
/// </param>
/// <param name="dumpHeaderOutput">
/// The <c>-D</c> stream, which gets every line read before <c>QUIT</c>, skipped or not, byte
/// for byte with its line end, as curl 8.21.0 writes each there (BL-1134); or
/// <see langword="null" /> without <c>-D</c>.
/// </param>
/// <remarks>
/// Commands and replies are Latin-1, so every byte of a percent-decoded <c>EHLO</c> domain
/// reaches the server unchanged, as curl sends it. As measured on curl 8.21.0 (BL-540): a
/// line ends at LF, with a CR before it dropped; a reply line starts with three digits; a
/// line whose fourth character is a space, or that is exactly four characters long before
/// its LF (<c>220</c> and a CR), ends the reply; a <c>-</c> there continues it; and any
/// other line is skipped. Every line read is reported with its line end, skipped or not, and
/// <c>QUIT</c> and its reply are not reported at all, as curl 8.21.0 sends them once the
/// transfer is over (BL-546).
/// </remarks>
internal sealed class SmtpControlChannel(
    IConnection connection,
    ITransferEvents events,
    CancellationToken cancellationToken,
    IDiagnosticLog diagnosticLog,
    Stream? dumpHeaderOutput = null)
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

    /// <summary>Where lines are reported: <c>events</c> until <see cref="QuitAsync" />, nowhere after.</summary>
    private ITransferEvents reporting = events;

    /// <summary>Where lines are dumped: the <c>-D</c> stream until <see cref="QuitAsync" />, nowhere after.</summary>
    private Stream? dumping = dumpHeaderOutput;

    /// <summary>
    /// Gets the <c>--trace-config smtp</c> lines the session writes as it steps (BL-1163);
    /// <see cref="SmtpStateTrace.Off" /> unless the handler traces its state machine.
    /// </summary>
    public SmtpStateTrace Trace { get; init; } = SmtpStateTrace.Off;

    /// <summary>
    /// Gets whether <see cref="QuitAsync" /> has sent <c>QUIT</c>, so a failed transfer ends
    /// with curl's <c>shutting down</c> line rather than its <c>closing</c> one.
    /// </summary>
    public bool QuitSent { get; private set; }

    /// <summary>
    /// Gets the connection commands are sent on and replies read from: the one the channel
    /// was built with until <see cref="SwitchTo(IConnection)" />.
    /// </summary>
    public IConnection Connection => connection;

    /// <summary>
    /// Gets the code of the last complete reply read, or 0 before the first.
    /// </summary>
    public int LastReplyCode { get; private set; }

    /// <summary>
    /// Carries on over <paramref name="secured" />, the connection after <c>STARTTLS</c>
    /// upgraded it to TLS.
    /// </summary>
    /// <param name="secured">The upgraded connection; the caller owns it.</param>
    public void SwitchTo(IConnection secured) => connection = secured;

    /// <summary>
    /// Sends <paramref name="command" /> followed by CRLF.
    /// </summary>
    /// <param name="command">The command line without its line end, such as <c>EHLO x</c>.</param>
    /// <param name="logged">
    /// What the diagnostic log says was sent when <paramref name="command" /> carries a
    /// credential, or <see langword="null" /> to log <paramref name="command" /> itself.
    /// </param>
    /// <returns>A task that completes once the command is sent.</returns>
    /// <exception cref="SmtpSendFailedException">The write failed with an <see cref="IOException" /> (exit 55).</exception>
    public async ValueTask SendAsync(string command, string? logged = null)
    {
        SmtpDiagnosticLogLines.CommandSent(diagnosticLog, logged ?? command);
        byte[] bytes = Encoding.Latin1.GetBytes(command + "\r\n");
        await WriteAsync(bytes).ConfigureAwait(false);
        reporting.ReportRequestHeader(bytes);
    }

    /// <summary>
    /// Sends <paramref name="bytes" /> as they are, such as a piece of the message after
    /// <c>DATA</c>, reported as data sent.
    /// </summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <returns>A task that completes once the bytes are sent.</returns>
    /// <exception cref="SmtpSendFailedException">The write failed with an <see cref="IOException" /> (exit 55).</exception>
    public async ValueTask SendBytesAsync(ReadOnlyMemory<byte> bytes)
    {
        await WriteAsync(bytes).ConfigureAwait(false);
        reporting.ReportDataSent(bytes.Span);
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
    /// <exception cref="SmtpNulByteInReplyException">
    /// A line held a NUL byte, as curl refuses with exit 8; the line is not reported.
    /// </exception>
    public ValueTask<SmtpReply?> ReadReplyAsync() => ReadReplyAsync(static _ => ValueTask.CompletedTask);

    /// <summary>
    /// Reads the next complete reply, handing each continuation line to
    /// <paramref name="continuationRead" /> as soon as it arrives, as curl writes the
    /// continuation lines of a command's reply before it has seen the final one (BL-543).
    /// </summary>
    /// <param name="continuationRead">
    /// Receives each continuation line exactly as it arrived, with its line end.
    /// </param>
    /// <returns>
    /// The reply, or <see langword="null" /> when the server closed the connection or a
    /// read failed with an <see cref="IOException" /> before the reply was complete.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// A line reached 65536 bytes, its CR and LF included, as curl refuses with exit 100.
    /// </exception>
    public async ValueTask<SmtpReply?> ReadReplyAsync(Func<string, ValueTask> continuationRead)
    {
        var lines = new List<string>();
        while (await ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (!StartsWithCode(line))
            {
                continue;
            }

            if (line.Length == 4 || line[3] == ' ')
            {
                lines.Add(line.TrimEnd('\r'));
                LastReplyCode = int.Parse(line.AsSpan(0, 3), provider: null);
                SmtpDiagnosticLogLines.ReplyRead(diagnosticLog, LastReplyCode);
                return new SmtpReply(LastReplyCode, lines) { FinalLine = line + "\n" };
            }

            if (line[3] == '-')
            {
                lines.Add(line.TrimEnd('\r'));
                await continuationRead(line + "\n").ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <summary>
    /// Sends <c>QUIT</c> and reads its reply, ignoring whatever it says and a reply line that
    /// is too long or holds a NUL byte, and a <c>QUIT</c> that cannot be sent, as curl does once
    /// the session is open. Neither is reported, and nothing
    /// is after them: curl sends <c>QUIT</c> once the transfer is over, where <c>-v</c> does
    /// not see it.
    /// </summary>
    /// <returns>A task that completes once the reply is read or the connection has closed.</returns>
    public async ValueTask QuitAsync()
    {
        reporting = NoTransferEvents.Instance;
        dumping = null;
        QuitSent = true;
        try
        {
            await SendAsync("QUIT").ConfigureAwait(false);
            await ReadReplyAsync().ConfigureAwait(false);
        }
        catch (SmtpSendFailedException)
        {
        }
        catch (InvalidDataException)
        {
        }
        catch (SmtpNulByteInReplyException)
        {
        }
    }

    /// <summary>Writes and flushes <paramref name="bytes" />, turning an <see cref="IOException" /> into an <see cref="SmtpSendFailedException" />.</summary>
    private async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            await connection.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure)
        {
            throw new SmtpSendFailedException(failure);
        }
    }

    private static bool StartsWithCode(string line) =>
        line.Length >= 4 && char.IsAsciiDigit(line[0]) && char.IsAsciiDigit(line[1]) && char.IsAsciiDigit(line[2]);

    /// <summary>
    /// Reads one line up to its LF, without the LF but with any CR before it, so that the
    /// caller can tell <c>220</c> and a CR, a complete reply, from <c>220</c> alone. The line
    /// is reported with its LF as a response header and written to the <c>-D</c> stream.
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
            line.Add(next);
            if (next == (byte)'\n')
            {
                byte[] bytes = [.. line];
                if (Array.IndexOf(bytes, (byte)0) >= 0)
                {
                    throw new SmtpNulByteInReplyException();
                }

                reporting.ReportResponseHeader(bytes);
                if (dumping is { } dump)
                {
                    await dump.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                }

                return Encoding.Latin1.GetString(bytes, 0, bytes.Length - 1);
            }
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
