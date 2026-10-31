using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.SocketIo;

/// <summary>
/// One Socket.IO v5 client session over Engine.IO v4's WebSocket transport: on the Engine.IO
/// <c>open</c> packet it connects to its namespace (Socket.IO "Connection to a namespace"), emits its
/// event once the server confirms, answers each <c>ping</c> with a <c>pong</c> (Engine.IO
/// "Heartbeat"), writes each event received as its JSON array and a newline, acknowledges each event
/// that asks for it with an empty array, and ends when the server disconnects or closes.
/// </summary>
/// <param name="channel">The WebSocket messages.</param>
/// <param name="namespaceName">The namespace to connect to, <c>/</c> for the main one.</param>
/// <param name="emit">The event's JSON array to emit, or <see langword="null" /> to only receive.</param>
/// <param name="output">Where received events are written.</param>
internal sealed class SocketIoSession(WebSocketMessageChannel channel, string namespaceName, string? emit, Stream output)
{
    private const char EngineIoOpen = '0';

    private const char EngineIoClose = '1';

    private const char EngineIoPing = '2';

    private const char EngineIoPong = '3';

    private const char EngineIoMessage = '4';

    private long written;

    private bool isConnected;

    private bool isEnded;

    /// <summary>Runs the session until the server disconnects or closes.</summary>
    /// <param name="cancellationToken">Cancels the session.</param>
    /// <returns>How many bytes were written to the output.</returns>
    /// <exception cref="SocketIoException">The server refused the namespace or closed before connecting to it.</exception>
    public async ValueTask<long> RunAsync(CancellationToken cancellationToken)
    {
        while (!isEnded && await channel.ReceiveTextAsync(cancellationToken).ConfigureAwait(false) is { } text)
        {
            await AcceptAsync(text, cancellationToken).ConfigureAwait(false);
        }

        return isConnected
            ? written
            : throw new SocketIoException(CurlExitCode.GotNothing, $"Socket.IO server closed before connecting to namespace {namespaceName}");
    }

    private ValueTask AcceptAsync(string text, CancellationToken cancellationToken) =>
        text.Length == 0 ? ValueTask.CompletedTask : text[0] switch
        {
            EngineIoOpen => SendPacketAsync(new SocketIoPacket(SocketIoPacketType.Connect, namespaceName, null, string.Empty), cancellationToken),
            EngineIoClose => End(),
            EngineIoPing => channel.SendTextAsync(EngineIoPong + text[1..], cancellationToken),
            EngineIoMessage => AcceptPacketAsync(SocketIoPacket.Decode(text[1..]), cancellationToken),
            _ => ValueTask.CompletedTask,
        };

    private ValueTask AcceptPacketAsync(SocketIoPacket? packet, CancellationToken cancellationToken) =>
        packet is not null && packet.Namespace == namespaceName ? AcceptOwnPacketAsync(packet, cancellationToken) : ValueTask.CompletedTask;

    // Acknowledgements and binary packets are ignored (ADR-1035).
    private ValueTask AcceptOwnPacketAsync(SocketIoPacket packet, CancellationToken cancellationToken)
    {
        if (packet.Type == SocketIoPacketType.Connect)
        {
            return ConnectedAsync(cancellationToken);
        }

        if (packet.Type == SocketIoPacketType.ConnectError)
        {
            return Refused(packet);
        }

        if (packet.Type == SocketIoPacketType.Disconnect)
        {
            return End();
        }

        return packet.Type == SocketIoPacketType.Event ? WriteEventAsync(packet, cancellationToken) : ValueTask.CompletedTask;
    }

    private ValueTask Refused(SocketIoPacket packet) =>
        throw new SocketIoException(CurlExitCode.HttpReturnedError, $"Socket.IO server refused namespace {namespaceName}: {packet.Data}");

    private ValueTask End()
    {
        isEnded = true;
        return ValueTask.CompletedTask;
    }

    private ValueTask ConnectedAsync(CancellationToken cancellationToken)
    {
        isConnected = true;
        return emit is null
            ? ValueTask.CompletedTask
            : SendPacketAsync(new SocketIoPacket(SocketIoPacketType.Event, namespaceName, null, emit), cancellationToken);
    }

    private async ValueTask WriteEventAsync(SocketIoPacket packet, CancellationToken cancellationToken)
    {
        byte[] line = Encoding.UTF8.GetBytes(packet.Data + "\n");
        await output.WriteAsync(line, cancellationToken).ConfigureAwait(false);
        written += line.Length;
        if (packet.AckId is not null)
        {
            await SendPacketAsync(new SocketIoPacket(SocketIoPacketType.Ack, namespaceName, packet.AckId, "[]"), cancellationToken).ConfigureAwait(false);
        }
    }

    private ValueTask SendPacketAsync(SocketIoPacket packet, CancellationToken cancellationToken) =>
        channel.SendTextAsync(EngineIoMessage + packet.Encode(), cancellationToken);
}
