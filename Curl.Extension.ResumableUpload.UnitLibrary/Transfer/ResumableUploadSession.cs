using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// Sends a resumable upload's requests through the rest of curl's transfer (the later hooks and curl's handler), so
/// <c>-v</c>, <c>--trace</c>, <c>--har</c>, <c>--record</c>, authentication, TLS and the proxy see each one as they see
/// any other request (EXT-119, ADR-1379).
/// </summary>
/// <param name="context">The transfer.</param>
/// <param name="next">The rest of curl's transfer.</param>
/// <param name="origin">The transfer's URL: a request to another origin (scheme, host or port) gets no <c>Authorization</c>, <c>Cookie</c> or <c>Proxy-Authorization</c> from <c>-H</c>, <c>-u</c>, <c>--oauth2-bearer</c> or <c>-b</c>, as curl drops them on a cross-origin redirect.</param>
internal sealed class ResumableUploadSession(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next, Uri origin)
{
    /// <summary>
    /// The exit codes meaning the connection or the request's stream broke, so the upload can go on from the server's
    /// offset (ADR-1394): 52 (nothing received), 55 and 56 (sending or receiving broke), 92 (the server reset the HTTP/2
    /// stream), 16 (the HTTP/2 connection closed before the final response head, or failed on a framing or protocol
    /// error) and 18 (the response was cut off part-way).
    /// </summary>
    private static readonly CurlExitCode[] ConnectionBreaks =
    [
        CurlExitCode.GotNothing,
        CurlExitCode.SendError,
        CurlExitCode.RecvError,
        CurlExitCode.Http2Stream,
        CurlExitCode.Http2,
        CurlExitCode.PartialFile,
    ];

    /// <summary>Sends one request and reads its final response whole.</summary>
    /// <param name="method">The request method.</param>
    /// <param name="uri">Where the request goes.</param>
    /// <param name="headers">The flow's header fields; <c>-H</c>'s follow, except one naming a flow field or <c>Content-Length</c>.</param>
    /// <param name="body">The whole upload, seekable, or <see langword="null" /> for a request without a body.</param>
    /// <param name="offset">Where in <paramref name="body" /> the request's content starts.</param>
    /// <returns>The request and its final response.</returns>
    /// <exception cref="ResumableUploadTransferException">The transfer failed; a connection that broke or answered nothing can be resumed.</exception>
    public async Task<ResumableUploadExchange> SendAsync(
        string method, Uri uri, IReadOnlyList<KeyValuePair<string, string>> headers, Stream? body = null, long offset = 0)
    {
        var output = new MemoryStream();
        var head = new MemoryStream();
        var content = body is null ? null : new UploadRemainderStream(body, offset);
        var result = await next(RequestFor(method, uri, headers, content, output, head));
        if (result.ExitCode != CurlExitCode.Ok)
        {
            throw new ResumableUploadTransferException(result.ExitCode, result.ErrorMessage ?? string.Empty, CanResume(result.ExitCode));
        }

        return ExchangeOf(method, uri, result.Report, head, output);
    }

    private static ResumableUploadExchange ExchangeOf(string method, Uri uri, TransferReport? report, MemoryStream head, MemoryStream output) =>
        new(method, uri, report?.ResponseCode ?? 0, report?.ResponseHeaders ?? [], head.ToArray(), output.ToArray());

    private ResumableUploadRequestContext RequestFor(
        string method, Uri uri, IReadOnlyList<KeyValuePair<string, string>> headers, Stream? content, Stream output, Stream head)
    {
        var sameOrigin = SameOrigin(uri);
        return new ResumableUploadRequestContext(
            context,
            CurlUrl.Parse(uri.AbsoluteUri, pathAsIs: true),
            HttpFor(method, headers, sameOrigin),
            content,
            method == "HEAD",
            output,
            head,
            sameOrigin ? context.Credentials : null);
    }

    private static bool CanResume(CurlExitCode exitCode) => ConnectionBreaks.Contains(exitCode);

    private HttpRequestOptions HttpFor(string method, IReadOnlyList<KeyValuePair<string, string>> own, bool sameOrigin)
    {
        var http = context.Http ?? new HttpRequestOptions();
        return http with
        {
            CustomMethod = method == "HEAD" ? null : method,
            Headers = [.. own.Select(field => $"{field.Key}: {field.Value}"), .. http.Headers.Where(line => IsSent(line, own, sameOrigin))],
            Body = null,
            RequestTarget = null,
            Fail = HttpFailMode.None,
            BearerToken = sameOrigin ? http.BearerToken : null,
            SendsCookieStrings = sameOrigin && http.SendsCookieStrings,
        };
    }

    private static bool IsSent(string line, IReadOnlyList<KeyValuePair<string, string>> own, bool sameOrigin)
    {
        var colon = line.IndexOf(':');
        var name = (colon < 0 ? line : line[..colon]).Trim();
        return !name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
            && !own.Any(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            && (sameOrigin || !IsCredential(name));
    }

    private static bool IsCredential(string name) =>
        name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase);

    private bool SameOrigin(Uri uri) =>
        uri.Scheme == origin.Scheme && uri.Port == origin.Port && string.Equals(uri.IdnHost, origin.IdnHost, StringComparison.OrdinalIgnoreCase);
}
