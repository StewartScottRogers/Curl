namespace Curl.Mcp;

/// <summary>
/// The answer to whether the <c>curl</c> tool may run an argument array: allowed, or refused for
/// the first option whose path lies outside <c>CURL_MCP_ROOT</c> or cannot be checked (ADR-0548, ADR-0564).
/// </summary>
/// <param name="IsAllowed">Whether every path the arguments name lies inside <c>CURL_MCP_ROOT</c>.</param>
/// <param name="Option">The refused option as the user wrote it (<c>-o</c>, <c>--output</c>, <c>URL</c> for a URL argument); <see langword="null" /> when allowed.</param>
/// <param name="Path">The offending path or value; <see langword="null" /> when allowed.</param>
/// <param name="Reason">Why the arguments are allowed or refused, for <see cref="McpToolFailure.PathRefused(string, string)" />.</param>
public sealed record McpArgumentCheck(bool IsAllowed, string? Option, string? Path, string Reason)
{
    /// <summary>The name a refusal gives a URL argument, which no option introduces.</summary>
    public const string UrlArgument = "URL";

    /// <summary>The answer for arguments whose every path lies inside <c>CURL_MCP_ROOT</c>.</summary>
    public static McpArgumentCheck Allowed { get; } = new(true, null, null, "Every path the arguments name lies inside CURL_MCP_ROOT.");

    /// <summary>The answer refusing <paramref name="option" /> for <paramref name="path" />.</summary>
    /// <param name="option">The option as the user wrote it.</param>
    /// <param name="path">The offending path or value.</param>
    /// <param name="reason">Why it is refused.</param>
    /// <returns>A refused check.</returns>
    public static McpArgumentCheck Refused(string option, string path, string reason) => new(false, option, path, reason);
}
