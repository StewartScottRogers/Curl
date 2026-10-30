using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketExtendedConnect.Option;

/// <summary>
/// Runs an open extended CONNECT WebSocket for one transfer: sends the <c>-d</c> data as one text message, writes
/// every data frame's payload to the output, answers each ping with a pong and the server's close with a close,
/// and ends when the server closes or ends its stream.
/// </summary>
internal static class ExtendedConnectSession
{
    /// <summary>Runs <paramref name="channel" /> for <paramref name="context" />.</summary>
    /// <param name="channel">The open WebSocket.</param>
    /// <param name="context">The transfer, for its <c>-d</c> data, output and cancellation.</param>
    /// <returns>Success with the payload bytes written.</returns>
    internal static async Task<TransferResult> RunAsync(IExtendedConnectChannel channel, ITransferContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        if (context.PostData is { } data)
        {
            await channel.SendAsync(WsOpcode.Text, data, cancellationToken).ConfigureAwait(false);
        }

        long written = 0;
        while (await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false) is { } runs)
        {
            foreach (WsFrameRun run in runs)
            {
                switch (run.Opcode)
                {
                    case WsOpcode.Close:
                        await channel.SendAsync(WsOpcode.Close, run.Payload, cancellationToken).ConfigureAwait(false);
                        await channel.CloseAsync(cancellationToken).ConfigureAwait(false);
                        return TransferResult.Success(written);
                    case WsOpcode.Ping:
                        await channel.SendAsync(WsOpcode.Pong, run.Payload, cancellationToken).ConfigureAwait(false);
                        break;
                    case WsOpcode.Pong:
                        break;
                    default:
                        await context.Output.WriteAsync(run.Payload, cancellationToken).ConfigureAwait(false);
                        written += run.Payload.Length;
                        break;
                }
            }
        }

        return TransferResult.Success(written);
    }
}
