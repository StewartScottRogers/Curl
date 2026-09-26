using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// How one request carries its body, as curl 8.21.0 decides it: the method, whether the body
/// is sent chunked, and whether the request waits for <c>100 Continue</c> before sending it.
/// Every rule was measured (BL-175 Notes).
/// </summary>
/// <remarks>
/// A request with a body is a POST unless <c>-X</c> names another method. The body is sent
/// chunked when its length is unknown or an <c>-H</c> value asks for
/// <c>Transfer-Encoding: chunked</c>, and with <c>Content-Length</c> otherwise. curl adds
/// <c>Expect: 100-continue</c> when the length is unknown or above
/// <see cref="ExpectContinueThreshold" />, unless an <c>-H</c> value names <c>Expect</c>; the
/// request waits for <c>100 Continue</c> whenever it carries <c>Expect: 100-continue</c>,
/// its own or an <c>-H</c> one.
/// </remarks>
internal sealed class HttpRequestFraming
{
    /// <summary>
    /// The largest body length curl 8.21.0 sends without <c>Expect: 100-continue</c>: 1 MiB.
    /// </summary>
    internal const long ExpectContinueThreshold = 1048576;

    private HttpRequestFraming(
        string method,
        HttpRequestBody? body,
        long? knownLength,
        bool isChunked,
        bool addsExpect,
        bool awaitsContinue)
    {
        Method = method;
        Body = body;
        KnownLength = knownLength;
        IsChunked = isChunked;
        AddsExpect = addsExpect;
        AwaitsContinue = awaitsContinue;
    }

    /// <summary>
    /// Gets the request method: <c>-X</c>'s, or POST with a body and GET without.
    /// </summary>
    internal string Method { get; }

    /// <summary>
    /// Gets the body to send, or <see langword="null" /> when there is none.
    /// </summary>
    internal HttpRequestBody? Body { get; }

    /// <summary>
    /// Gets the body's length when it is known, or <see langword="null" /> for a
    /// <see cref="StreamBody" /> of unknown length and when there is no body.
    /// </summary>
    internal long? KnownLength { get; }

    /// <summary>
    /// Gets a value indicating whether the body is sent with chunked transfer coding.
    /// </summary>
    internal bool IsChunked { get; }

    /// <summary>
    /// Gets a value indicating whether curl's own <c>Expect: 100-continue</c> line is sent.
    /// </summary>
    internal bool AddsExpect { get; }

    /// <summary>
    /// Gets a value indicating whether the request waits for <c>100 Continue</c> before
    /// sending the body.
    /// </summary>
    internal bool AwaitsContinue { get; }

    /// <summary>
    /// Decides the framing for a request with <paramref name="options" />.
    /// </summary>
    /// <param name="options">The HTTP options.</param>
    /// <param name="customHeaders">The parsed <c>-H</c> values, in command-line order.</param>
    /// <returns>The framing.</returns>
    internal static HttpRequestFraming Of(HttpRequestOptions options, HttpCustomHeader[] customHeaders)
    {
        if (options.Body is not { } body)
        {
            return new HttpRequestFraming(options.CustomMethod ?? "GET", null, null, false, false, false);
        }

        return OfBody(options.CustomMethod ?? "POST", body, customHeaders);
    }

    /// <summary>
    /// Decides the framing for a request that sends <paramref name="body" />.
    /// </summary>
    private static HttpRequestFraming OfBody(string method, HttpRequestBody body, HttpCustomHeader[] customHeaders)
    {
        long? length = body is BytesBody bytes ? bytes.Content.Length : ((StreamBody)body).Length;
        bool wantsExpect = WantsExpect(length);
        bool namesExpect = customHeaders.Any(header => header.Names("Expect"));
        bool isChunked = length is null || AsksForChunked(customHeaders);
        bool awaitsContinue = namesExpect ? AsksForContinue(customHeaders) : wantsExpect;
        return new HttpRequestFraming(method, body, length, isChunked, wantsExpect && !namesExpect, awaitsContinue);
    }

    /// <summary>
    /// Tells whether curl wants <c>Expect: 100-continue</c> for a body of
    /// <paramref name="length" />: when it is unknown or above
    /// <see cref="ExpectContinueThreshold" />.
    /// </summary>
    private static bool WantsExpect(long? length) => length.GetValueOrDefault(long.MaxValue) > ExpectContinueThreshold;

    /// <summary>
    /// Tells whether the first <c>-H</c> value naming <c>Transfer-Encoding</c> lists
    /// <c>chunked</c>.
    /// </summary>
    private static bool AsksForChunked(HttpCustomHeader[] customHeaders) =>
        FirstValueOf(customHeaders, "Transfer-Encoding")?.Contains("chunked", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Tells whether the first <c>-H</c> value naming <c>Expect</c> is
    /// <c>100-continue</c>.
    /// </summary>
    private static bool AsksForContinue(HttpCustomHeader[] customHeaders) =>
        string.Equals(FirstValueOf(customHeaders, "Expect"), "100-continue", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the trimmed value of the first <c>-H</c> value naming <paramref name="name" />
    /// that sends a line, or <see langword="null" /> when none does.
    /// </summary>
    private static string? FirstValueOf(HttpCustomHeader[] customHeaders, string name)
    {
        foreach (HttpCustomHeader header in customHeaders)
        {
            if (header.Names(name) && header.SentLine is { } line)
            {
                return line[(name.Length + 1)..].Trim();
            }
        }

        return null;
    }
}
