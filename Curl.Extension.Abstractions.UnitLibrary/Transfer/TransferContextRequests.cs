namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// Lets a transfer hook's <see cref="ITransferHook.ExecuteAsync(ITransferContext, TransferHookServices, Func{ITransferContext, ValueTask{TransferResult}})" />
/// run curl's handler with a request of its own, such as a WebDAV <c>PROPFIND</c> or a sync <c>GET</c>, sent through curl's
/// HTTP stack (TLS, proxy, <c>--user</c>, redirects) rather than written by the extension.
/// </summary>
public static class TransferContextRequests
{
    /// <summary>Gives <paramref name="context" /> sent to <paramref name="url" /> with <paramref name="method" /> and <paramref name="body" />.</summary>
    /// <param name="context">The transfer the hook was given.</param>
    /// <param name="url">The URL to send the request to.</param>
    /// <param name="method">The HTTP method, as <c>-X</c> gives it; <see langword="null" /> keeps the transfer's own. A transfer that is not HTTP has none to set.</param>
    /// <param name="body">The request body, as <c>--data-binary</c> gives it; <see langword="null" /> keeps the transfer's own.</param>
    /// <returns>
    /// The same transfer with the URL, method and body replaced - the HTTP body set to <paramref name="body" /> when given, none at all for an empty one -
    /// and the <c>--request-target</c> dropped, since it names a path of the transfer's own URL; every other member its own.
    /// </returns>
    public static ITransferContext WithRequest(this ITransferContext context, CurlUrl url, string? method, ReadOnlyMemory<byte>? body)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(url);
        HttpRequestOptions? http = WithMethod(context.Http, method);
        http = WithoutRequestTarget(http);
        http = WithBody(http, body);
        return new RetargetedTransferContext(context, url, http, body ?? context.PostData, context.Credentials);
    }

    private static HttpRequestOptions? WithMethod(HttpRequestOptions? http, string? method) =>
        method is null || http is null ? http : http with { CustomMethod = method };

    private static HttpRequestOptions? WithoutRequestTarget(HttpRequestOptions? http) =>
        http?.RequestTarget is null ? http : http with { RequestTarget = null };

    private static HttpRequestOptions? WithBody(HttpRequestOptions? http, ReadOnlyMemory<byte>? body) =>
        body is not { } bytes || http is null
            ? http
            : http with { Body = bytes.IsEmpty ? null : new BytesBody(bytes, "application/x-www-form-urlencoded") };
    /// <summary>Gives <paramref name="context" /> without the request headers and credentials of the transfer's own options.</summary>
    /// <remarks>
    /// Drops <c>-H</c>, <c>-A</c>, <c>-e</c>, <c>--referer ;auto</c>, <c>-b</c> cookie strings, <c>--oauth2-bearer</c>, <c>--aws-sigv4</c>,
    /// <c>--user</c> and the HTTP signature, so an extension's own request to a third party, such as an Oblivious HTTP relay,
    /// carries only what the extension adds with <see cref="TransferContextRequestHeaders.WithRequestHeaders" />. Proxy headers stay: they go to the proxy.
    /// </remarks>
    /// <param name="context">The transfer the hook was given.</param>
    /// <returns>The same transfer with those options cleared; a transfer that is not HTTP keeps its own members but loses its credentials.</returns>
    public static ITransferContext WithoutOwnHeaders(this ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HttpRequestOptions? http = context.Http is null
            ? null
            : context.Http with
            {
                Headers = [],
                UserAgent = null,
                Referer = null,
                AutoReferer = false,
                SendsCookieStrings = false,
                BearerToken = null,
                AwsSigV4 = null,
                HttpSignature = null,
            };
        return new RetargetedTransferContext(context, context.Url, http, context.PostData, null);
    }
}
