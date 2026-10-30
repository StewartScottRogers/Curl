using Curl.Protocol.Abstractions;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// Decides which transfers the <c>--compression-dictionary</c> hook takes part in: an HTTP or HTTPS request for a
/// whole body (no <c>-I</c>, range or resume), and among those the ones it may advertise a dictionary on.
/// </summary>
internal static class DictionaryTransferRequest
{
    /// <summary>The request URL of a transfer the hook takes part in; <see langword="null" /> when it takes no part.</summary>
    public static Uri? TargetUriOf(ITransferContext context) =>
        AsksForAWholeBody(context) && IsHttp(context.Url) ? TargetUri(context.Url) : null;

    private static bool AsksForAWholeBody(ITransferContext context) =>
        context is { Http: not null, NoBody: false, Range: null, ResumeFrom: null };

    private static bool IsHttp(CurlUrl url) => url.Scheme is "http" or "https";

    /// <summary>
    /// Whether the hook may set <c>Accept-Encoding</c> to the dictionary codings: not when <c>--compressed</c> makes
    /// curl's handler decode the body (it refuses <c>dcb</c> and <c>dcz</c>), nor when <c>-H</c> already names the codings.
    /// </summary>
    public static bool MayAdvertise(ITransferContext context) =>
        context.Http is { Compressed: false } http
        && !http.Headers.Any(line => line.StartsWith("Accept-Encoding:", StringComparison.OrdinalIgnoreCase));

    /// <summary>The value of the response's first <paramref name="name" /> field; <see langword="null" /> when it has none.</summary>
    public static string? ResponseFieldOf(TransferResult result, string name) =>
        result.Report?.ResponseHeaders.FirstOrDefault(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    private static Uri? TargetUri(CurlUrl url)
    {
        var port = url.IsDefaultPort ? string.Empty : ":" + url.Port;
        var query = url.Query is null ? string.Empty : "?" + url.Query;
        // A URL System.Uri cannot hold leaves uri null, so the transfer runs without dictionaries.
        _ = Uri.TryCreate(url.Scheme + "://" + url.Host + port + url.AbsolutePath + query, UriKind.Absolute, out var uri);
        return uri;
    }
}
