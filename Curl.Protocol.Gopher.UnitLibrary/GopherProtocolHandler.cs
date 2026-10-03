using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Serves the <c>gopher</c> and <c>gophers</c> schemes by sending the URL's selector and
/// writing the server's reply to the output unaltered, as curl 8.21.0 does.
/// </summary>
/// <remarks>
/// <para>
/// The connection comes from the injected <see cref="IConnector" /> (ADR-0005): port 70
/// for both schemes unless the URL names one, and <c>gophers</c> differs only in asking
/// for TLS. No <see cref="System.Net.Sockets.Socket" /> or <c>SslStream</c> is
/// constructed here.
/// </para>
/// <para>
/// The selector is built by <see cref="GopherSelector" /> and sent followed by CRLF; each
/// of the two pieces, once sent, is also written to
/// <see cref="ITransferContext.DumpHeaderOutput" /> when there is one (<c>-D</c>), as curl
/// passes them to its client as headers, and a refusal there is exit 23. The
/// reply is copied to <see cref="ITransferContext.Output" /> until the server closes the
/// connection, which ends the transfer with exit 0, even when nothing was received. Under
/// <see cref="ITransferContext.NoBody" /> (<c>-I</c>) the selector is sent and the reply is
/// never read, exit 0, as gopher has no headers for curl to receive (BL-1308). With
/// <see cref="ITransferContext.MaxFileSize" /> above 0 the reply is written up to that many
/// bytes, counted across reads, and a read that goes past it is cut there and ends the
/// transfer with exit 63 (<see cref="CurlExitCode.FilesizeExceeded" />); a reply exactly at
/// the limit is not a failure. A
/// selector that decodes to a NUL byte is exit 3 (<see cref="CurlExitCode.UrlMalformat" />),
/// found after connecting as in curl. A failed connect is returned unchanged; a failed
/// send is exit 55, a failed receive exit 56 and a failed output write exit 23, all
/// returned rather than thrown. The exit 23 message names the size of the read that failed
/// to write (at most 16384 bytes, as curl 8.21.0 reads gopher) and the bytes of it the
/// output accepted, from <see cref="OutputWriteFailedException.BytesAccepted" />.
/// Cancellation leaves as an exception. A <c>gophers</c> read that ends without the
/// server's <c>close_notify</c> is exit 56 with the TLS build's own text.
/// </para>
/// <para>
/// After connecting, <see cref="ITransferContext.Events" /> gets what curl 8.21.0's
/// <c>-v</c> and <c>--trace</c> show (measured, BL-934): each read as data received, the
/// server's close as a zero-byte block, and never the selector sent - curl does not trace
/// it. The connection then ends with <c>shutting down connection #N</c> for a finished
/// transfer or a malformed selector, and <c>closing connection #N</c> for any other
/// failure, after the failure's message unless it is curl's fallback text for a failed
/// send or receive. A failed send of the selector or its CRLF also reports
/// <c>Failed sending Gopher request</c> just before the closing line, as
/// <c>lib/gopher.c</c> does; its exit 55 message is <c>Send failure: Connection was reset</c>
/// for a reset and <c>Failed sending data to the peer</c> otherwise.
/// </para>
/// <para>
/// Each step goes to Curl's own diagnostic log, component <c>gopher</c> (ADR-0222, BL-928):
/// the selector sent and the bytes and milliseconds of a finished transfer as <c>info</c>,
/// and the failure that ends a transfer, with its <see cref="CurlExitCode" />, as
/// <c>error</c>. The log is also handed to the connector in
/// <see cref="ConnectTarget.DiagnosticLog" />.
/// </para>
/// </remarks>
public sealed class GopherProtocolHandler : IProtocolHandler
{
    private const int DefaultPort = 70;

    private const int ReadBufferSize = 16384;

    /// <summary>
    /// The two schemes this handler serves, as curl 8.21.0's <c>--version</c> protocol
    /// list names them.
    /// </summary>
    private static readonly string[] Schemes = ["gopher", "gophers"];

    private static readonly byte[] LineEnd = "\r\n"u8.ToArray();

    private readonly IConnector connector;

    /// <summary>
    /// Initializes a new instance of the <see cref="GopherProtocolHandler" /> class.
    /// </summary>
    /// <param name="connector">The connector each transfer's connection comes from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connector" /> is <see langword="null" />.</exception>
    public GopherProtocolHandler(IConnector connector)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        GopherDiagnosticLog log = new(context.DiagnosticLog);
        long started = context.TimeProvider.GetTimestamp();
        TransferResult result = await TransferAsync(context, log).ConfigureAwait(false);
        log.TransferEnded(result, context.TimeProvider.GetElapsedTime(started));
        return result;
    }

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, GopherDiagnosticLog log)
    {
        ConnectResult connected = await connector
            .ConnectAsync(CreateTarget(context), context.CancellationToken)
            .ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return new TransferResult(connected.ExitCode, 0, connected.ErrorMessage) { IsConnectionRefused = connected.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        TransferResult result;
        await using (connection.ConfigureAwait(false))
        {
            result = await ExchangeAsync(connection, context, log).ConfigureAwait(false);
        }

        ReportConnectionEnd(context.Events, result, connected.ConnectionNumber);
        return result;
    }

    private static async ValueTask<TransferResult> ExchangeAsync(IConnection connection, ITransferContext context, GopherDiagnosticLog log)
    {
        if (GopherSelector.FromUrl(context.Url) is not { } selector)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, GopherTransferMessages.SelectorMalformed);
        }

        foreach (byte[] piece in (byte[][])[selector, LineEnd])
        {
            if (await TrySendAsync(connection, piece, context.CancellationToken).ConfigureAwait(false) is { } sendFailure)
            {
                return TransferResult.Failure(CurlExitCode.SendError, SendFailure(sendFailure));
            }

            if (await TryDumpSentAsync(context.DumpHeaderOutput, piece, context.CancellationToken).ConfigureAwait(false) is { } refused)
            {
                return refused;
            }
        }

        log.SelectorSent(selector);

        // lib/transfer.c: gopher has no response-header writer, so under -I curl never
        // receives and the transfer ends once the selector is sent.
        if (context.NoBody)
        {
            return TransferResult.Success(0);
        }

        return await CopyReplyAsync(connection, context).ConfigureAwait(false);
    }

    /// <summary>
    /// Reports the line curl 8.21.0 ends a gopher connection with (measured, BL-934 Notes):
    /// <c>shutting down connection #N</c> after a finished transfer or a malformed selector,
    /// and otherwise <c>closing connection #N</c>, after the failure's own message when curl
    /// reports it through <c>failf</c> - every failure but the fallback texts for a failed
    /// send or receive - and, for a failed send, after <c>Failed sending Gopher request</c>.
    /// </summary>
    private static void ReportConnectionEnd(ITransferEvents events, TransferResult result, long connectionNumber)
    {
        if (result.ExitCode is CurlExitCode.Ok or CurlExitCode.UrlMalformat)
        {
            events.ReportInfo(GopherTransferMessages.ShuttingDownConnection(connectionNumber));
            return;
        }

        if (!IsFallbackText(result.ErrorMessage!))
        {
            events.ReportInfo(result.ErrorMessage!);
        }

        if (result.ExitCode == CurlExitCode.SendError)
        {
            events.ReportInfo(GopherTransferMessages.GopherRequestNotSent);
        }

        events.ReportInfo(GopherTransferMessages.ClosingConnection(connectionNumber));
    }

    /// <summary>
    /// Whether <paramref name="message" /> is the text curl prints for a failed send or
    /// receive without calling <c>failf</c>, and so without a <c>-v</c> line of its own.
    /// </summary>
    private static bool IsFallbackText(string message) =>
        message == GopherTransferMessages.SendFailed || message == GopherTransferMessages.ReceiveFailed;

    private static ConnectTarget CreateTarget(ITransferContext context)
    {
        CurlUrl url = context.Url;
        return new(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, url.Scheme == "gophers")
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
    }

    /// <summary>
    /// Sends one piece of the request: the selector, or its CRLF.
    /// </summary>
    /// <returns>
    /// <see langword="null" /> when the piece was sent, and what the connection threw when
    /// it was not.
    /// </returns>
    private static async ValueTask<IOException?> TrySendAsync(
        IConnection connection,
        byte[] piece,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(piece, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// The exit 55 message for a failed send: the socket filter's <c>Send failure:</c> text
    /// for a reset, and curl's fallback text for anything else.
    /// </summary>
    private static string SendFailure(IOException exception) =>
        exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset }
            ? GopherTransferMessages.SendConnectionReset
            : GopherTransferMessages.SendFailed;

    /// <summary>
    /// Writes a piece of the request just sent to the <c>-D</c> stream, as curl 8.21.0's
    /// <c>gopher_do</c> passes each piece it sends to the client as a header (measured,
    /// BL-1130): <c>-D</c> gets the selector and its CRLF, <c>-i</c> alone gets nothing.
    /// </summary>
    /// <returns>
    /// <see langword="null" /> when there is no <c>-D</c> stream or it took the piece, and an
    /// exit 23 result naming the piece's length when it refused it.
    /// </returns>
    private static async ValueTask<TransferResult?> TryDumpSentAsync(
        Stream? dumpHeaderOutput,
        byte[] piece,
        CancellationToken cancellationToken)
    {
        if (dumpHeaderOutput is null)
        {
            return null;
        }

        try
        {
            await dumpHeaderOutput.WriteAsync(piece, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException)
        {
            return TransferResult.Failure(CurlExitCode.WriteError, GopherTransferMessages.HeaderWriteFailed(piece.Length));
        }
    }

    /// <summary>
    /// Copies the reply to the output, reporting each read as data received and the
    /// server's close as a zero-byte block, as curl 8.21.0's <c>--trace</c> shows them.
    /// </summary>
    private static async ValueTask<TransferResult> CopyReplyAsync(IConnection connection, ITransferContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        byte[] buffer = new byte[ReadBufferSize];
        long limit = context.MaxFileSize is > 0 and long maxFileSize ? maxFileSize : long.MaxValue;
        long bytesWritten = 0;
        while (true)
        {
            int read;
            try
            {
                read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                return new TransferResult(CurlExitCode.RecvError, bytesWritten, ReceiveFailure(exception));
            }

            context.Events.ReportDataReceived(buffer.AsSpan(0, read));
            if (read == 0)
            {
                return TransferResult.Success(bytesWritten);
            }

            int allowed = (int)Math.Min(read, limit - bytesWritten);
            try
            {
                await context.Output.WriteAsync(buffer.AsMemory(0, allowed), cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                return new TransferResult(
                    CurlExitCode.WriteError,
                    bytesWritten,
                    GopherTransferMessages.OutputWriteFailed(allowed, BytesAcceptedBy(exception)));
            }

            bytesWritten += allowed;
            context.Progress.ReportDownloaded(bytesWritten, null);
            if (allowed < read)
            {
                return new TransferResult(
                    CurlExitCode.FilesizeExceeded,
                    bytesWritten,
                    GopherTransferMessages.MaxFileSizeExceeded(limit, bytesWritten));
            }
        }
    }

    /// <summary>
    /// The exit 56 message for a failed read: the TLS build's own text when a
    /// <c>gophers</c> connection ended without <c>close_notify</c> (ADR-0221), and curl's
    /// fallback text for anything else.
    /// </summary>
    private static string ReceiveFailure(IOException exception) =>
        exception is MissingCloseNotifyException ? exception.Message : GopherTransferMessages.ReceiveFailed;

    /// <summary>
    /// How many bytes of a failed write the output accepted: the count an
    /// <see cref="OutputWriteFailedException" /> carries, and 0 for any other
    /// <see cref="IOException" />.
    /// </summary>
    private static int BytesAcceptedBy(IOException exception) =>
        exception is OutputWriteFailedException failed ? failed.BytesAccepted : 0;
}
