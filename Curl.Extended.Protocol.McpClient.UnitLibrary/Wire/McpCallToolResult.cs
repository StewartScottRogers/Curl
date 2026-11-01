namespace Curl.Extended.Protocol.McpClient.Wire;

/// <summary>A CallToolResult ("Server Features &gt; Tools &gt; Tool Result" and "Error Handling").</summary>
/// <param name="Content">The result's content items, in order.</param>
/// <param name="IsError">Whether the tool reported a tool execution error; <see langword="false"/> when absent.</param>
public sealed record McpCallToolResult(IReadOnlyList<McpContent> Content, bool IsError);
