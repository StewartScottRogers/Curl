using System.Text;

namespace Curl.Conformance.Extended.GoogleCloudStorage.Signing;

/// <summary>
/// Builds the canonical request of Cloud Storage's "V4 signing process" from a request the reference endpoint received, written from the
/// documentation (cloud.google.com/storage/docs/authentication/canonical-requests), never from the client's signer: the method, the
/// percent-encoded path, the query sorted by name, the signed headers as <c>name:value</c> lines, the signed header names, and the payload.
/// </summary>
public static class GcsV4CanonicalRequest
{
    /// <summary>Builds the canonical request of <paramref name="request" /> over the headers <paramref name="signedHeaders" /> names.</summary>
    /// <param name="request">The request as received.</param>
    /// <param name="signedHeaders">The lower-case header names the request signed, sorted and joined by <c>;</c>.</param>
    /// <param name="payload">The payload line: the request's <c>x-goog-content-sha256</c> or <c>UNSIGNED-PAYLOAD</c>.</param>
    /// <returns>The canonical request.</returns>
    public static string Of(GcsReferenceRequest request, string signedHeaders, string payload)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(signedHeaders);
        string headerLines = string.Concat(signedHeaders.Split(';').Select(name => $"{name}:{CanonicalValue(request.Headers.GetValueOrDefault(name) ?? string.Empty)}\n"));
        return $"{request.Method}\n{Encode(request.Path, keepSlash: true)}\n{CanonicalQuery(request.Query)}\n{headerLines}\n{signedHeaders}\n{payload}";
    }

    /// <summary>Percent-encodes <paramref name="text" />'s UTF-8 bytes, keeping only the unreserved characters <c>A-Z a-z 0-9 - . _ ~</c> (and <c>/</c> when asked), with upper-case hex.</summary>
    /// <param name="text">The text.</param>
    /// <param name="keepSlash">Whether <c>/</c> stays as it is, as in the path.</param>
    /// <returns>The encoded text.</returns>
    public static string Encode(string text, bool keepSlash)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder encoded = new();
        foreach (byte octet in Encoding.UTF8.GetBytes(text))
        {
            encoded.Append(IsKept((char)octet, keepSlash) ? ((char)octet).ToString() : $"%{octet:X2}");
        }

        return encoded.ToString();
    }

    private const string Unreserved = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    private static bool IsKept(char character, bool keepSlash) =>
        Unreserved.Contains(character, StringComparison.Ordinal) || (keepSlash && character == '/');

    private static string CanonicalQuery(IReadOnlyDictionary<string, string> query) =>
        string.Join('&', query.Select(parameter => (Name: Encode(parameter.Key, keepSlash: false), Value: Encode(parameter.Value, keepSlash: false)))
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
            .Select(parameter => $"{parameter.Name}={parameter.Value}"));

    private static string CanonicalValue(string value) =>
        string.Join(' ', value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
}
