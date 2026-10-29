using System.Text;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketRepl.Session;

/// <summary>
/// Shows received messages: a text message's bytes as they are, a binary message's as lowercase hexadecimal,
/// each message ended by a line feed. Control frames are not shown.
/// </summary>
/// <param name="output">Where the messages are written.</param>
internal sealed class WebSocketReplOutput(Stream output)
{
    private static readonly byte[] LineFeed = [(byte)'\n'];

    private WsOpcode messageOpcode = WsOpcode.Text;

    /// <summary>Shows one run of a received frame's payload.</summary>
    /// <param name="run">The run, as the frame decoder gave it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the run is written and flushed.</returns>
    public async ValueTask ShowAsync(WsFrameRun run, CancellationToken cancellationToken)
    {
        if (run.Opcode is WsOpcode.Ping or WsOpcode.Pong)
        {
            return;
        }

        await output.WriteAsync(Shown(run), cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    // The run's bytes as shown, with the line feed that ends its message when the run ends one.
    private byte[] Shown(WsFrameRun run)
    {
        if (run.Opcode != WsOpcode.Continuation)
        {
            messageOpcode = run.Opcode;
        }

        byte[] shown = messageOpcode == WsOpcode.Binary
            ? Encoding.ASCII.GetBytes(Convert.ToHexStringLower(run.Payload.Span))
            : run.Payload.ToArray();
        return run.IsFinal && run.BytesLeft == 0 ? [.. shown, .. LineFeed] : shown;
    }
}
