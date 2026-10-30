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
    /// <returns>The same transfer with the URL, method and body replaced and every other member its own.</returns>
    public static ITransferContext WithRequest(this ITransferContext context, CurlUrl url, string? method, ReadOnlyMemory<byte>? body)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(url);
        HttpRequestOptions? http = method is null || context.Http is null ? context.Http : context.Http with { CustomMethod = method };
        return new RetargetedTransferContext(context, url, http, body ?? context.PostData);
    }
}
