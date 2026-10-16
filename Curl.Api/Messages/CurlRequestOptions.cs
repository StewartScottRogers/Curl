using Curl.Api.Callbacks;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;

namespace Curl.Api;

/// <summary>Maps a <see cref="CurlRequest"/> onto an easy handle's <see cref="EasyOptions"/>.</summary>
internal static class CurlRequestOptions
{
    /// <summary>
    /// <paramref name="options"/> with the request's URL, method, headers and body set, the header
    /// lines going to <paramref name="header"/> and the body bytes discarded (streamed bodies are BL-2501).
    /// </summary>
    /// <param name="options">The handle's options, with the client's defaults already applied.</param>
    /// <param name="request">The request to perform.</param>
    /// <param name="header">The <c>CURLOPT_HEADERFUNCTION</c> that takes the response's header lines.</param>
    /// <returns>The options to perform the request with.</returns>
    public static EasyOptions Apply(EasyOptions options, CurlRequest request, HeaderCallback header)
    {
        bool post = request.Body is not null || request.Method == HttpMethod.Post;
        EasyOptions applied = options with
        {
            Network = options.Network with { Url = request.Url },
            Transfer = ApplyMethod(options.Transfer, request.Method, post),
            Http = options.Http with { HttpHeaders = [.. request.Headers.Select(HeaderLine)], Post = post },
            Callbacks = options.Callbacks with { Header = header, Write = DiscardBody },
        };
        return post ? (request.Body ?? CurlContent.Empty).ApplyTo(applied) : applied;
    }

    private static EasyTransferOptions ApplyMethod(EasyTransferOptions transfer, HttpMethod method, bool post) =>
        transfer with
        {
            NoBody = method == HttpMethod.Head,
            CustomRequest = IsImpliedBy(method, post) ? null : method.Method,
        };

    /// <summary>
    /// Whether libcurl sends <paramref name="method"/> without <c>CURLOPT_CUSTOMREQUEST</c>:
    /// <c>HEAD</c> always, <c>POST</c> when posting, <c>GET</c> when not.
    /// </summary>
    private static bool IsImpliedBy(HttpMethod method, bool post) =>
        method == HttpMethod.Head || method == (post ? HttpMethod.Post : HttpMethod.Get);

    private static string HeaderLine(KeyValuePair<string, string> field) =>
        field.Value.Length == 0 ? $"{field.Key};" : $"{field.Key}: {field.Value}";

    private static long DiscardBody(ReadOnlySpan<byte> data) => data.Length;
}
