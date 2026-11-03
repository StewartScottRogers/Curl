using System.Text;

namespace Curl.Conformance.Extended.S3.Signing;

/// <summary>
/// Builds the canonical request of AWS Signature Version 4 from a request the reference S3 endpoint received, written from the
/// Amazon S3 API reference ("Signature Calculations for the Authorization Header: Transferring Payload in a Single Chunk"), never
/// from the client's signer: the method, the path UriEncoded once with <c>/</c> kept, the query sorted by encoded name, the signed
/// headers as <c>name:value</c> lines, a blank line, the signed header names, and the hashed payload.
/// </summary>
public static class S3SigV4CanonicalRequest
{
    private const string Unreserved = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    /// <summary>Builds the canonical request of <paramref name="request" /> over the headers <paramref name="signedHeaders" /> names.</summary>
    /// <param name="request">The request as received.</param>
    /// <param name="signedHeaders">The lower-case header names the request signed, sorted and joined by <c>;</c>.</param>
    /// <param name="hashedPayload">The payload line: the request's <c>x-amz-content-sha256</c>.</param>
    /// <returns>The canonical request.</returns>
    public static string Of(S3ReferenceRequest request, string signedHeaders, string hashedPayload)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(signedHeaders);
        string path = Uri.UnescapeDataString(request.Target.Split('?', 2)[0]);
        string headerLines = string.Concat(signedHeaders.Split(';').Select(name => $"{name}:{CanonicalValue(request.Headers.GetValueOrDefault(name) ?? string.Empty)}\n"));
        return $"{request.Method}\n{UriEncode(path, keepSlash: true)}\n{CanonicalQuery(request.Query)}\n{headerLines}\n{signedHeaders}\n{hashedPayload}";
    }

    /// <summary>UriEncodes <paramref name="text" />: every UTF-8 byte but the unreserved <c>A-Z a-z 0-9 - . _ ~</c> (and <c>/</c> when asked) as <c>%XY</c>, upper-case hex.</summary>
    /// <param name="text">The text.</param>
    /// <param name="keepSlash">Whether <c>/</c> stays as it is, as in the object key name.</param>
    /// <returns>The encoded text.</returns>
    public static string UriEncode(string text, bool keepSlash)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder encoded = new();
        foreach (byte octet in Encoding.UTF8.GetBytes(text))
        {
            char character = (char)octet;
            encoded.Append(Unreserved.Contains(character, StringComparison.Ordinal) || (keepSlash && character == '/') ? character.ToString() : $"%{octet:X2}");
        }

        return encoded.ToString();
    }

    private static string CanonicalQuery(IReadOnlyDictionary<string, string> query) =>
        string.Join('&', query.Select(parameter => (Name: UriEncode(parameter.Key, keepSlash: false), Value: UriEncode(parameter.Value, keepSlash: false)))
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
            .Select(parameter => $"{parameter.Name}={parameter.Value}"));

    private static string CanonicalValue(string value) =>
        string.Join(' ', value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
}
