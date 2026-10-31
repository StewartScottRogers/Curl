namespace Curl.Extension.Protocol.McpClient.Wire;

/// <summary>One content item of a CallToolResult ("Server Features &gt; Tools &gt; Tool Result").</summary>
/// <param name="Type">The item's <c>type</c>: <c>text</c>, <c>image</c>, <c>audio</c>, <c>resource_link</c> or <c>resource</c>.</param>
/// <param name="Text">The <c>text</c> of a text item, or <see langword="null"/> for any other type.</param>
/// <param name="Json">The whole item as JSON text.</param>
public sealed record McpContent(string Type, string? Text, string Json);
