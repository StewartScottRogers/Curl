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
internal sealed class SignalRHubSession(SignalRWebSocketSession session, byte[]? callRecord, Stream output, ITransferProgress progress)
{
    /// <summary>The error of the Completion curl sends for a hub call that expects a client result: the .NET client's text.</summary>
    internal const string NoClientResultError = "Client didn't provide a result.";

    private long bytesWritten;

    /// <summary>Runs the session and gives the number of bytes written to the output.</summary>
    /// <exception cref="SignalRHubException">The hub refused the handshake, failed the call or closed with an error (exit 22).</exception>
    /// <exception cref="SignalRTransportException">The WebSocket closed before the handshake response.</exception>
    /// <exception cref="SignalRWireFormatException">The hub sent a record that is not a hub protocol message.</exception>
    public async Task<long> RunAsync(CancellationToken cancellationToken)
    {
        await session.SendTextAsync(HubMessageWriter.WriteHandshakeRequest(), cancellationToken);
        byte[] handshake = await session.ReceiveRecordAsync(cancellationToken)
            ?? throw new SignalRTransportException("The hub closed the WebSocket before answering the handshake.");
        if (HubMessageReader.ReadHandshakeResponse(handshake).Error is { } refusal)
        {
            throw new SignalRHubException(CurlExitCode.HttpReturnedError, $"SignalR hub refused the handshake: {refusal}");
        }

        if (callRecord is not null)
        {
            await session.SendTextAsync(callRecord, cancellationToken);
        }

        while (await session.ReceiveRecordAsync(cancellationToken) is { } record
            && !await AcceptAsync(HubMessageReader.ReadMessage(record), cancellationToken))
        {
        }

        await session.CloseAsync(cancellationToken);
        return bytesWritten;
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
            await session.SendTextAsync(RefusalOf(invocationId), cancellationToken);
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
