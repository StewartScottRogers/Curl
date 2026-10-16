namespace Curl.Mcp;

/// <summary>
/// Why a tool returned an MCP tool error rather than output (ADR-0548).
/// </summary>
public enum McpToolFailureKind
{
    /// <summary>curl ran and finished with a nonzero exit code.</summary>
    CurlExit,

    /// <summary>A path an option names lies outside <c>CURL_MCP_ROOT</c>, so nothing ran.</summary>
    PathRefused,

    /// <summary>The MCP request was cancelled before curl finished.</summary>
    Cancelled,

    /// <summary>A field of the tool's request is invalid, so nothing ran.</summary>
    InvalidRequest,
}
