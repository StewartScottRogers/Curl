using System.Buffers;
using System.Text.Json;

namespace Curl.Extended.Protocol.SignalR.HubProtocol;

/// <summary>
/// Writes the records a SignalR client sends in the JSON hub protocol, each one JSON object ended by the record
/// separator 0x1E (HubProtocol.md, "JSON Encoding").
/// </summary>
internal static class HubMessageWriter
{
    /// <summary>Writes the handshake request <c>{"protocol":"json","version":1}</c> (HubProtocol.md, "Overview").</summary>
    public static byte[] WriteHandshakeRequest() => WriteRecord(writer =>
    {
        writer.WriteString("protocol", "json");
        writer.WriteNumber("version", 1);
    });

    /// <summary>Writes a type 1 Invocation, without <c>invocationId</c> when it has none (HubProtocol.md, "Invocation").</summary>
    public static byte[] WriteInvocation(InvocationMessage message) => WriteRecord(writer =>
    {
        writer.WriteNumber("type", 1);
        if (message.InvocationId is not null)
        {
            writer.WriteString("invocationId", message.InvocationId);
        }

        WriteCall(writer, message.Target, message.ArgumentsJson, message.StreamIds);
    });

    /// <summary>Writes a type 4 StreamInvocation (HubProtocol.md, "StreamInvocation").</summary>
    public static byte[] WriteStreamInvocation(StreamInvocationMessage message) => WriteRecord(writer =>
    {
        writer.WriteNumber("type", 4);
        writer.WriteString("invocationId", message.InvocationId);
        WriteCall(writer, message.Target, message.ArgumentsJson, message.StreamIds);
    });

    /// <summary>Writes a type 5 CancelInvocation (HubProtocol.md, "CancelInvocation").</summary>
    public static byte[] WriteCancelInvocation(CancelInvocationMessage message) => WriteRecord(writer =>
    {
        writer.WriteNumber("type", 5);
        writer.WriteString("invocationId", message.InvocationId);
    });

    /// <summary>Writes a type 6 Ping (HubProtocol.md, "Ping").</summary>
    public static byte[] WritePing() => WriteRecord(writer => writer.WriteNumber("type", 6));

    private static void WriteCall(Utf8JsonWriter writer, string target, IReadOnlyList<string> argumentsJson, IReadOnlyList<string> streamIds)
    {
        writer.WriteString("target", target);
        writer.WriteStartArray("arguments");
        foreach (string argumentJson in argumentsJson)
        {
            writer.WriteRawValue(argumentJson);
        }

        writer.WriteEndArray();
        if (streamIds.Count > 0)
        {
            writer.WriteStartArray("streamIds");
            foreach (string streamId in streamIds)
            {
                writer.WriteStringValue(streamId);
            }

            writer.WriteEndArray();
        }
    }

    private static byte[] WriteRecord(Action<Utf8JsonWriter> writeMembers)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeMembers(writer);
            writer.WriteEndObject();
        }

        buffer.Write([HubFrameSplitter.RecordSeparator]);
        return buffer.WrittenSpan.ToArray();
    }
}
