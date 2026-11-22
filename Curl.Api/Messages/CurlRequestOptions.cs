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
        bool upload = request.Body is not null && (UploadSchemes.UploadsWithBody(request.Url) || IsHttpPutUpload(request));
        bool post = !upload && (request.Body is not null || request.Method == HttpMethod.Post);
        EasyOptions applied = options with
        {
            Network = options.Network with { Url = request.Url },
            Transfer = ApplyMethod(options.Transfer, request.Method, post, upload),
            Http = options.Http with { HttpHeaders = HeaderLines(request), Post = post },
            Callbacks = options.Callbacks with { Header = header, Write = write },
        };
        return ApplyBody(applied, BodyToApply(request, post, upload), upload, engine, owned);
    }

    /// <summary>Whether a <c>PUT</c> body is sent as an upload, as <c>curl -T</c> does (ADR-1331): any body but a multipart one.</summary>
    private static bool IsHttpPutUpload(CurlRequest request) =>
        request.Method == HttpMethod.Put && request.Body is not CurlMultipartContent;

    /// <summary>The <c>CURLOPT_HTTPHEADER</c> lines of the request: its fields, a <c>Name;</c> line for each empty header, then a <c>Name:</c> line for each removed header.</summary>
    private static string[] HeaderLines(CurlRequest request) =>
        [.. request.Headers.Select(HeaderLine), .. request.EmptyHeaders.Select(name => $"{name};"), .. request.RemovedHeaders.Select(name => $"{name}:")];

    /// <summary>The body to apply: the request's, an empty one for a post with none, or <see langword="null"/> for none.</summary>
    private static CurlContent? BodyToApply(CurlRequest request, bool post, bool upload) =>
        upload ? request.Body : post ? request.Body ?? CurlContent.Empty : null;

    /// <summary><paramref name="options"/> with <paramref name="body"/> applied as an upload or as a post body, or unchanged for no body.</summary>
    private static EasyOptions ApplyBody(EasyOptions options, CurlContent? body, bool upload, ICurlEngine engine, ICollection<IDisposable> owned) =>
        body is null ? options : upload ? body.ApplyAsUpload(options, engine, owned) : body.ApplyTo(options, engine, owned);

    /// <summary>Sets <c>CURLOPT_NOBODY</c> for <c>HEAD</c> and <c>CURLOPT_CUSTOMREQUEST</c> only for a method libcurl would not send on its own.</summary>
    /// <param name="transfer">The transfer options to change.</param>
    /// <param name="method">The request method.</param>
    /// <param name="post">Whether the request posts.</param>
    /// <param name="upload">Whether the request uploads its body (ADR-1316).</param>
    /// <returns>The changed options.</returns>
    private static EasyTransferOptions ApplyMethod(EasyTransferOptions transfer, HttpMethod method, bool post, bool upload) =>
        transfer with
        {
            NoBody = method == HttpMethod.Head,
            Upload = upload || transfer.Upload,
            CustomRequest = upload || IsImpliedBy(method, post) ? null : method.Method,
        };

    /// <summary>
    /// Whether libcurl sends <paramref name="method"/> without <c>CURLOPT_CUSTOMREQUEST</c>:
    /// <c>HEAD</c> always, <c>POST</c> when posting, <c>GET</c> when not.
    /// </summary>
    private static bool IsImpliedBy(HttpMethod method, bool post) =>
        method == HttpMethod.Head || method == (post ? HttpMethod.Post : HttpMethod.Get);

    /// <summary>The <c>CURLOPT_HTTPHEADER</c> line of a field: <c>Name: value</c>, or <c>Name:</c> for an empty value, which removes the header as <c>curl -H "Name:"</c> does (ADR-1335).</summary>
    private static string HeaderLine(KeyValuePair<string, string> field) =>
        field.Value.Length == 0 ? $"{field.Key}:" : $"{field.Key}: {field.Value}";
}
