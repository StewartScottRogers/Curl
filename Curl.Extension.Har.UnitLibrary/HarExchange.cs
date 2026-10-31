namespace Curl.Extension.Har;

/// <summary>What one HTTP transfer gives its HAR entry (HAR 1.2, "entries"): the request as curl's options built it and the result.</summary>
/// <param name="StartedDateTime">When the transfer started.</param>
/// <param name="Method">The request method.</param>
/// <param name="Url">The absolute URL requested.</param>
/// <param name="RequestHeaders">The request headers known to the hook: curl's <c>User-Agent</c>, <c>Referer</c> and every <c>-H</c> field.</param>
/// <param name="PostData">The <c>-d</c> body, or <see langword="null" /> when the request sent none the hook can see.</param>
/// <param name="Result">The transfer's result, with its report when the server answered.</param>
/// <param name="ResponseBody">The response body as curl's handler wrote it.</param>
/// <param name="TimestampFrequency">The frequency of the transfer's <see cref="TimeProvider" /> timestamps.</param>
internal sealed record HarExchange(
    DateTimeOffset StartedDateTime,
    string Method,
    string Url,
    IReadOnlyList<KeyValuePair<string, string>> RequestHeaders,
    BytesBody? PostData,
    TransferResult Result,
    byte[] ResponseBody,
    long TimestampFrequency);
