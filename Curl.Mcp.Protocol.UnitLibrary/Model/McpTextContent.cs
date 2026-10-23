using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.Model;

/// <summary>A text content block of a tool-call result: <c>type</c> <c>text</c> and the text.</summary>
public sealed class McpTextContent
{
    /// <summary>Initializes a new instance of the <see cref="McpTextContent" /> class.</summary>
    /// <param name="text">The text.</param>
    public McpTextContent(string text)
    {
        Text = text;
    }

    /// <summary>Gets the content type, always <c>text</c>.</summary>
    [JsonPropertyName("type")]
    public string Type => "text";

    /// <summary>Gets the text.</summary>
    [JsonPropertyName("text")]
    public string Text { get; }
}
