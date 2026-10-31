using Curl.Extension.SignalR.Json;

namespace Curl.Extension.SignalR.HubProtocol;

/// <summary>Reads one JSON hub protocol record, without its 0x1E separator, into a message (HubProtocol.md).</summary>
internal static class HubMessageReader
{
    /// <summary>Reads the handshake response: an empty object, or one with an <c>error</c> (HubProtocol.md, "Overview").</summary>
    /// <exception cref="SignalRWireFormatException">The record is not a handshake response.</exception>
    public static HubHandshakeResponse ReadHandshakeResponse(ReadOnlySpan<byte> record) =>
        new(JsonFieldReading.ReadObject(record).OptionalString("error"));

    /// <summary>Reads a message of any type; a type HubProtocol.md does not define reads as <see cref="UnknownHubMessage"/>.</summary>
    /// <exception cref="SignalRWireFormatException">The record is not a message, or misses a member its type requires.</exception>
    public static HubMessage ReadMessage(ReadOnlySpan<byte> record)
    {
        JsonObjectFields fields = JsonFieldReading.ReadObject(record);
        return fields.RequiredInt32("type") switch
        {
            1 => new InvocationMessage(fields.OptionalString("invocationId"), fields.RequiredString("target"), Arguments(fields), fields.OptionalStringArray("streamIds")),
            2 => new StreamItemMessage(fields.RequiredString("invocationId"), fields.RequiredRawJson("item")),
            3 => ReadCompletion(fields),
            4 => new StreamInvocationMessage(fields.RequiredString("invocationId"), fields.RequiredString("target"), Arguments(fields), fields.OptionalStringArray("streamIds")),
            5 => new CancelInvocationMessage(fields.RequiredString("invocationId")),
            6 => new PingMessage(),
            7 => new CloseMessage(fields.OptionalString("error"), fields.OptionalBoolean("allowReconnect")),
            int type => new UnknownHubMessage(type),
        };
    }

    private static IReadOnlyList<string> Arguments(JsonObjectFields fields)
    {
        _ = fields.RequiredRawJson("arguments");
        return fields.OptionalRawJsonArray("arguments");
    }

    private static CompletionMessage ReadCompletion(JsonObjectFields fields)
    {
        string? resultJson = fields.OptionalRawJson("result");
        string? error = fields.OptionalString("error");
        if (resultJson is not null && error is not null)
        {
            throw new SignalRWireFormatException("A Completion message carries both 'result' and 'error'.");
        }

        return new CompletionMessage(fields.RequiredString("invocationId"), resultJson, error);
    }
}
