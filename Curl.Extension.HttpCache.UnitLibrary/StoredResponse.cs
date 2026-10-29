namespace Curl.Extension.HttpCache;

/// <summary>
/// One response as a cache stores it: its status line and status code, its header fields in
/// order, its body, and when the request that fetched it was sent and its response received
/// (the request_time and response_time of RFC 9111 §4.2.3).
/// </summary>
/// <param name="StatusLine">The status line without its line ending, such as <c>HTTP/1.1 200 OK</c>.</param>
/// <param name="StatusCode">The status code.</param>
/// <param name="Headers">The header fields, in the order they arrived.</param>
/// <param name="Body">The body bytes.</param>
/// <param name="RequestTime">When the request was sent.</param>
/// <param name="ResponseTime">When the response was received.</param>
public sealed record StoredResponse(
    string StatusLine,
    int StatusCode,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    byte[] Body,
    DateTimeOffset RequestTime,
    DateTimeOffset ResponseTime);
