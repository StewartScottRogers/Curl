using System.Globalization;

using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Dict;

/// <summary>
/// Serves the <c>dict</c> scheme: sends the lookup the URL's path names and writes the
/// server's whole reply to the output, unaltered, until the server closes the connection.
/// </summary>
/// <param name="connector">
/// Supplies the connection to the URL's host and port (ADR-0005). No
/// <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <remarks>
/// Matches curl 8.21.0, measured against a loopback listener. The port defaults to 2628.
/// The request, encoded by <see cref="DictRequest" />, is sent whole without waiting for
/// the server's greeting. A server that closes without sending anything ends the transfer
/// with exit 0 and nothing written. When <see cref="ITransferContext.Proxy" /> is set the
/// connection is tunnelled through it, <c>-p</c> or not; the connector opens the tunnel
/// (ADR-0056). A connect failure is returned as the connector reported it. A path that decodes to a control character is refused after connecting,
/// with exit 3 (<see cref="CurlExitCode.UrlMalformat" />), nothing sent and nothing written.
/// Each transfer writes Curl's own diagnostic log from <see cref="ITransferContext.DiagnosticLog" />
/// through <see cref="DictDiagnosticLog" />, and the connect target carries that log on (BL-928).
/// After connecting, <see cref="ITransferContext.Events" /> gets what curl 8.21.0's <c>-v</c>
/// and <c>--trace</c> show (measured, BL-934): the whole request as one block of data sent,
/// each read as data received, the server's close as a zero-byte block, and then
/// <c>shutting down connection #N</c>, which a refused path reports too. A <c>MATCH</c> or
/// <c>DEFINE</c> lookup whose word is empty or missing first reports <c>lookup word is
/// missing</c>, before the request is sent, as curl does (BL-1126).
/// A failed send is exit 55, a failed receive exit 56 and a failed output write exit 23,
/// all returned rather than thrown, with the texts of <see cref="DictIoFailures" />; such a
/// transfer reports its message (unless it is curl's fallback text), <c>Failed sending DICT
/// request</c> after a failed send, and <c>closing connection #N</c> (BL-1125). Cancellation
/// still leaves as an exception. Under <see cref="ITransferContext.NoBody" /> (<c>-I</c>) the
/// request is sent and the transfer ends with exit 0 without reading the reply; past
/// <see cref="ITransferContext.MaxFileSize" /> the read is cut to the bytes left under the
/// limit, they are written, and the transfer ends with exit 63
/// (<see cref="CurlExitCode.FilesizeExceeded" />) and <c>Exceeded the maximum allowed file
/// size (N) with N bytes</c>, as curl 8.21.0's <c>cw_download_write</c> does (BL-1309).
/// </remarks>
public sealed class DictProtocolHandler(IConnector connector) : IProtocolHandler
{
    /// <summary>The <c>-v</c> line curl 8.21.0 reports before sending a lookup whose word is empty or missing.</summary>
    private const string LookupWordMissingMessage = "lookup word is missing";

    /// <summary>The port a <c>dict</c> URL without one connects to.</summary>
    private const int DefaultPort = 2628;

    /// <summary>The most bytes read from the server at once.</summary>
    private const int BufferSize = 16384;

    private const string UrlMalformatMessage = "URL using bad/illegal format or missing URL";

    /// <summary>
    /// The one scheme this handler serves, as curl 8.21.0's <c>--version</c> protocol list
    /// names it.
    /// </summary>
    private static readonly string[] Schemes = ["dict"];

    private readonly IConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

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

        var log = new DictDiagnosticLog(context.DiagnosticLog);
        long started = context.TimeProvider.GetTimestamp();
        TransferResult result = await TransferAsync(context, log).ConfigureAwait(false);
        log.TransferEnded(result, context.TimeProvider.GetElapsedTime(started));
        return result;
    }

    private async Task<TransferResult> TransferAsync(ITransferContext context, DictDiagnosticLog log)
    {
        CurlUrl url = context.Url;
        var target = new ConnectTarget(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, false)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        TransferResult result;
        await using (connection.ConfigureAwait(false))
        {
            result = await ExchangeAsync(connection, context, log).ConfigureAwait(false);
        }

        ReportConnectionEnd(context.Events, result, connect.ConnectionNumber);
        return result;
    }

    /// <summary>
    /// Reports the lines curl 8.21.0 ends a dict connection with: <c>shutting down connection #N</c>
    /// after a finished transfer or a refused path, and otherwise the failure's message (unless
    /// it is curl's fallback text for a failed send or receive), <c>Failed sending DICT request</c>
    /// after a failed send, and <c>closing connection #N</c>.
    /// </summary>
    private static void ReportConnectionEnd(ITransferEvents events, TransferResult result, long connectionNumber)
    {
        if (result.ExitCode is CurlExitCode.Ok or CurlExitCode.UrlMalformat)
        {
            events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"shutting down connection #{connectionNumber}"));
            return;
        }

        if (!DictIoFailures.IsFallbackText(result.ErrorMessage!))
        {
            events.ReportInfo(result.ErrorMessage!);
        }

        if (result.ExitCode == CurlExitCode.SendError)
        {
            events.ReportInfo(DictIoFailures.DictRequestNotSent);
        }

        events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}"));
    }

    private static async Task<TransferResult> ExchangeAsync(IConnection connection, ITransferContext context, DictDiagnosticLog log)
    {
        if (!DictRequest.TryEncode(context.Url.AbsolutePath, out byte[] request, out bool wordMissing))
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, UrlMalformatMessage);
        }

        if (wordMissing)
        {
            context.Events.ReportInfo(LookupWordMissingMessage);
        }

        try
        {
            await connection.WriteAsync(request, context.CancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(context.CancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            return DictIoFailures.SendFailed(exception);
        }

        context.Events.ReportDataSent(request);
        log.CommandSent(request);
        if (context.NoBody)
        {
            return TransferResult.Success(0);
        }

        return await CopyReplyAsync(connection, context).ConfigureAwait(false);
    }

    private static async Task<TransferResult> CopyReplyAsync(IConnection connection, ITransferContext context)
    {
        var buffer = new byte[BufferSize];
        long? limit = context.MaxFileSize is > 0 ? context.MaxFileSize : null;
        long bytesWritten = 0;
        while (true)
        {
            int read;
            try
            {
                read = await connection.ReadAsync(buffer, context.CancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                return DictIoFailures.ReceiveFailed(bytesWritten, exception);
            }

            context.Events.ReportDataReceived(buffer.AsSpan(0, read));
            if (read == 0)
            {
                return TransferResult.Success(bytesWritten);
            }

            int allowed = AllowedBytes(limit, bytesWritten, read);
            try
            {
                await context.Output.WriteAsync(buffer.AsMemory(0, allowed), context.CancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                return DictIoFailures.WriteFailed(bytesWritten, allowed, exception);
            }

            bytesWritten += allowed;
            context.Progress.ReportDownloaded(bytesWritten, null);
            if (allowed < read)
            {
                return FileSizeExceeded(limit!.Value, bytesWritten);
            }
        }
    }

    /// <summary>
    /// How many of a read's <paramref name="count" /> bytes fit under <c>--max-filesize</c>
    /// <paramref name="limit" /> (<see langword="null" /> for none) after
    /// <paramref name="bytesWritten" />, as curl 8.21.0's <c>cw_download_write</c> cuts a write.
    /// </summary>
    private static int AllowedBytes(long? limit, long bytesWritten, int count) =>
        limit is { } max ? (int)Math.Clamp(max - bytesWritten, 0, count) : count;

    /// <summary>
    /// Makes the exit 63 result curl 8.21.0's <c>cw_download_write</c> fails with once a
    /// write was cut at <c>--max-filesize</c> <paramref name="limit" />.
    /// </summary>
    private static TransferResult FileSizeExceeded(long limit, long bytesWritten) =>
        new(
            CurlExitCode.FilesizeExceeded,
            bytesWritten,
            string.Create(CultureInfo.InvariantCulture, $"Exceeded the maximum allowed file size ({limit}) with {bytesWritten} bytes"));
}
