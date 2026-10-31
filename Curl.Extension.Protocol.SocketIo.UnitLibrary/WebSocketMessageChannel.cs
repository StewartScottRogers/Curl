using System.Buffers;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.Protocol.SocketIo;

/// <summary>
/// Whole WebSocket text messages over an upgraded connection (RFC 6455 sections 5 and 6), decoded with
/// the shared <see cref="WsFrameDecoder" /> and sent with <see cref="WsFrameEncoder" />. Each ping is
/// answered with a pong (section 5.5.2); binary messages are dropped, as Engine.IO v4 carries only
/// Socket.IO's binary attachments in them.
/// </summary>
/// <param name="connection">The upgraded connection.</param>
/// <param name="randomSource">The source of each client frame's mask.</param>
/// <param name="received">The bytes that came after the upgrade reply head.</param>
internal sealed class WebSocketMessageChannel(IConnection connection, IWebSocketRandomSource randomSource, ReadOnlyMemory<byte> received)
{
    private const int ReadSize = 16384;

    private readonly WsFrameDecoder decoder = new(recordsRuns: true);

    // A null entry is the server's close frame.
    private readonly Queue<string?> messages = new();

    private readonly ArrayBufferWriter<byte> message = new();

    private readonly byte[] buffer = new byte[ReadSize];

    private ReadOnlyMemory<byte> pending = received;

    private bool isBinaryMessage;

    /// <summary>Waits for the next whole text message.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The message, or <see langword="null" /> once the server sent a close frame or closed the connection.</returns>
    /// <exception cref="SocketIoException">A frame broke RFC 6455 (exit 56).</exception>
    public async ValueTask<string?> ReceiveTextAsync(CancellationToken cancellationToken)
    {
        await AcceptAsync(pending, cancellationToken).ConfigureAwait(false);
        pending = ReadOnlyMemory<byte>.Empty;
        while (messages.Count == 0)
        {
            int read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            await AcceptAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return messages.Dequeue();
    }

    /// <summary>Sends <paramref name="text" /> as one text frame.</summary>
    /// <param name="text">The message.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public ValueTask SendTextAsync(string text, CancellationToken cancellationToken) =>
        SendAsync(WsOpcode.Text, Encoding.UTF8.GetBytes(text), cancellationToken);

    private async ValueTask SendAsync(WsOpcode opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(WsFrameEncoder.Encode(opcode, payload.Span, randomSource), cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask AcceptAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        WsDecodedBytes decoded = decoder.Decode(bytes.Span);
        foreach (WsFrameRun run in decoder.TakeRuns())
        {
            Collect(run);
        }

        if (decoded.ViolationMessage is { } violation)
        {
            throw new SocketIoException(CurlExitCode.RecvError, violation);
        }

        if (decoded.LastPing is { } ping)
        {
            await SendAsync(WsOpcode.Pong, ping, cancellationToken).ConfigureAwait(false);
        }
    }

    private void Collect(WsFrameRun run)
    {
        if (run.Opcode is WsOpcode.Close)
        {
            messages.Enqueue(null);
            return;
        }

        if (run.Opcode is not (WsOpcode.Ping or WsOpcode.Pong))
        {
            CollectData(run);
        }
    }

    private void CollectData(WsFrameRun run)
    {
        isBinaryMessage = run.Opcode == WsOpcode.Continuation ? isBinaryMessage : run.Opcode == WsOpcode.Binary;
        message.Write(run.Payload.Span);
        if (run.IsFinal && run.BytesLeft == 0)
        {
            EndMessage();
        }
    }

    private void EndMessage()
    {
        if (!isBinaryMessage)
        {
            messages.Enqueue(Encoding.UTF8.GetString(message.WrittenSpan));
        }

        message.ResetWrittenCount();
    }
}
