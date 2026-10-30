namespace Curl.Extension.ResumableUpload.Http;

/// <summary>An interim (1xx) response read before the final one (RFC 9110 §15.2), such as <c>104 Upload Resumption Supported</c>.</summary>
/// <param name="StatusCode">The status code, 100 to 199 but never 101.</param>
/// <param name="Headers">The header fields, in the order received.</param>
public sealed record ResumableUploadHttpInterimResponse(int StatusCode, IReadOnlyList<KeyValuePair<string, string>> Headers)
{
    /// <summary>Finds the first header field named <paramref name="name"/>, compared case-insensitively.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>Its value, or <see langword="null"/> when the response has none.</returns>
    public string? FindHeader(string name) => ResumableUploadHttpResponseHead.FindHeader(Headers, name);
}
