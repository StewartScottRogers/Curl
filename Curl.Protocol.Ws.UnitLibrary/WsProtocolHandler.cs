using System.Globalization;
using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Serves the <c>ws</c> and <c>wss</c> schemes: connects, sends curl's HTTP/1.1 upgrade request
/// and reads the reply head, accepting the upgrade on a <c>101</c> (ADR-0128); after it, sends
/// any <c>-T</c> upload as one frame and writes every frame's payload to the output until the
/// server closes the connection (ADR-0131).
/// </summary>
/// <param name="connector">
/// Supplies the connection to the URL's host and port, over TLS for <c>wss</c> and tunnelled
/// through <see cref="ITransferContext.Proxy" /> when one is set. No
/// <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <param name="authenticator">
/// Builds the pre-emptive <c>Authorization</c> value for <c>-u</c>, <c>--basic</c> and
/// <c>--oauth2-bearer</c>, with no challenges.
/// </param>
/// <param name="randomSource">Supplies the 16 bytes behind <c>Sec-WebSocket-Key</c>.</param>
/// <remarks>
/// Matches curl 8.21.0, measured against a loopback listener (ADR-0128, BL-580). Any status
/// but <c>101</c> fails with exit 22, <c>Refused WebSocket upgrade: &lt;code&gt;</c>, and
/// nothing written to the output; <c>Sec-WebSocket-Accept</c>, <c>Upgrade</c> and
/// <c>Connection</c> in a <c>101</c> are not checked, because curl does not check them. The
/// reply head, <c>101</c> or not, is written to <see cref="ITransferContext.HeaderOutput" />
/// (<c>-D</c>). A connect failure is returned as the connector reported it.
/// </remarks>
public sealed class WsProtocolHandler(
    IConnector connector,
    IHttpAuthenticator authenticator,
    IWebSocketRandomSource randomSource) : IProtocolHandler
{
    /// <summary>The status code that accepts the upgrade.</summary>
    internal const int SwitchingProtocols = 101;

    /// <summary>How many random bytes <c>Sec-WebSocket-Key</c> encodes.</summary>
    private const int KeyLength = 16;

    /// <summary>
    /// The two schemes this handler serves, as curl 8.21.0's <c>--version</c> protocol list
    /// names them.
    /// </summary>
    private static readonly string[] Schemes = ["ws", "wss"];

    private readonly IConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

    private readonly IHttpAuthenticator authenticator =
        authenticator ?? throw new ArgumentNullException(nameof(authenticator));

    private readonly IWebSocketRandomSource randomSource =
        randomSource ?? throw new ArgumentNullException(nameof(randomSource));

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

        CurlUrl url = context.Url;
        var target = new ConnectTarget(url.IdnHost, url.Port, url.Scheme == "wss")
        {
            Proxy = context.Proxy,
            Events = context.Events,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        context.Events.ReportInfo(WsInfoLines.UsingHttp1);
        await using (connection.ConfigureAwait(false))
        {
            try
            {
                return await UpgradeAsync(connection, context, connect.ConnectionNumber).ConfigureAwait(false);
            }
            catch (WsTransferException failure)
            {
                context.Events.ReportInfo(failure.Message);
                context.Events.ReportInfo(WsInfoLines.Closing(connect.ConnectionNumber));
                return TransferResult.Failure(failure.ExitCode, failure.Message);
            }
        }
    }

    /// <summary>
    /// Reports each line of the reply head as curl's <c>-v</c> does, with
    /// <paramref name="refusal" />, when given, reported before the blank line that ends it, where
    /// curl 8.21.0 writes <c>Refused WebSocket upgrade</c> (BL-584).
    /// </summary>
    private static void ReportHead(ITransferEvents events, byte[] head, string? refusal)
    {
        int lineStart = 0;
        while (lineStart < head.Length)
        {
            int lineEnd = Array.IndexOf(head, (byte)'\n', lineStart) + 1;
            if (lineEnd == head.Length && refusal is not null)
            {
                events.ReportInfo(refusal);
            }

            events.ReportResponseHeader(head.AsSpan(lineStart, lineEnd - lineStart));
            lineStart = lineEnd;
        }
    }

    /// <summary>
    /// Sends the upgrade request and reads the reply head, reporting both for <c>-v</c> and
    /// <c>--trace</c> as curl 8.21.0 does (BL-584): the request as one header event, the head
    /// one line at a time, and on a refusal <c>closing connection #N</c>.
    /// </summary>
    private async Task<TransferResult> UpgradeAsync(IConnection connection, ITransferContext context, long connectionNumber)
    {
        HttpRequestOptions options = context.Http ?? new HttpRequestOptions();
        string method = options.CustomMethod ?? "GET";
        string? authorization = authenticator.CreateAuthorization(
            new HttpAuthRequest(
                method,
                context.Url,
                WsUpgradeRequestFormatter.RequestTarget(context.Url),
                context.Credentials,
                options.BearerToken,
                options.AuthSchemes,
                IsProxy: false),
            []);
        byte[] request = WsUpgradeRequestFormatter.Format(context.Url, options, method, NewKey(), authorization);
        context.Events.ReportRequestHeader(request);
        await SendAsync(connection, request, context.CancellationToken).ConfigureAwait(false);
        context.Events.ReportInfo(WsInfoLines.RequestSent);
        WsUpgradeResponse response = await WsUpgradeResponseReader.ReadAsync(connection, context.CancellationToken).ConfigureAwait(false);
        await WriteHeadAsync(context.HeaderOutput, response.Head, context.CancellationToken).ConfigureAwait(false);
        TransferReport report = new()
        {
            ResponseCode = response.StatusCode,
            HttpVersion = HttpVersion.Version11,
            Method = method,
            HeaderSize = response.Head.Length,
            RequestSize = request.Length,
        };
        if (response.StatusCode != SwitchingProtocols)
        {
            string message = string.Create(CultureInfo.InvariantCulture, $"Refused WebSocket upgrade: {response.StatusCode}");
            ReportHead(context.Events, response.Head, message);
            context.Events.ReportInfo(WsInfoLines.Closing(connectionNumber));
            return TransferResult.Failure(CurlExitCode.HttpReturnedError, message) with { Report = report };
        }

        ReportHead(context.Events, response.Head, refusal: null);
        context.Events.ReportInfo(WsInfoLines.SwitchingToWebSocket);
        context.Events.ReportInfo(WsInfoLines.SwitchedToWebSocket);
        TransferResult result = await ExchangeFramesAsync(connection, context, response.Remaining, report).ConfigureAwait(false);
        ReportTransferEnd(context.Events, result, connectionNumber);
        return result;
    }

    /// <summary>
    /// Writes the lines curl 8.21.0's <c>-v</c> ends an upgraded transfer with (BL-584):
    /// <c>shutting down connection #N</c> after the server closed the connection; a failure's
    /// message, then <c>shutting down connection #N</c> for <c>Empty reply from server</c> and
    /// <c>closing connection #N</c> for any other.
    /// </summary>
    private static void ReportTransferEnd(ITransferEvents events, TransferResult result, long connectionNumber)
    {
        if (result.ExitCode == CurlExitCode.Ok)
        {
            events.ReportInfo(WsInfoLines.ShuttingDown(connectionNumber));
            return;
        }

        events.ReportInfo(result.ErrorMessage!);
        events.ReportInfo(result.ExitCode == CurlExitCode.GotNothing
            ? WsInfoLines.ShuttingDown(connectionNumber)
            : WsInfoLines.Closing(connectionNumber));
    }

    /// <summary>
    /// Sends the <c>-T</c> upload as one binary frame, then writes the payload of every frame
    /// received to the output until the server closes the connection (ADR-0128, ADR-0131).
    /// </summary>
    /// <remarks>
    /// Measured against curl 8.21.0 (BL-582): the transfer ends with exit 0 when the server
    /// closes the connection after at least one frame byte, close frame or not, and with 52
    /// <c>Empty reply from server</c> when none arrived. <c>%{size_download}</c> counts frame
    /// bytes, heads included; <c>%{size_delivered}</c> the payload bytes written, close frame
    /// payloads included (BL-777); <c>%{size_upload}</c> the upload frame; <c>%{size_request}</c>
    /// the upgrade request, the upload frame and every pong. A failure keeps the report, so
    /// <c>%{http_code}</c> is still <c>101</c>. <c>-m</c> cancels the reads, and the
    /// cancellation escapes for the runner's exit 28 (ADR-0117).
    /// </remarks>
    private async Task<TransferResult> ExchangeFramesAsync(
        IConnection connection,
        ITransferContext context,
        byte[] alreadyReceived,
        TransferReport report)
    {
        var receiver = new WsFrameReceiver(connection, randomSource, context.Progress, context.Events);
        long uploaded = 0;
        TransferResult result;
        try
        {
            uploaded = await SendUploadAsync(connection, context).ConfigureAwait(false);
            await receiver.ReceiveAsync(
                alreadyReceived,
                (payload, token) => WriteAsync(context.Output, payload, token),
                context.CancellationToken).ConfigureAwait(false);
            result = receiver.BytesReceived == 0
                ? TransferResult.Failure(CurlExitCode.GotNothing, WsUpgradeResponseReader.EmptyReply)
                : TransferResult.Success(receiver.BytesReceived);
        }
        catch (WsTransferException failure)
        {
            result = TransferResult.Failure(failure.ExitCode, failure.Message, receiver.BytesReceived);
        }

        context.Progress.ReportTransferDone();
        return result with
        {
            Report = report with
            {
                RequestSize = report.RequestSize + uploaded + receiver.BytesSent,
                DownloadSize = receiver.BytesReceived,
                DeliveredSize = receiver.BytesDelivered,
                UploadSize = uploaded,
            },
        };
    }

    /// <summary>
    /// Sends the whole of <see cref="ITransferContext.Upload" /> as one masked binary frame, as
    /// curl 8.21.0 sends <c>-T</c> on a WebSocket; a failed read ends the upload, as curl takes
    /// a file read that fails.
    /// </summary>
    /// <returns>The frame's length, or 0 when there is no upload.</returns>
    private async ValueTask<long> SendUploadAsync(IConnection connection, ITransferContext context)
    {
        if (context.Upload is not { } upload)
        {
            return 0;
        }

        using var payload = new MemoryStream();
        try
        {
            await upload.CopyToAsync(payload, context.CancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The bytes read before the failure are the upload.
        }

        byte[] frame = WsFrameEncoder.Encode(WsOpcode.Binary, payload.GetBuffer().AsSpan(0, (int)payload.Length), randomSource);
        context.Events.ReportDataSent(frame);
        await SendAsync(connection, frame, context.CancellationToken).ConfigureAwait(false);
        context.Events.ReportInfo(WsInfoLines.UploadSent(frame.Length));
        context.Progress.ReportUploaded(frame.Length, frame.Length);
        return frame.Length;
    }

    /// <summary>Sends <paramref name="bytes" /> and flushes, turning a failure into curl's exit 55.</summary>
    /// <param name="connection">The connection to send on.</param>
    /// <param name="bytes">The request or frame to send.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes once the bytes are flushed.</returns>
    internal static async ValueTask SendAsync(IConnection connection, byte[] bytes, CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw WsIoFailures.SendFailed(exception);
        }
    }

    private static async ValueTask WriteHeadAsync(Stream? headerOutput, byte[] head, CancellationToken cancellationToken)
    {
        if (headerOutput is not null)
        {
            await WriteAsync(headerOutput, head, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Writes <paramref name="bytes" /> to <paramref name="destination" />, turning a failure into curl's exit 23.</summary>
    private static async ValueTask WriteAsync(Stream destination, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        try
        {
            await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw WsIoFailures.WriteFailed(bytes.Length, exception);
        }
    }

    private string NewKey()
    {
        Span<byte> key = stackalloc byte[KeyLength];
        randomSource.Fill(key);
        return Convert.ToBase64String(key);
    }
}
