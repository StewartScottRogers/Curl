using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Curl.Mcp;

/// <summary>
/// The MCP tools <c>parse_url</c> and <c>build_url</c>: forward their fields to
/// <see cref="McpUrlTool" /> and return its result as JSON text, flagged as a tool error when the
/// result is one (ADR-0548).
/// </summary>
/// <param name="tool">The tools' logic.</param>
[McpServerToolType]
public sealed class CurlUrlTool(McpUrlTool tool)
{
    /// <summary>The description an agent reads for the <c>parse_url</c> tool.</summary>
    public const string ParseDescription =
        "Parses a URL with libcurl's URL API (curl_url_set and curl_url_get) and returns the URL as the parser rebuilt it and its "
        + "parts: scheme, user, password, options, host, port, path, query, fragment and zoneId. A part the URL does not have is "
        + "null, never an empty string. A URL the parser refuses is a tool error naming the CURLUcode.";

    /// <summary>The description an agent reads for the <c>build_url</c> tool.</summary>
    public const string BuildDescription =
        "Builds a URL from its parts with libcurl's URL API: sets only the parts supplied (curl_url_set), in libcurl's order, and "
        + "returns the whole URL (curl_url_get). A part or URL the parser refuses, such as a bad port or no host, is a tool error "
        + "naming the part and the CURLUcode.";

    private const string FlagsDescription =
        "libcurl URL flags by name (CURLU_* without the prefix, in CamelCase), e.g. NonSupportScheme, DefaultPort, UrlDecode, "
        + "GuessScheme, PathAsIs; none when omitted.";

    /// <summary>Parses one URL.</summary>
    /// <param name="url">The URL.</param>
    /// <param name="flags">The URL flags by name.</param>
    /// <returns>The tool result.</returns>
    [McpServerTool(Name = "parse_url", ReadOnly = true, Idempotent = true)]
    [Description(ParseDescription)]
    public CallToolResult ParseUrl(
        [Description("The URL to parse, e.g. https://user:pw@example.com:8443/a?x=1#f.")] string url,
        [Description(FlagsDescription)] string[]? flags = null)
    {
        McpParseUrlToolResult result = tool.Parse(url, flags);
        return ToCallToolResult(result.IsError, JsonSerializer.Serialize(result, CurlMcpJsonContext.Default.McpParseUrlToolResult));
    }

    /// <summary>Builds one URL from its parts.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <param name="user">The user name.</param>
    /// <param name="password">The password.</param>
    /// <param name="options">The login options.</param>
    /// <param name="host">The host.</param>
    /// <param name="port">The port.</param>
    /// <param name="path">The path.</param>
    /// <param name="query">The query.</param>
    /// <param name="fragment">The fragment.</param>
    /// <param name="zoneId">The IPv6 zone identifier.</param>
    /// <param name="flags">The URL flags by name.</param>
    /// <returns>The tool result.</returns>
    [McpServerTool(Name = "build_url", ReadOnly = true, Idempotent = true)]
    [Description(BuildDescription)]
    public CallToolResult BuildUrl(
        [Description("The scheme, e.g. https.")] string? scheme = null,
        [Description("The user name.")] string? user = null,
        [Description("The password.")] string? password = null,
        [Description("The login options (IMAP, POP3 and SMTP).")] string? options = null,
        [Description("The host name or address.")] string? host = null,
        [Description("The port number, as text.")] string? port = null,
        [Description("The path, e.g. /a/b.")] string? path = null,
        [Description("The query, without its '?'.")] string? query = null,
        [Description("The fragment, without its '#'.")] string? fragment = null,
        [Description("The IPv6 zone identifier.")] string? zoneId = null,
        [Description(FlagsDescription)] string[]? flags = null)
    {
        McpUrlParts parts = new(scheme, user, password, options, host, port, path, query, fragment, zoneId);
        McpBuildUrlToolResult result = tool.Build(parts, flags);
        return ToCallToolResult(result.IsError, JsonSerializer.Serialize(result, CurlMcpJsonContext.Default.McpBuildUrlToolResult));
    }

    private static CallToolResult ToCallToolResult(bool isError, string json) =>
        new() { IsError = isError, Content = [new TextContentBlock { Text = json }] };
}
