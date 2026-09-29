using System.Text;

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
    /// Gets the encoding that turns command-line text in the request head - the
    /// <see cref="Headers" />, <see cref="ProxyHeaders" />, <see cref="UserAgent" /> and
    /// <see cref="Referer" /> values - into bytes, as the platform's curl receives its
    /// arguments (ADR-0067): the system ANSI code page with best fit on Windows, UTF-8
    /// elsewhere. <see cref="Encoding.Latin1" />, one byte per character, when not set.
    /// </summary>
    public Encoding CommandLineTextEncoding { get; init; } = Encoding.Latin1;

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
    /// Gets a value indicating whether a followed redirect sends the URL it came from as its
    /// <c>Referer</c>, without user information or fragment, as <c>-e "...;auto"</c> asks
    /// curl 8.21.0 to (BL-361 Notes).
    /// </summary>
    public bool AutoReferer { get; init; }

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
    /// Gets the <c>--max-redirs</c> limit: 50 when not given, as in curl 8.21.0, and <c>-1</c>
    /// for no limit. curl counts each resend after a <c>417 Expectation Failed</c> as a
    /// followed redirect, with or without <c>-L</c>, so the handler ends a resend that would
    /// pass it with exit 47 (measured, BL-396 Notes).
    /// </summary>
    public int MaxRedirects { get; init; } = 50;

    /// <summary>
    /// Gets how many redirects the transfer followed before this request: 0 for the first,
    /// and the count so far for a hop <c>-L</c> follows, whose resends count against the same
    /// <see cref="MaxRedirects" /> (BL-396 Notes).
    /// </summary>
    public int RedirectsFollowed { get; init; }

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
    /// Gets the happy-eyeballs timeout from <c>--happy-eyeballs-timeout-ms</c>: how long a
    /// <c>--http3</c> transfer waits for the QUIC handshake before it also starts a TCP
    /// connect (ADR-0144 section 4); curl's default of 200 milliseconds when not given.
    /// </summary>
    public TimeSpan HappyEyeballsTimeout { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets a value indicating whether <c>--compressed</c> was given: ask for and decode a
    /// compressed body.
    /// </summary>
    public bool Compressed { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--tr-encoding</c> was given: send <c>TE: gzip</c>
    /// and decode a compressed Transfer-Encoding.
    /// </summary>
    public bool TransferEncoding { get; init; }

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
