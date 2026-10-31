namespace Curl.Extension.Protocol.McpClient.StreamableHttp;

/// <summary>One HTTP/1.1 response's status, header fields and body, not yet read.</summary>
/// <param name="StatusCode">The status code.</param>
/// <param name="Headers">The header fields, in order.</param>
/// <param name="Body">The body.</param>
/// <param name="ConnectionPersists">Whether another request can follow on the connection once the body is read to its end (RFC 9112 §9.3).</param>
internal sealed record McpHttpResponse(int StatusCode, IReadOnlyList<KeyValuePair<string, string>> Headers, McpHttpBody Body, bool ConnectionPersists)
{
    /// <summary>Finds the first field named <paramref name="name"/>, compared case-insensitively.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>Its value, or <see langword="null"/>.</returns>
    public string? FindHeader(string name) => McpHttpResponseHeadReader.FindHeader(Headers, name);
}
