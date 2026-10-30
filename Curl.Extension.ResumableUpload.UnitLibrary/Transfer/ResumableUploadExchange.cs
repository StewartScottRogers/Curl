using Curl.Extension.ResumableUpload.Http;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>One request of a resumable upload and its final response, the body read whole.</summary>
/// <param name="Method">The request's method.</param>
/// <param name="Uri">The URL the request went to.</param>
/// <param name="StatusCode">The final response's status code.</param>
/// <param name="ReasonPhrase">The final response's reason phrase.</param>
/// <param name="Headers">The final response's header fields, in order.</param>
/// <param name="Body">The final response's body.</param>
internal sealed record ResumableUploadExchange(
    string Method, Uri Uri, int StatusCode, string ReasonPhrase, IReadOnlyList<KeyValuePair<string, string>> Headers, byte[] Body)
{
    /// <summary>Gets a value indicating whether the status code is 2xx (RFC 9110 §15.3).</summary>
    public bool IsSuccess => StatusCode is >= 200 and < 300;

    /// <summary>Finds a header field's value, its name compared case-insensitively.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>The first field's value, or <see langword="null" /> when the response has none.</returns>
    public string? FindHeader(string name) => ResumableUploadHttpResponseHead.FindHeader(Headers, name);
}
