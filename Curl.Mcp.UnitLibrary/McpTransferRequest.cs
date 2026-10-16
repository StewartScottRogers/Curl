namespace Curl.Mcp;

/// <summary>
/// What the <c>transfer</c> tool is asked to do (ADR-0548, ADR-0593): one request, described by
/// fields rather than a curl command line. No field names a local path, so nothing here needs
/// confining to <c>CURL_MCP_ROOT</c>; a <c>file:</c> URL is refused for the same reason.
/// </summary>
/// <param name="Url">The URL to transfer: absolute, with a scheme other than <c>file</c>.</param>
public sealed record McpTransferRequest(string Url)
{
    /// <summary>The request method, an HTTP token sent as given. Default <c>GET</c>.</summary>
    public string Method { get; init; } = "GET";

    /// <summary>Extra request headers, in the order sent. Default none.</summary>
    public IReadOnlyList<McpTransferHeader> Headers { get; init; } = [];

    /// <summary>The request body. Default <see langword="null" />: none.</summary>
    public McpTransferBody? Body { get; init; }

    /// <summary>Whether redirects are followed, as <c>curl -L</c> follows them. Default <see langword="false" />.</summary>
    public bool FollowRedirects { get; init; }

    /// <summary>The longest the whole transfer may take, in seconds; zero or <see langword="null" /> never times out. Default <see langword="null" />.</summary>
    public double? TimeoutSeconds { get; init; }

    /// <summary>Whether the server's certificate goes unverified, as <c>curl -k</c> leaves it. Default <see langword="false" />.</summary>
    public bool Insecure { get; init; }

    /// <summary>The proxy to use, as <c>curl -x</c> takes it. Default <see langword="null" />: the environment's, as libcurl reads it.</summary>
    public string? Proxy { get; init; }

    /// <summary>The <c>User-Agent</c> header. Default <see langword="null" />: none is sent.</summary>
    public string? UserAgent { get; init; }
}
