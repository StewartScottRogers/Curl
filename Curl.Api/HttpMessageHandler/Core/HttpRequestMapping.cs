using CurlHttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Api;

/// <summary>Maps an <see cref="HttpRequestMessage"/> onto the <see cref="CurlRequest"/> that sends it (ADR-0647).</summary>
internal static class HttpRequestMapping
{
    /// <summary>
    /// The <see cref="CurlRequest"/> for <paramref name="request"/>: its method, absolute URI, request
    /// headers then content headers (each name once, its values joined as <see cref="HttpClient"/> sends
    /// them, <c>Content-Length</c> left to the engine), its content as a stream body of the content's
    /// length, and its version as <c>CURLOPT_HTTP_VERSION</c> on the client's HTTP options.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="clientHttp">The client's HTTP options, kept apart from the version; <see langword="null"/> for libcurl's.</param>
    /// <param name="cancellationToken">Cancels reading the content's stream.</param>
    /// <returns>The request to send.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="request"/> has no absolute URI.</exception>
    /// <exception cref="HttpRequestException">The engine speaks no version the request's version policy allows (<see cref="HttpVersionPolicyGuard.SupportedVersion"/>).</exception>
    public static async Task<CurlRequest> ToCurlRequestAsync(HttpRequestMessage request, CurlHttpOptions? clientHttp, CancellationToken cancellationToken)
    {
        Uri uri = request.RequestUri is { IsAbsoluteUri: true } absolute
            ? absolute
            : throw new InvalidOperationException("The request URI must be set and absolute.");
        CurlHttpVersion version = VersionOf(HttpVersionPolicyGuard.SupportedVersion(request.Version, request.VersionPolicy), request.VersionPolicy);
        CurlContent? body = request.Content is null ? null : await BodyOfAsync(request.Content, cancellationToken).ConfigureAwait(false);
        return new CurlRequest(request.Method, uri.AbsoluteUri)
        {
            Headers = [.. FieldsOf(request.Headers), .. FieldsOf(request.Content?.Headers)],
            Body = body,
            Http = (clientHttp ?? new CurlHttpOptions()) with { HttpVersion = version },
        };
    }

    /// <summary>
    /// The <c>CURLOPT_HTTP_VERSION</c> for a request <paramref name="version"/> and <paramref name="policy"/>:
    /// 3.0 and 2.0 ask for HTTP/3 and HTTP/2 with fallback, or only that version under
    /// <see cref="HttpVersionPolicy.RequestVersionExact"/>; 1.0 and 1.1 ask for that version, or for
    /// libcurl's default under <see cref="HttpVersionPolicy.RequestVersionOrHigher"/>.
    /// </summary>
    /// <param name="version">The request's version.</param>
    /// <param name="policy">The request's version policy.</param>
    /// <returns>The version to ask the engine for.</returns>
    internal static CurlHttpVersion VersionOf(Version version, HttpVersionPolicy policy) => version.Major switch
    {
        3 => policy == HttpVersionPolicy.RequestVersionExact ? CurlHttpVersion.Http3Only : CurlHttpVersion.Http3,
        2 => policy == HttpVersionPolicy.RequestVersionExact ? CurlHttpVersion.Http2PriorKnowledge : CurlHttpVersion.Http2,
        _ => Http1VersionOf(version, policy),
    };

    /// <summary>The <c>CURLOPT_HTTP_VERSION</c> for an HTTP/1 request: libcurl's default under <see cref="HttpVersionPolicy.RequestVersionOrHigher"/>, else 1.0 or 1.1.</summary>
    /// <param name="version">The request's version.</param>
    /// <param name="policy">The request's version policy.</param>
    /// <returns>The version to ask the engine for.</returns>
    private static CurlHttpVersion Http1VersionOf(Version version, HttpVersionPolicy policy) =>
        policy == HttpVersionPolicy.RequestVersionOrHigher ? CurlHttpVersion.None
        : version == System.Net.HttpVersion.Version10 ? CurlHttpVersion.Http10
        : CurlHttpVersion.Http11;

    /// <summary>The content's stream, from where it stands, with the content's length when known.</summary>
    /// <param name="content">The request content.</param>
    /// <param name="cancellationToken">Cancels reading the stream.</param>
    /// <returns>The stream body.</returns>
    private static async Task<CurlContent> BodyOfAsync(HttpContent content, CancellationToken cancellationToken)
    {
        Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return CurlContent.FromStream(stream, content.Headers.ContentLength);
    }

    /// <summary>Each header of <paramref name="headers"/> but <c>Content-Length</c>, its values joined; none for <see langword="null"/>.</summary>
    /// <param name="headers">The headers, or <see langword="null"/>.</param>
    /// <returns>The header fields.</returns>
    private static IEnumerable<KeyValuePair<string, string>> FieldsOf(System.Net.Http.Headers.HttpHeaders? headers) =>
        from header in headers?.NonValidated ?? default
        where !header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
        select new KeyValuePair<string, string>(header.Key, header.Value.ToString());
}
