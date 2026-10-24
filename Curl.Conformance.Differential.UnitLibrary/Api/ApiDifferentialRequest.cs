namespace Curl.Conformance.Differential.Api;

/// <summary>
/// One <c>Curl.Api</c> request, described without referencing <c>Curl.Api</c> so this library
/// stays free of the engine: the IntegrationTests host maps it onto a <c>CurlRequest</c>. Every
/// property says what the request sets; one left at its default sets nothing, so the request
/// keeps <c>Curl.Api</c>'s own default.
/// </summary>
/// <param name="Method">The request method, such as <c>GET</c>.</param>
/// <param name="Path">The path on the loopback server, such as <c>/echo</c>.</param>
public sealed record ApiDifferentialRequest(string Method, string Path)
{
    /// <summary>Gets the request headers, in order; an empty value sends that header with no value, as <c>-H 'Name;'</c> does (<c>Curl.Api</c> cannot remove a header).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; } = [];

    /// <summary>Gets the request body sent as bytes (<c>CurlContent.FromBytes</c>), or <see langword="null"/> for none.</summary>
    public string? Body { get; init; }

    /// <summary>Gets the text parts of a multipart body (<c>CurlMultipartContent</c>), in order; empty for none.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> FormFields { get; init; } = [];

    /// <summary>Gets a value indicating whether redirects are followed (<c>FollowLocation.All</c>).</summary>
    public bool FollowsRedirects { get; init; }

    /// <summary>Gets the user name to authenticate with, or <see langword="null"/> for none.</summary>
    public string? UserName { get; init; }

    /// <summary>Gets the password to authenticate with, or <see langword="null"/> for none.</summary>
    public string? Password { get; init; }

    /// <summary>Gets a value indicating whether Digest, not Basic, is the authentication method.</summary>
    public bool UsesDigest { get; init; }

    /// <summary>Gets the <c>Cookie</c> header's pairs (<c>CURLOPT_COOKIE</c>), or <see langword="null"/> for none.</summary>
    public string? Cookie { get; init; }

    /// <summary>Gets a value indicating whether the cookie engine starts empty (<c>CURLOPT_COOKIEFILE</c> <c>""</c>, as <c>-b ''</c>).</summary>
    public bool StartsCookieEngine { get; init; }

    /// <summary>Gets the <c>User-Agent</c> to send, or <see langword="null"/> to send none (<c>Curl.Api</c>'s default).</summary>
    public string? UserAgent { get; init; }
}
