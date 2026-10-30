using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketSubprotocols;

/// <summary>
/// What <c>--ws-subprotocol</c> does to a ws or wss transfer (ADR-1085): asks for the subprotocol with
/// <c>Sec-WebSocket-Protocol</c> (RFC 6455 §4.1) and uploads its opening messages in place of any <c>-d</c> or
/// <c>-T</c> upload, one text frame each (<see cref="WsMessageUploadStream" />). The messages go out once the
/// upgrade is accepted, without waiting for the server's answer to the first. Any other transfer runs unchanged;
/// a value that names no subprotocol this extension speaks fails the transfer with exit 2 before anything is sent.
/// </summary>
/// <param name="value">The value given to <c>--ws-subprotocol</c>.</param>
internal sealed class WebSocketSubprotocolTransferHook(string value) : ITransferHook
{
    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is not { } http || !IsWebSocket(context.Url))
        {
            return await next(context).ConfigureAwait(false);
        }

        WebSocketSubprotocolRequest request;
        try
        {
            request = WebSocketSubprotocolRequest.Parse(value, context.Url.Host);
        }
        catch (FormatException invalid)
        {
            return TransferResult.Failure(CurlExitCode.FailedInit, invalid.Message);
        }

        using WsMessageUploadStream upload = new([.. request.Messages.Select(message => new WsUploadMessage(WsOpcode.Text, Encoding.UTF8.GetBytes(message)))]);
        HttpRequestOptions asking = http with { Headers = [.. http.Headers, $"Sec-WebSocket-Protocol: {request.Name}"] };
        return await next(new WebSocketSubprotocolContext(context, upload, asking)).ConfigureAwait(false);
    }

    private static bool IsWebSocket(CurlUrl url) =>
        url.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase) || url.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase);
}
