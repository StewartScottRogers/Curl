using System.Globalization;
using System.Text;

namespace Curl.Conformance.Extended.OciRegistry;

/// <summary>One response of the OCI reference registry.</summary>
/// <param name="StatusCode">The HTTP status code.</param>
/// <param name="Headers">The headers besides <c>Content-Length</c>, which <see cref="ToBytes" /> adds.</param>
/// <param name="Body">The body; its length is sent even when the request was <c>HEAD</c> and the body is not.</param>
public sealed record OciReferenceResponse(int StatusCode, IReadOnlyList<KeyValuePair<string, string>> Headers, byte[] Body)
{
    /// <summary>
    /// The error codes of the Distribution Specification's "Error Codes" table (code-1 to code-14), each with the status a
    /// registry answers it with and the table's message.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (int StatusCode, string Message)> ErrorCodes = new Dictionary<string, (int, string)>(StringComparer.Ordinal)
    {
        ["BLOB_UNKNOWN"] = (404, "blob unknown to registry"),
        ["BLOB_UPLOAD_INVALID"] = (400, "blob upload invalid"),
        ["BLOB_UPLOAD_UNKNOWN"] = (404, "blob upload unknown to registry"),
        ["DIGEST_INVALID"] = (400, "provided digest did not match uploaded content"),
        ["MANIFEST_BLOB_UNKNOWN"] = (404, "manifest references a manifest or blob unknown to registry"),
        ["MANIFEST_INVALID"] = (400, "manifest invalid"),
        ["MANIFEST_UNKNOWN"] = (404, "manifest unknown to registry"),
        ["NAME_INVALID"] = (400, "invalid repository name"),
        ["NAME_UNKNOWN"] = (404, "repository name not known to registry"),
        ["SIZE_INVALID"] = (400, "provided length did not match content length"),
        ["UNAUTHORIZED"] = (401, "authentication required"),
        ["DENIED"] = (403, "requested access to the resource is denied"),
        ["UNSUPPORTED"] = (405, "the operation is unsupported"),
        ["TOOMANYREQUESTS"] = (429, "too many requests"),
    };

    /// <summary>A response with a body.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The body.</param>
    /// <param name="headers">The headers besides <c>Content-Length</c>, as name and value pairs.</param>
    /// <returns>The response.</returns>
    public static OciReferenceResponse Of(int statusCode, byte[] body, params string[] headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return new(statusCode, [.. headers.Chunk(2).Select(pair => KeyValuePair.Create(pair[0], pair[1]))], body);
    }

    /// <summary>An error response with the specification's body, <c>{"errors":[{"code","message","detail"}]}</c>, at the code's status.</summary>
    /// <param name="code">A code of <see cref="ErrorCodes" />.</param>
    /// <param name="headers">Further headers, as name and value pairs.</param>
    /// <returns>The response.</returns>
    public static OciReferenceResponse Error(string code, params string[] headers)
    {
        (int statusCode, string message) = ErrorCodes[code];
        byte[] body = Encoding.UTF8.GetBytes($$"""{"errors":[{"code":"{{code}}","message":"{{message}}","detail":null}]}""");
        return Of(statusCode, body, ["Content-Type", "application/json", .. headers]);
    }

    /// <summary>Gives the bytes sent on the wire: status line, headers, <c>Content-Length</c>, the blank line and the body.</summary>
    /// <param name="withBody">Whether to send the body; <see langword="false" /> for a <c>HEAD</c>.</param>
    /// <returns>The bytes.</returns>
    public byte[] ToBytes(bool withBody)
    {
        StringBuilder head = new(string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 {StatusCode} Status\r\n"));
        foreach ((string name, string value) in Headers)
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }

        head.Append(CultureInfo.InvariantCulture, $"Content-Length: {Body.Length}\r\n\r\n");
        return [.. Encoding.Latin1.GetBytes(head.ToString()), .. withBody ? Body : []];
    }
}
