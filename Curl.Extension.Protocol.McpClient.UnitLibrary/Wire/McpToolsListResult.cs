namespace Curl.Extension.Protocol.McpClient.Wire;

/// <summary>One page of a <c>tools/list</c> result ("Server Features &gt; Tools &gt; Listing Tools").</summary>
/// <param name="Tools">The tools on this page.</param>
/// <param name="NextCursor">The cursor for the next page, or <see langword="null"/> on the last page.</param>
public sealed record McpToolsListResult(IReadOnlyList<McpTool> Tools, string? NextCursor);
