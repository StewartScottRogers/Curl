namespace Curl.Extension.SegmentedDownload;

/// <summary>
/// Decides whether a transfer is one <c>--segments</c> splits: a plain HTTP GET of the whole resource, with no body,
/// upload, custom method, <c>HEAD</c>, range, resume or time condition (RFC 9110 §14.2: range requests are defined for GET).
/// Any other transfer runs as curl runs it.
/// </summary>
internal static class SegmentableRequest
{
    /// <summary>Reports whether <paramref name="context" /> is a transfer to split into ranges.</summary>
    public static bool Is(ITransferContext context) =>
        context.Http is { } http && IsPlainGet(http) && SendsNoBody(context) && AsksForTheWholeResource(context) && SetsNoRangeField(http);

    private static bool IsPlainGet(HttpRequestOptions http) => http is { CustomMethod: null, Body: null };

    private static bool SendsNoBody(ITransferContext context) =>
        context is { PostData: null, PostWithoutData: false, Upload: null };

    private static bool AsksForTheWholeResource(ITransferContext context) =>
        context is { NoBody: false, Range: null, ResumeFrom: null, TimeCondition: null };

    // A user -H "Range: ..." or "If-Range: ..." is a range request of the user's own: Range is a single-value field, so the
    // extension's per-segment Range would be a second one beside it. The field name ends at ':' or ';' (curl's "Name;" form).
    private static bool SetsNoRangeField(HttpRequestOptions http) =>
        !http.Headers.Any(header => FieldName(header) is "Range" or "If-Range");

    private static string FieldName(string header)
    {
        var end = header.AsSpan().IndexOfAny(':', ';');
        return (end < 0 ? header : header[..end]).Trim().ToLowerInvariant() switch
        {
            "range" => "Range",
            "if-range" => "If-Range",
            _ => string.Empty,
        };
    }
}
