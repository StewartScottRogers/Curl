using System.Text.Json;

namespace Curl.Extension.ConnectRpc;

/// <summary>
/// What a Connect streaming response says (Connect protocol reference, "Streaming-Response" and "End-Stream
/// Message"): its messages, each written on its own line, and the error its end-of-stream message carries, if any.
/// </summary>
/// <param name="Messages">The messages, each followed by a line feed.</param>
/// <param name="Error">The error as curl's message shows it, or <see langword="null" /> when the call succeeded.</param>
internal sealed record ConnectStreamReply(byte[] Messages, string? Error)
{
    /// <summary>Reads a streaming response body.</summary>
    /// <param name="framed">The response body.</param>
    /// <returns>Its messages and error.</returns>
    /// <exception cref="FormatException">The body is not enveloped messages ending in an uncompressed end-of-stream message whose JSON is valid.</exception>
    internal static ConnectStreamReply Read(ReadOnlySpan<byte> framed)
    {
        using MemoryStream messages = new();
        foreach (ConnectEnvelopedMessage message in ConnectEnvelope.Decode(framed))
        {
            if ((message.Flags & ConnectEnvelope.CompressedFlag) != 0)
            {
                throw new FormatException("Connect: the server sent a compressed message, but no compression was offered");
            }

            if ((message.Flags & ConnectEnvelope.EndStreamFlag) != 0)
            {
                return new ConnectStreamReply(messages.ToArray(), ErrorOf(message.Payload));
            }

            messages.Write(message.Payload);
            messages.WriteByte((byte)'\n');
        }

        throw new FormatException("Connect: the response ends without an end-of-stream message");
    }

    /// <summary>Gives the error the end-of-stream message's <c>error</c> member describes, or <see langword="null" /> for none.</summary>
    private static string? ErrorOf(byte[] endStream)
    {
        try
        {
            using JsonDocument end = JsonDocument.Parse(endStream);
            return end.RootElement.ValueKind == JsonValueKind.Object && end.RootElement.TryGetProperty("error", out JsonElement error)
                ? Describe(error)
                : null;
        }
        catch (JsonException)
        {
            throw new FormatException("Connect: the end-of-stream message is not JSON");
        }
    }

    /// <summary>Gives "Connect error &lt;code&gt;: &lt;message&gt;", the code <c>unknown</c> when the error names none ("Error Codes").</summary>
    private static string Describe(JsonElement error)
    {
        string code = TextOf(error, "code") ?? "unknown";
        return TextOf(error, "message") is { } message ? $"Connect error {code}: {message}" : $"Connect error {code}";
    }

    /// <summary>Gives the string member <paramref name="name" /> of <paramref name="element" />, or <see langword="null" />.</summary>
    private static string? TextOf(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
