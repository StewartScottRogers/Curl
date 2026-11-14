using System.Text;
using System.Text.Json;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SocketIo;

/// <summary>
/// One Socket.IO v5 client session over Engine.IO v4's WebSocket transport: on the Engine.IO
/// <c>open</c> packet it connects to its namespace (Socket.IO "Connection to a namespace"), emits its
/// event once the server confirms, answers each <c>ping</c> with a <c>pong</c> (Engine.IO
/// "Heartbeat") and ends with exit 56 when no ping arrives within the open packet's
/// <c>pingInterval</c> + <c>pingTimeout</c> on the transfer's clock (ADR-1210), sends no packet over the open packet's <c>maxPayload</c> and ends with exit 55 instead (ADR-1212), writes each event received as its JSON array and a newline, acknowledges each event
/// that asks for it with an empty array, and ends when the server disconnects or closes.
/// </summary>
/// <param name="channel">The WebSocket messages.</param>
/// <param name="namespaceName">The namespace to connect to, <c>/</c> for the main one.</param>
/// <param name="emit">The event's JSON array to emit, or <see langword="null" /> to only receive.</param>
/// <param name="output">Where received events are written.</param>
/// <param name="timeProvider">The transfer's clock, which times the heartbeat.</param>
/// <param name="progress">Told the bytes written so far after each event, so a <c>-m</c> timeout and <c>%{size_download}</c> count them.</param>
internal sealed class SocketIoSession(WebSocketMessageChannel channel, string namespaceName, string? emit, Stream output, TimeProvider timeProvider, ITransferProgress progress)
{
    private const char EngineIoOpen = '0';

    private const char EngineIoClose = '1';

    private const char EngineIoPing = '2';

    private const char EngineIoPong = '3';

    private const char EngineIoMessage = '4';

    private long written;

    private bool isConnected;

    private bool isEnded;

    private readonly CancellationTokenSource heartbeat = new(Timeout.InfiniteTimeSpan, timeProvider);

    private TimeSpan heartbeatTimeout = Timeout.InfiniteTimeSpan;

    private long maxPayload = long.MaxValue;

    /// <summary>
    /// Gives how long the client waits for the next ping, the open packet's <c>pingInterval</c> plus
    /// <c>pingTimeout</c> in milliseconds (capped at the longest a timer can wait), or
    /// <see cref="Timeout.InfiniteTimeSpan" /> when the handshake does not give both as whole non-negative numbers.
    /// </summary>
    /// <param name="handshake">The open packet's JSON handshake, the packet type left out.</param>
    /// <returns>The heartbeat timeout.</returns>
    internal static TimeSpan HeartbeatTimeoutOf(string handshake) =>
        ReadHandshake(handshake, Timeout.InfiniteTimeSpan, root =>
            root.TryGetProperty("pingInterval", out JsonElement interval) && root.TryGetProperty("pingTimeout", out JsonElement timeout)
            && interval.TryGetUInt32(out uint intervalMilliseconds) && timeout.TryGetUInt32(out uint timeoutMilliseconds)
                ? TimeSpan.FromMilliseconds(Math.Min((long)intervalMilliseconds + timeoutMilliseconds, uint.MaxValue - 1L))
                : Timeout.InfiniteTimeSpan);

    /// <summary>
    /// Gives the longest packet the server accepts, the open packet's <c>maxPayload</c> in bytes, or
    /// <see cref="long.MaxValue" /> when the handshake does not give it as a whole non-negative number (ADR-1212).
    /// </summary>
    /// <param name="handshake">The open packet's JSON handshake, the packet type left out.</param>
    /// <returns>The most UTF-8 bytes one Engine.IO packet the client sends may have, its type included.</returns>
    internal static long MaxPayloadOf(string handshake) =>
        ReadHandshake(handshake, long.MaxValue, root =>
            root.TryGetProperty("maxPayload", out JsonElement maxPayload) && maxPayload.TryGetInt64(out long bytes) && bytes >= 0 ? bytes : long.MaxValue);

    private static T ReadHandshake<T>(string handshake, T fallback, Func<JsonElement, T> read)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(handshake);
            return read(document.RootElement);
        }
        catch (JsonException)
        {
            return fallback;
        }
        catch (InvalidOperationException)
        {
            return fallback;
        }
    }

    /// <summary>Runs the session until the server disconnects or closes.</summary>
    /// <param name="cancellationToken">Cancels the session.</param>
    /// <returns>How many bytes were written to the output.</returns>
    /// <exception cref="SocketIoException">
    /// The server refused the namespace, closed before connecting to it, sent no ping within the heartbeat timeout,
    /// or a packet to send is over the open packet's <c>maxPayload</c>.
    /// </exception>
    public async ValueTask<long> RunAsync(CancellationToken cancellationToken)
    {
        using (heartbeat)
        using (CancellationTokenSource receiving = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, heartbeat.Token))
        {
            try
            {
                while (!isEnded && await channel.ReceiveTextAsync(receiving.Token).ConfigureAwait(false) is { } text)
                {
                    await AcceptAsync(text, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SocketIoException(CurlExitCode.RecvError, $"Socket.IO server sent no ping within {heartbeatTimeout.TotalMilliseconds} ms");
            }
        }

        return isConnected
            ? written
            : throw new SocketIoException(CurlExitCode.GotNothing, $"Socket.IO server closed before connecting to namespace {namespaceName}");
    }

    private ValueTask AcceptAsync(string text, CancellationToken cancellationToken) =>
        text.Length == 0 ? ValueTask.CompletedTask : text[0] switch
        {
            EngineIoOpen => OpenedAsync(text[1..], cancellationToken),
            EngineIoClose => End(),
            EngineIoPing => PingedAsync(text[1..], cancellationToken),
            EngineIoMessage => AcceptPacketAsync(SocketIoPacket.Decode(text[1..]), cancellationToken),
            _ => ValueTask.CompletedTask,
        };

    private ValueTask OpenedAsync(string handshake, CancellationToken cancellationToken)
    {
        heartbeatTimeout = HeartbeatTimeoutOf(handshake);
        maxPayload = MaxPayloadOf(handshake);
        heartbeat.CancelAfter(heartbeatTimeout);
        return SendPacketAsync(new SocketIoPacket(SocketIoPacketType.Connect, namespaceName, null, string.Empty), cancellationToken);
    }

    private ValueTask PingedAsync(string payload, CancellationToken cancellationToken)
    {
        heartbeat.CancelAfter(heartbeatTimeout);
        return SendTextAsync(EngineIoPong + payload, cancellationToken);
    }

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
        progress.ReportDownloaded(written, null);
        if (packet.AckId is not null)
        {
            await SendPacketAsync(new SocketIoPacket(SocketIoPacketType.Ack, namespaceName, packet.AckId, "[]"), cancellationToken).ConfigureAwait(false);
        }
    }

    private ValueTask SendPacketAsync(SocketIoPacket packet, CancellationToken cancellationToken) =>
        SendTextAsync(EngineIoMessage + packet.Encode(), cancellationToken);

    private ValueTask SendTextAsync(string text, CancellationToken cancellationToken)
    {
        int length = Encoding.UTF8.GetByteCount(text);
        return length <= maxPayload
            ? channel.SendTextAsync(text, cancellationToken)
            : throw new SocketIoException(CurlExitCode.SendError, $"Socket.IO packet of {length} bytes is over the server's maxPayload of {maxPayload} bytes");
    }
}
