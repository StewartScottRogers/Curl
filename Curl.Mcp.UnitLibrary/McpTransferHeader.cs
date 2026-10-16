namespace Curl.Mcp;

/// <summary>
/// One HTTP header of the <c>transfer</c> tool (ADR-0548, ADR-0593): a request header it sends, or a
/// response header it returns.
/// </summary>
/// <param name="Name">The header's name, e.g. <c>Content-Type</c>.</param>
/// <param name="Value">The header's value, without leading and trailing white space; may be empty.</param>
public sealed record McpTransferHeader(string Name, string Value);
