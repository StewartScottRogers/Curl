using Curl.Api.Callbacks;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;

namespace Curl.Api;

/// <summary>Maps a <see cref="CurlRequest"/> onto an easy handle's <see cref="EasyOptions"/>.</summary>
internal static class CurlRequestOptions
{
    /// <summary>
    /// <paramref name="options"/> with the request's URL, method, headers and body set, the header
    /// lines going to <paramref name="header"/> and the body bytes to <paramref name="write"/>.
    /// </summary>
    /// <param name="options">The handle's options, with the client's defaults already applied.</param>
    /// <param name="request">The request to perform.</param>
    /// <param name="header">The <c>CURLOPT_HEADERFUNCTION</c> that takes the response's header lines.</param>
    /// <param name="write">The <c>CURLOPT_WRITEFUNCTION</c> that takes the response's body.</param>
    /// <param name="engine">The engine performing the request, whose handles the body may create.</param>
    /// <param name="owned">Takes every handle the body creates, to dispose when the transfer ends.</param>
    /// <returns>The options to perform the request with.</returns>
    public static EasyOptions Apply(EasyOptions options, CurlRequest request, HeaderCallback header, WriteCallback write, ICurlEngine engine, ICollection<IDisposable> owned)
    {
        bool post = request.Body is not null || request.Method == HttpMethod.Post;
        EasyOptions applied = options with
        {
            Network = options.Network with { Url = request.Url },
            Transfer = ApplyMethod(options.Transfer, request.Method, post),
            Http = options.Http with { HttpHeaders = [.. request.Headers.Select(HeaderLine)], Post = post },
            Callbacks = options.Callbacks with { Header = header, Write = write },
        };
        return post ? (request.Body ?? CurlContent.Empty).ApplyTo(applied, engine, owned) : applied;
    }

    /// <summary>Sets <c>CURLOPT_NOBODY</c> for <c>HEAD</c> and <c>CURLOPT_CUSTOMREQUEST</c> only for a method libcurl would not send on its own.</summary>
    /// <param name="transfer">The transfer options to change.</param>
    /// <param name="method">The request method.</param>
    /// <param name="post">Whether the request posts.</param>
    /// <returns>The changed options.</returns>
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

    /// <summary>The <c>CURLOPT_HTTPHEADER</c> line of a field: <c>Name: value</c>, or <c>Name;</c> for an empty value, which is how libcurl is told to send a header with no value.</summary>
    private static string HeaderLine(KeyValuePair<string, string> field) =>
        field.Value.Length == 0 ? $"{field.Key};" : $"{field.Key}: {field.Value}";
}
