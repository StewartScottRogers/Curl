using System.Text.Json;

namespace Curl.Extension.Protocol.McpClient.Client;

/// <summary>
/// The tool call a <c>-d</c> body asks for: a JSON object with a string <c>name</c> and, optionally, an object
/// <c>arguments</c>, the <c>params</c> of <c>tools/call</c> ("Server Features &gt; Tools &gt; Calling Tools").
/// </summary>
/// <param name="Name">The tool's name.</param>
/// <param name="ArgumentsJson">The arguments object as JSON text; <c>{}</c> when the body gave none.</param>
internal sealed record McpToolCall(string Name, string ArgumentsJson)
{
    /// <summary>Reads the tool call from <paramref name="body"/>, or gives <see langword="null"/> when it is not one.</summary>
    public static McpToolCall? From(ReadOnlyMemory<byte> body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("name", out JsonElement name)
                && name.ValueKind == JsonValueKind.String
                && ArgumentsOf(root) is { } arguments
                ? new McpToolCall(name.GetString()!, arguments)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ArgumentsOf(JsonElement root) =>
        !root.TryGetProperty("arguments", out JsonElement arguments) ? "{}"
        : arguments.ValueKind == JsonValueKind.Object ? arguments.GetRawText()
        : null;
}
