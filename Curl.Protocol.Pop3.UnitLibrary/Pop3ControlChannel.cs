using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Sends commands on a POP3 connection and reads its responses, a line at a time.
/// </summary>
/// <param name="connection">The connection; the caller owns and disposes it.</param>
/// <param name="events">
/// Where <c>-v</c> and <c>--trace</c> learn of each command sent, as a request header, and
/// each line read, as a response header.
/// </param>
/// <param name="cancellationToken">Cancels every send and read.</param>
/// <param name="diagnosticLog">
/// Where each command sent and each status read are logged at <c>verbose</c> (ADR-0222), a
/// command as its sender says it may be logged; <see langword="null" /> logs nothing.
/// </param>
/// <param name="dumpHeaderOutput">
/// The <c>-D</c> stream, which gets every line read, byte for byte with its line end, until
/// <see cref="StopReporting" />, as curl 8.21.0 writes each response line there (BL-1133);
/// <see langword="null" /> writes nothing.
/// </param>
/// <remarks>
/// Commands and responses are Latin-1. As measured on curl 8.21.0 (BL-547): a line ends at
/// LF, with a CR before it dropped; a status line starts with <c>+</c> or <c>-ERR</c> and any
/// other line is skipped; while <c>CAPA</c> is answered every line counts, a line that is
/// exactly <c>.</c> ends the list and a line starting <c>-ERR</c> refuses it. Every command
/// sent is reported with its CRLF and every line read with its line end, skipped or not, until
/// <see cref="StopReporting" />; a body's bytes are not reported here (BL-552).
/// </remarks>
internal sealed class Pop3ControlChannel(
    IConnection connection, ITransferEvents events, CancellationToken cancellationToken, IDiagnosticLog? diagnosticLog = null, Stream? dumpHeaderOutput = null)
{
    private const int ReadBufferSize = 4096;

    /// <summary>
    /// The most bytes a line may hold before its LF, so that the line with its LF is at most
    /// 65535 bytes: measured on curl 8.21.0, one byte more fails with exit 100.
    /// </summary>
    private const int MaxLineBytes = 65535;

    private const string ErrorPrefix = "-ERR";

    private const string CapabilitiesEnd = ".";

    private readonly IDiagnosticLog log = diagnosticLog ?? NoDiagnosticLog.Instance;

    private readonly byte[] buffer = new byte[ReadBufferSize];

    private int bufferStart;

    private int bufferEnd;

    /// <summary>Where lines are reported: <c>events</c> until <see cref="StopReporting" />, nowhere after.</summary>
    private ITransferEvents reporting = events;

    /// <summary>Where lines are written for <c>-D</c>: <c>dumpHeaderOutput</c> until <see cref="StopReporting" />, nowhere after.</summary>
    private Stream? dumping = dumpHeaderOutput;

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
    /// Reports nothing more: curl sends <c>QUIT</c> once the transfer is over, where <c>-v</c>
    /// does not see it or its answer, and its answer is not written to <c>-D</c> either.
    /// </summary>
    public void StopReporting()
    {
        reporting = NoTransferEvents.Instance;
        dumping = null;
    }

    /// <summary>
    /// Sends <paramref name="command" /> followed by CRLF, reported as a request header once
    /// sent. A connection that fails with an <see cref="IOException" /> is left for the next
    /// read to find closed, and the command is not reported.
    /// </summary>
    /// <param name="command">The command line without its line end, such as <c>CAPA</c>.</param>
    /// <param name="logged">
    /// What the diagnostic log says was sent when <paramref name="command" /> carries a
    /// credential, or <see langword="null" /> to log <paramref name="command" /> itself.
    /// </param>
    /// <returns>A task that completes once the command is sent or the send has failed.</returns>
    public async ValueTask SendAsync(string command, string? logged = null)
    {
        Pop3DiagnosticLogLines.CommandSent(log, logged ?? command);
        byte[] line = Encoding.Latin1.GetBytes(command + "\r\n");
        try
        {
            await connection.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return;
        }

        reporting.ReportRequestHeader(line);
    }

    /// <summary>
    /// Reads the next status line, skipping every line that is not one.
    /// </summary>
    /// <returns>The status line.</returns>
    /// <exception cref="Pop3ReplyMissingException">The server closed the connection first.</exception>
    /// <exception cref="InvalidDataException">A line reached 65536 bytes, its CR and LF included.</exception>
    /// <exception cref="Pop3NulByteInLineException">A line held a NUL byte.</exception>
    public async ValueTask<Pop3Response> ReadResponseAsync()
    {
        while (true)
        {
            string line = await ReadLineAsync().ConfigureAwait(false);
            if (line.StartsWith('+') || line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
            {
                var response = new Pop3Response(line);
                Pop3DiagnosticLogLines.ResponseRead(log, response);
                return response;
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
    /// <exception cref="Pop3NulByteInLineException">A line held a NUL byte.</exception>
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
    /// Reads the next chunk of a multi-line body: the bytes left over after the status line
    /// when there are any, as curl hands on the rest of the read that carried it, else what
    /// the next read returns.
    /// </summary>
    /// <returns>
    /// The chunk, valid until the next read on this channel; empty once the server has closed
    /// the connection or a read has failed.
    /// </returns>
    public async ValueTask<ReadOnlyMemory<byte>> ReadChunkAsync()
    {
        if (bufferStart == bufferEnd && !await TryFillAsync().ConfigureAwait(false))
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        ReadOnlyMemory<byte> chunk = buffer.AsMemory(bufferStart, bufferEnd - bufferStart);
        bufferStart = bufferEnd;
        return chunk;
    }

    /// <summary>
    /// Reads one line up to its LF, without the LF or a CR before it. The line is reported
    /// with its line end as a response header, unless it holds a NUL byte: then it is not
    /// reported and <see cref="Pop3NulByteInLineException" /> is thrown, as curl 8.21.0
    /// checks every response line before <c>-v</c> shows it (BL-1120).
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
            line.Add(next);
            if (next == (byte)'\n')
            {
                return await EndLineAsync(line).ConfigureAwait(false);
            }
        }

        throw new Pop3ReplyMissingException();
    }

    /// <summary>
    /// Ends <paramref name="line" />, which holds its LF: refuses it when it holds a NUL byte,
    /// else reports it as a response header, writes it to the <c>-D</c> stream when there is
    /// one, and returns it without its LF or a CR before it.
    /// </summary>
    private async ValueTask<string> EndLineAsync(List<byte> line)
    {
        if (line.Contains(0))
        {
            throw new Pop3NulByteInLineException();
        }

        int length = line.Count > 1 && line[^2] == (byte)'\r' ? line.Count - 2 : line.Count - 1;
        byte[] bytes = [.. line];
        reporting.ReportResponseHeader(bytes);
        if (dumping is not null)
        {
            await dumping.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        return Encoding.Latin1.GetString(bytes, 0, length);
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
