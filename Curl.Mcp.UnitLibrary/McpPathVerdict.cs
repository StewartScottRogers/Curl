namespace Curl.Mcp;

/// <summary>What <see cref="McpConfinementRoot.Check(string)" /> decided about one path (ADR-0548).</summary>
public enum McpPathVerdict
{
    /// <summary>The path, fully resolved, lies inside <c>CURL_MCP_ROOT</c>, so a tool may use it.</summary>
    Inside,

    /// <summary>The path, fully resolved, lies outside <c>CURL_MCP_ROOT</c>, so a tool refuses it.</summary>
    Outside,

    /// <summary>The path cannot be resolved (an invalid path, or a symbolic link loop), so a tool refuses it.</summary>
    Unresolvable,

    /// <summary><c>CURL_MCP_ROOT</c> is not set, so a tool refuses every path.</summary>
    NoRootConfigured,
}
