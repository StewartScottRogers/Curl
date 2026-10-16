namespace Curl.Mcp;

/// <summary>
/// The answer to whether a tool may use one path: the path as given, the verdict, the path it
/// resolved to and a reason a refusal can carry to the client (ADR-0548).
/// </summary>
/// <param name="Path">The path as the tool was given it.</param>
/// <param name="Verdict">Whether the path lies inside <c>CURL_MCP_ROOT</c>, and if not, why not.</param>
/// <param name="ResolvedPath">The path with <c>..</c> and symbolic links resolved; <see langword="null" /> when it could not be resolved or no root is set.</param>
/// <param name="Reason">Why the path is allowed or refused, naming the path, for <see cref="McpToolFailure.PathRefused(string, string)" />.</param>
public sealed record McpPathCheck(string Path, McpPathVerdict Verdict, string? ResolvedPath, string Reason)
{
    /// <summary>Whether a tool may use the path: only when it lies inside <c>CURL_MCP_ROOT</c>.</summary>
    public bool IsAllowed => Verdict == McpPathVerdict.Inside;
}
