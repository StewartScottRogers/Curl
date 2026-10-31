using System.Buffers;
using System.Net;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extended.Protocol.Mqtt5.WebSocket;

/// <summary>
/// An <see cref="IConnection"/> that carries MQTT 5 bytes in WebSocket binary frames over an upgraded connection
/// (OASIS MQTT Version 5.0 section 6, RFC 6455 section 5). Each write goes out as one masked binary frame; reads give
/// the binary payload as one byte stream, so a packet may span frames and a frame may hold several packets. A ping is
/// answered with a pong, the server's close frame reads as the end of the stream, and a text frame or a frame that
/// breaks RFC 6455 fails the read, so the transfer ends and the connection is closed.
/// </summary>
/// <param name="upgraded">The connection the opening handshake succeeded on.</param>
/// <param name="randomSource">The source of each frame's mask.</param>
/// <param name="received">The bytes that came after the upgrade reply head.</param>
internal sealed class Mqtt5WebSocketConnection(IConnection upgraded, IWebSocketRandomSource randomSource, ReadOnlyMemory<byte> received) : IConnection
{
    private const int ReadSize = 16384;

    private readonly WsFrameDecoder decoder = new(recordsRuns: true);

    private readonly ArrayBufferWriter<byte> payload = new();

    private readonly byte[] readBuffer = new byte[ReadSize];

    private ReadOnlyMemory<byte> pending = received;

    private int delivered;

    private bool isClosed;

    private bool isTextMessage;

    /// <inheritdoc/>
    public bool IsSecure => upgraded.IsSecure;

    /// <inheritdoc/>
    public EndPoint? RemoteEndPoint => upgraded.RemoteEndPoint;

    /// <summary>Gives the next binary payload bytes, or 0 once the server sent a close frame or closed the connection.</summary>
    /// <exception cref="IOException">The server sent a text frame or a frame that breaks RFC 6455.</exception>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await AcceptAsync(pending, cancellationToken).ConfigureAwait(false);
        pending = ReadOnlyMemory<byte>.Empty;
        while (payload.WrittenCount == delivered && !isClosed)
        {
            int read = await upgraded.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return 0;
            }

            await AcceptAsync(readBuffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return Deliver(buffer.Span);
    }

    /// <summary>Sends <paramref name="buffer"/> as one binary frame.</summary>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        upgraded.WriteAsync(WsFrameEncoder.Encode(WsOpcode.Binary, buffer.Span, randomSource), cancellationToken);

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken) => upgraded.FlushAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => upgraded.DisposeAsync();

    private int Deliver(Span<byte> destination)
    {
        ReadOnlySpan<byte> undelivered = payload.WrittenSpan[delivered..];
        int count = Math.Min(undelivered.Length, destination.Length);
        undelivered[..count].CopyTo(destination);
        delivered += count;
        if (delivered == payload.WrittenCount)
        {
            payload.ResetWrittenCount();
            delivered = 0;
        }

        return count;
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
            throw new IOException(violation);
        }

        if (decoded.LastPing is { } ping)
        {
            await upgraded.WriteAsync(WsFrameEncoder.Encode(WsOpcode.Pong, ping, randomSource), cancellationToken).ConfigureAwait(false);
            await upgraded.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void Collect(WsFrameRun run)
    {
        switch (run.Opcode)
        {
            case WsOpcode.Close:
                isClosed = true;
                break;
            case WsOpcode.Ping or WsOpcode.Pong:
                break;
            default:
                CollectData(run);
                break;
        }
    }

    private void CollectData(WsFrameRun run)
    {
        isTextMessage = run.Opcode == WsOpcode.Continuation ? isTextMessage : run.Opcode == WsOpcode.Text;
        if (isTextMessage)
        {
            throw new IOException(Mqtt5WebSocketMessages.TextFrame);
        }

        payload.Write(run.Payload.Span);
    }
}
