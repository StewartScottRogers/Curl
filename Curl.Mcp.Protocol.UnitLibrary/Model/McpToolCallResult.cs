using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.Model;

/// <summary>The result of <c>tools/call</c>: the tool's text content blocks and whether the tool failed.</summary>
public sealed class McpToolCallResult
{
    /// <summary>Initializes a new instance of the <see cref="McpToolCallResult" /> class.</summary>
    /// <param name="content">The text content blocks.</param>
    /// <param name="isError">Whether the tool failed.</param>
    public McpToolCallResult(IReadOnlyList<McpTextContent> content, bool isError)
    {
        Content = content;
        IsError = isError;
    }

    /// <summary>Gets the text content blocks.</summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<McpTextContent> Content { get; }

    /// <summary>Gets whether the tool failed; always written, <c>false</c> included.</summary>
    [JsonPropertyName("isError")]
    public bool IsError { get; }

    /// <summary>Creates the result of a tool that succeeded with one text block.</summary>
    /// <param name="text">The text.</param>
    /// <returns>A result with one text block and <see cref="IsError" /> <c>false</c>.</returns>
    public static McpToolCallResult Success(string text) => new([new McpTextContent(text)], isError: false);

    /// <summary>Creates the result of a tool that failed with one text block.</summary>
    /// <param name="text">The text describing the failure.</param>
    /// <returns>A result with one text block and <see cref="IsError" /> <c>true</c>.</returns>
    public static McpToolCallResult Error(string text) => new([new McpTextContent(text)], isError: true);
}
