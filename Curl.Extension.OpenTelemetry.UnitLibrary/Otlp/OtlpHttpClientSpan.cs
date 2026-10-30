using System.Globalization;
using Curl.Extension.OpenTelemetry.TraceContext;

namespace Curl.Extension.OpenTelemetry.Otlp;

/// <summary>
/// Builds the span of one HTTP transfer as OpenTelemetry's HTTP client span semantic conventions (1.26.0, "HTTP client")
/// describe it: named by the method, of kind client, with <c>http.request.method</c>, <c>url.full</c> (credentials
/// redacted), <c>server.address</c>, <c>server.port</c> and any <c>http.response.status_code</c>; an error status and
/// <c>error.type</c> when the transfer failed or the status is 400 or more ("Status").
/// </summary>
internal static class OtlpHttpClientSpan
{
    /// <summary>What <c>url.full</c> puts in place of a user and password ("url.full").</summary>
    internal const string RedactedUserInfo = "REDACTED:REDACTED";

    /// <summary>Gives the span of a finished transfer.</summary>
    /// <param name="sent">The <c>traceparent</c> sent; its parent-id is the span's id.</param>
    /// <param name="context">The transfer.</param>
    /// <param name="result">The transfer's result.</param>
    /// <param name="start">When the transfer started.</param>
    /// <param name="end">When the transfer ended.</param>
    /// <returns>The span.</returns>
    internal static OtlpSpan For(TraceParent sent, ITransferContext context, TransferResult result, DateTimeOffset start, DateTimeOffset end)
    {
        var method = result.Report?.Method ?? context.Http!.CustomMethod ?? "GET";
        var statusCode = result.Report?.ResponseCode ?? 0;
        var errorType = ErrorTypeOf(result.ExitCode, statusCode);
        var attributes = AttributesOf(method, context.Url, statusCode, errorType);
        return new OtlpSpan(sent.TraceId, sent.ParentId, method, UnixNanoOf(start), UnixNanoOf(end), attributes, errorType is not null, result.ErrorMessage);
    }

    private static List<OtlpAttribute> AttributesOf(string method, CurlUrl url, int statusCode, string? errorType)
    {
        List<OtlpAttribute> attributes =
        [
            OtlpAttribute.String("http.request.method", method),
            OtlpAttribute.String("url.full", Redacted(url.OriginalString)),
            OtlpAttribute.String("server.address", url.Host),
            OtlpAttribute.Int("server.port", url.Port),
        ];
        if (statusCode > 0)
        {
            attributes.Add(OtlpAttribute.Int("http.response.status_code", statusCode));
        }

        if (errorType is not null)
        {
            attributes.Add(OtlpAttribute.String("error.type", errorType));
        }

        return attributes;
    }

    /// <summary>Puts <see cref="RedactedUserInfo" /> in place of any user information in <paramref name="url" />.</summary>
    /// <param name="url">The URL as given.</param>
    /// <returns>The URL with no credentials.</returns>
    internal static string Redacted(string url)
    {
        var authorityStart = url.IndexOf("://", StringComparison.Ordinal) + 3;
        var authorityEnd = url.IndexOfAny(['/', '?', '#'], authorityStart);
        var authority = authorityEnd < 0 ? url[authorityStart..] : url[authorityStart..authorityEnd];
        var at = authority.LastIndexOf('@');
        return authorityStart < 3 || at < 0 ? url : url[..authorityStart] + RedactedUserInfo + url[(authorityStart + at)..];
    }

    private static string? ErrorTypeOf(CurlExitCode exitCode, int statusCode) =>
        exitCode != CurlExitCode.Ok ? exitCode.ToString()
        : statusCode >= 400 ? statusCode.ToString(CultureInfo.InvariantCulture)
        : null;

    private static long UnixNanoOf(DateTimeOffset time) => (time - DateTimeOffset.UnixEpoch).Ticks * 100;
}
