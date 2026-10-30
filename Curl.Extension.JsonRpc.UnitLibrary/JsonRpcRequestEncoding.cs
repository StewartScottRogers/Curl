using System.Buffers;
using System.Text.Json;

namespace Curl.Extension.JsonRpc;

/// <summary>
/// Writes the request body for a <c>--jsonrpc</c> value's calls: one Request object for one call (JSON-RPC 2.0
/// section 4), an array of them, a batch, for several (section 6).
/// </summary>
internal static class JsonRpcRequestEncoding
{
    /// <summary>The media type of the request body.</summary>
    internal const string MediaType = "application/json";

    /// <summary>Writes the body for <paramref name="calls" />.</summary>
    /// <param name="calls">The calls, at least one.</param>
    /// <returns>The body as UTF-8 JSON.</returns>
    internal static byte[] Encode(IReadOnlyList<JsonRpcCall> calls)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            if (calls.Count == 1)
            {
                WriteCall(writer, calls[0]);
            }
            else
            {
                writer.WriteStartArray();
                foreach (JsonRpcCall call in calls)
                {
                    WriteCall(writer, call);
                }

                writer.WriteEndArray();
            }
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Writes one Request object: <c>jsonrpc</c>, <c>method</c>, <c>params</c> when given and <c>id</c> unless a notification.</summary>
    private static void WriteCall(Utf8JsonWriter writer, JsonRpcCall call)
    {
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", "2.0");
        writer.WriteString("method", call.Method);
        if (call.Params is { } parameters)
        {
            writer.WritePropertyName("params");
            writer.WriteRawValue(parameters);
        }

        if (call.Id is { } id)
        {
            writer.WriteNumber("id", id);
        }

        writer.WriteEndObject();
    }
}
