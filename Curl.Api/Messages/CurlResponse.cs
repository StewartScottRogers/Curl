using Curl.Api.Info;

namespace Curl.Api;

/// <summary>
/// What <see cref="CurlClient.SendAsync"/> received for one request: the final response's status,
/// reason and version, its headers and trailers as received, everything the transfer reported, and
/// the body. Immutable apart from reading <see cref="Body"/>.
/// </summary>
public sealed record CurlResponse
{
    /// <summary>The final response's status code (<c>CURLINFO_RESPONSE_CODE</c>); 0 when none was received.</summary>
    public int StatusCode { get; init; }

    /// <summary>The final status line's reason phrase; empty when it had none (HTTP/2 and HTTP/3 have none).</summary>
    public string ReasonPhrase { get; init; } = "";

    /// <summary>
    /// The HTTP version the response came over (<c>CURLINFO_HTTP_VERSION</c>);
    /// <see cref="System.Net.HttpVersion.Unknown"/> when no HTTP was spoken.
    /// </summary>
    public Version Version { get; init; } = System.Net.HttpVersion.Unknown;

    /// <summary>
    /// The final response's header fields, in the order received with their casing, duplicates
    /// included; the headers of an earlier response (a <c>1xx</c> or a followed redirect) are not here.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; } = [];

    /// <summary>The trailer fields received after the body, in the order received with their casing.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Trailers { get; init; } = [];

    /// <summary>Everything the transfer reported (<c>curl_easy_getinfo</c>).</summary>
    public TransferInfo Info { get; init; } = new();

    /// <summary>The response body. Always empty for now: streamed bodies are BL-2501.</summary>
    public Stream Body { get; init; } = Stream.Null;
}
