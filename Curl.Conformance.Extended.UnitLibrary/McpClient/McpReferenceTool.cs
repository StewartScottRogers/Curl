namespace Curl.Conformance.Extended.McpClient;

/// <summary>A tool the reference MCP server offers ("Server Features &gt; Tools").</summary>
/// <param name="Name">The tool's name.</param>
/// <param name="Description">The tool's description.</param>
/// <param name="ResultJson">The <c>result</c> object <c>tools/call</c> answers with, as JSON: its <c>content</c> and <c>isError</c>.</param>
public sealed record McpReferenceTool(string Name, string Description, string ResultJson);
