namespace Curl.Mcp;

/// <summary>
/// The request body the <c>transfer</c> tool sends (ADR-0548, ADR-0593): inline text, sent as UTF-8,
/// or base64 for bytes that are not text. Never a file.
/// </summary>
/// <param name="Content">The body as text, or as base64 when <paramref name="IsBase64" /> is set.</param>
/// <param name="IsBase64">Whether <paramref name="Content" /> is base64.</param>
public sealed record McpTransferBody(string Content, bool IsBase64);
