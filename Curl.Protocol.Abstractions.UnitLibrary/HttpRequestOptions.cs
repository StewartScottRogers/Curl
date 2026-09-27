namespace Curl.Protocol.Abstractions;

/// <summary>
/// The HTTP-only options of one transfer, filled by the command-line layer and read only
/// by an HTTP handler (ADR-0014).
/// </summary>
/// <remarks>
/// <see cref="ITransferContext.Http" /> being <see langword="null" /> means no HTTP option
/// was given, and an HTTP handler treats it exactly as <c>new HttpRequestOptions()</c>,
/// every member at its default. <c>-u</c>/<c>--user</c> stays on
/// <see cref="ITransferContext.Credentials" /> and <c>-T</c>/<c>--upload-file</c> on
/// <see cref="ITransferContext.Upload" />; neither is repeated here.
/// </remarks>
public sealed record HttpRequestOptions
{
    /// <summary>
    /// Gets the method from <c>-X</c>/<c>--request</c>, verbatim, or
    /// <see langword="null" /> to let the handler choose GET, HEAD, POST or PUT.
    /// </summary>
    public string? CustomMethod { get; init; }

    /// <summary>
    /// Gets each <c>-H</c>/<c>--header</c> value verbatim, in command-line order; empty
    /// when none was given.
    /// </summary>
    public IReadOnlyList<string> Headers { get; init; } = [];

    /// <summary>
    /// Gets each <c>--proxy-header</c> value verbatim, in command-line order; empty when
    /// none was given. A request sent to a forward proxy carries them after the
    /// <c>-H</c> values; a request sent to the origin, directly or through a tunnel, never
    /// does.
    /// </summary>
    public IReadOnlyList<string> ProxyHeaders { get; init; } = [];

    /// <summary>
    /// Gets the value from <c>-A</c>/<c>--user-agent</c>: <see langword="null" /> sends
    /// curl's own <c>User-Agent</c>, and the empty string sends none.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Gets the referring URL from <c>-e</c>/<c>--referer</c>, or <see langword="null" />
    /// to send no <c>Referer</c> header.
    /// </summary>
    public string? Referer { get; init; }

    /// <summary>
    /// Gets the request body from the <c>-d</c> and <c>-F</c> families, already encoded,
    /// or <see langword="null" /> to send none.
    /// </summary>
    public HttpRequestBody? Body { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>-L</c>/<c>--location</c> was given, so the
    /// handler drains a 3xx body instead of writing it.
    /// </summary>
    public bool FollowRedirects { get; init; }

    /// <summary>
    /// Gets how a status of 400 or above ends the transfer; <see cref="HttpFailMode.None" />
    /// when neither <c>-f</c> nor <c>--fail-with-body</c> was given.
    /// </summary>
    public HttpFailMode Fail { get; init; }

    /// <summary>
    /// Gets the HTTP version the request line asks for;
    /// <see cref="HttpVersionPreference.Http11" /> by default.
    /// </summary>
    public HttpVersionPreference Version { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--compressed</c> was given: ask for and decode a
    /// compressed body.
    /// </summary>
    public bool Compressed { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--raw</c> was given: pass content and transfer
    /// encodings through undecoded.
    /// </summary>
    public bool Raw { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--ignore-content-length</c> was given: read the
    /// body to close regardless of <c>Content-Length</c>.
    /// </summary>
    public bool IgnoreContentLength { get; init; }

    /// <summary>
    /// Gets the request line's target from <c>--request-target</c>, verbatim, or
    /// <see langword="null" /> to derive it from <see cref="ITransferContext.Url" />.
    /// </summary>
    public string? RequestTarget { get; init; }

    /// <summary>
    /// Gets the schemes the authenticator may answer the origin with;
    /// <see cref="HttpAuthSchemes.Basic" />, curl's default, when no scheme option was
    /// given.
    /// </summary>
    public HttpAuthSchemes AuthSchemes { get; init; } = HttpAuthSchemes.Basic;

    /// <summary>
    /// Gets the token from <c>--oauth2-bearer</c>, or <see langword="null" /> when not
    /// given.
    /// </summary>
    public string? BearerToken { get; init; }

    /// <summary>
    /// Gets the proxy already chosen for this URL, or <see langword="null" /> to connect
    /// directly.
    /// </summary>
    public ProxyEndpoint? ForwardProxy { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>-p</c>/<c>--proxytunnel</c> was given: tunnel
    /// through an HTTP-kind proxy with CONNECT even for an <c>http://</c> URL.
    /// </summary>
    public bool ProxyTunnel { get; init; }
}
