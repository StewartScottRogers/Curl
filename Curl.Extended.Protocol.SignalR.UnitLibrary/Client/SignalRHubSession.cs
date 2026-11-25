using System.Buffers;
using System.Text;
using System.Text.Json;
using Curl.Extended.Protocol.SignalR.HubProtocol;
using Curl.Extended.Protocol.SignalR.Transport;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Client;

/// <summary>
/// Speaks the JSON hub protocol over an upgraded WebSocket (HubProtocol.md): sends the handshake request and checks
/// its response, sends the call when there is one, and writes one line to <paramref name="output"/> per message the
/// hub sends that curl shows: the call's Completion result, each of its StreamItems, and each Invocation the hub makes
/// as the JSON array <c>["target", args...]</c>. Curl implements no client methods, so it answers an Invocation that
/// carries an <c>invocationId</c> (a client result) with a Completion whose error is <see cref="NoClientResultError"/>
/// (ADR-1204). It ends at the call's Completion, a Close message or the WebSocket's close, and at the first two
/// starts the WebSocket closing handshake with a Close frame (RFC 6455 section 1.4).
/// </summary>
/// <param name="session">The upgraded WebSocket.</param>
/// <param name="callRecord">The Invocation or StreamInvocation record <see cref="SignalRCall"/> gave, or <see langword="null"/>.</param>
/// <param name="output">Where the lines go.</param>
/// <param name="progress">Told the running byte total after each line, so a timeout message can count it.</param>
/// <param name="timeProvider">The clock of the keep-alive Ping, sent whenever nothing else was sent for <see cref="KeepAliveInterval"/>.</param>
internal sealed class SignalRHubSession(SignalRWebSocketSession session, byte[]? callRecord, Stream output, ITransferProgress progress, TimeProvider timeProvider)
{
    /// <summary>How long the client may send nothing before it sends a Ping: the .NET client's default KeepAliveInterval, half the server's 30 s client timeout (HubProtocol.md, "Ping").</summary>
    internal static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    /// <summary>The error of the Completion curl sends for a hub call that expects a client result: the .NET client's text.</summary>
    internal const string NoClientResultError = "Client didn't provide a result.";

    private long bytesWritten;

    private long lastSentAt;

    /// <summary>Runs the session and gives the number of bytes written to the output.</summary>
    /// <exception cref="SignalRHubException">The hub refused the handshake, failed the call or closed with an error (exit 22).</exception>
    /// <exception cref="SignalRTransportException">The WebSocket closed before the handshake response.</exception>
    /// <exception cref="SignalRWireFormatException">The hub sent a record that is not a hub protocol message.</exception>
    public async Task<long> RunAsync(CancellationToken cancellationToken)
    {
        await SendAsync(HubMessageWriter.WriteHandshakeRequest(), cancellationToken);
        byte[] handshake = await session.ReceiveRecordAsync(cancellationToken)
            ?? throw new SignalRTransportException("The hub closed the WebSocket before answering the handshake.");
        if (HubMessageReader.ReadHandshakeResponse(handshake).Error is { } refusal)
        {
            throw new SignalRHubException(CurlExitCode.HttpReturnedError, $"SignalR hub refused the handshake: {refusal}");
        }

        if (callRecord is not null)
        {
            await SendAsync(callRecord, cancellationToken);
        }

        using var stopPinging = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task pinging = PingWhenIdleAsync(stopPinging.Token, cancellationToken);
        Task reading = ReadUntilEndAsync(cancellationToken);
        await Task.WhenAny(reading);
        await StopPingingAsync(stopPinging, pinging);

        // Rethrows the read loop's failure, which is then the transfer's; otherwise a Ping that failed to write while
        // the hub was open fails the transfer here.
        await reading;
        await AwaitPingingAsync(pinging);
        await session.CloseAsync(cancellationToken);
        return bytesWritten;
    }

    // A Ping write that failed is ignored only when the read loop ended through the server's Close: the hub finished the
    // transfer and went away, possibly while its Close was still unread when the write failed. Otherwise it is an error.
    private async Task AwaitPingingAsync(Task pinging)
    {
        try
        {
            await pinging;
        }
        catch (IOException) when (session.CloseReceived)
        {
        }
    }

    // Sends a record and notes the time, so the keep-alive Ping waits a full interval after any send.
    private async Task SendAsync(byte[] record, CancellationToken cancellationToken)
    {
        await session.SendTextAsync(record, cancellationToken);
        Volatile.Write(ref lastSentAt, timeProvider.GetTimestamp());
    }

    // Sends a Ping each time KeepAliveInterval passes with nothing sent (HubProtocol.md, "Ping"); ends when stopped.
    // The stop token is checked only before a Ping starts and never reaches the frame write, so a frame is written whole;
    // abort is the transfer's own cancellation, which fails the transfer anyway.
    private async Task PingWhenIdleAsync(CancellationToken stop, CancellationToken abort)
    {
        while (!stop.IsCancellationRequested)
        {
            TimeSpan idle = timeProvider.GetElapsedTime(Volatile.Read(ref lastSentAt));
            if (idle >= KeepAliveInterval)
            {
                if (!await TrySendPingAsync(abort))
                {
                    return;
                }
            }
            else
            {
                // WhenAny does not throw when the stop cancels the delay, so stopping is a normal end of this task.
                await Task.WhenAny(Task.Delay(KeepAliveInterval - idle, timeProvider, stop));
            }
        }
    }

    // Reads and accepts records until the session ends.
    private async Task ReadUntilEndAsync(CancellationToken cancellationToken)
    {
        while (await session.ReceiveRecordAsync(cancellationToken) is { } record
            && !await AcceptAsync(HubMessageReader.ReadMessage(record), cancellationToken))
        {
        }
    }

    // Waited for on every path, so the connection is never disposed under a Ping write and a cancel never cuts a Ping
    // frame short; WhenAny does not rethrow, so when the read loop failed its exception is the one reported and the
    // Ping task's own failure is dropped.
    private static async Task StopPingingAsync(CancellationTokenSource stopPinging, Task pinging)
    {
        await stopPinging.CancelAsync();
        await Task.WhenAny(pinging);
    }

    // Writes a Ping unless the Close handshake has begun; gives false when the keep-alive loop should end. A failed write
    // faults the Ping task; RunAsync decides after the read loop ends whether that is an error (AwaitPingingAsync).
    private async Task<bool> TrySendPingAsync(CancellationToken abort)
    {
        bool written = await session.SendTextUnlessClosingAsync(HubMessageWriter.WritePing(), abort);
        Volatile.Write(ref lastSentAt, timeProvider.GetTimestamp());
        return written;
    }

    private static string CallArray(InvocationMessage invocation)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            writer.WriteStringValue(invocation.Target);
            foreach (string argumentJson in invocation.ArgumentsJson)
            {
                writer.WriteRawValue(argumentJson);
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    // The type 3 Completion record that refuses a hub call expecting a client result (HubProtocol.md, "Completion").
    private static byte[] RefusalOf(string invocationId)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("type", 3);
            writer.WriteString("invocationId", invocationId);
            writer.WriteString("error", NoClientResultError);
            writer.WriteEndObject();
        }

        return [.. buffer.WrittenSpan, 0x1E];
    }

    // The line curl writes for a message, or null for none; a failed Completion of the call throws.
    private static string? LineFor(HubMessage message) => message switch
    {
        StreamItemMessage item when item.InvocationId == SignalRCall.InvocationId => item.ItemJson,
        CompletionMessage completion when completion.InvocationId == SignalRCall.InvocationId => ResultOf(completion),
        InvocationMessage invocation => CallArray(invocation),
        _ => null,
    };

    // Whether a message ends the session; a Close with an error throws.
    private static bool Ends(HubMessage message) => message switch
    {
        CompletionMessage completion => completion.InvocationId == SignalRCall.InvocationId,
        CloseMessage { Error: { } error } => throw new SignalRHubException(CurlExitCode.HttpReturnedError, $"SignalR hub closed the connection: {error}"),
        CloseMessage => true,
        _ => false,
    };

    private static string? ResultOf(CompletionMessage completion) =>
        completion.Error is null
            ? completion.ResultJson
            : throw new SignalRHubException(CurlExitCode.HttpReturnedError, $"SignalR hub method failed: {completion.Error}");

    // Gives whether the session is over.
    private async Task<bool> AcceptAsync(HubMessage message, CancellationToken cancellationToken)
    {
        if (LineFor(message) is { } line)
        {
            await WriteLineAsync(line, cancellationToken);
        }

        if (message is InvocationMessage { InvocationId: { } invocationId })
        {
            await SendAsync(RefusalOf(invocationId), cancellationToken);
        }

        return Ends(message);
    }

    private async Task WriteLineAsync(string json, CancellationToken cancellationToken)
    {
        byte[] line = Encoding.UTF8.GetBytes(json + "\n");
        await output.WriteAsync(line, cancellationToken);
        await output.FlushAsync(cancellationToken);
        bytesWritten += line.Length;
        progress.ReportDownloaded(bytesWritten, null);
    }
}
