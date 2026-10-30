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
        context.Http is { } http && IsPlainGet(http) && SendsNoBody(context) && AsksForTheWholeResource(context);

    private static bool IsPlainGet(HttpRequestOptions http) => http is { CustomMethod: null, Body: null };

    private static bool SendsNoBody(ITransferContext context) =>
        context is { PostData: null, PostWithoutData: false, Upload: null };

    private static bool AsksForTheWholeResource(ITransferContext context) =>
        context is { NoBody: false, Range: null, ResumeFrom: null, TimeCondition: null };
}
