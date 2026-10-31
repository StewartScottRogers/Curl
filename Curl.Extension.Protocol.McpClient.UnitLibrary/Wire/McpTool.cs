namespace Curl.Extension.Protocol.McpClient.Wire;

/// <summary>One tool a <c>tools/list</c> result lists ("Server Features &gt; Tools &gt; Tool").</summary>
/// <param name="Name">The tool's unique name.</param>
/// <param name="Description">The tool's description, or <see langword="null"/> when the server gave none.</param>
/// <param name="InputSchemaJson">The tool's <c>inputSchema</c> as JSON text, or <see langword="null"/> when absent.</param>
public sealed record McpTool(string Name, string? Description, string? InputSchemaJson);
