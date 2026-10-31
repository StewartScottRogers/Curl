using System.Text;

namespace Curl.Extension.S3.Signing;

/// <summary>
/// Builds the canonical request of the S3 reference's "Signature Calculations for the Authorization
/// Header": the method, the canonical URI, the canonical query string, the canonical headers, the
/// signed headers and the payload hash, each on its own line.
/// </summary>
public static class S3CanonicalRequest
{
    /// <summary>Builds the canonical request for <paramref name="request"/>, signing exactly the headers it holds.</summary>
    /// <param name="request">The request whose headers already include <c>host</c>, <c>x-amz-date</c> and <c>x-amz-content-sha256</c>.</param>
    /// <returns>The canonical request and its signed headers list.</returns>
    public static (string CanonicalRequest, string SignedHeaders) Build(S3SigningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var headers = request.Headers
            .GroupBy(header => header.Key.ToLowerInvariant(), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (Name: group.Key, Value: string.Join(',', group.Select(header => CanonicalHeaderValue(header.Value)))))
            .ToList();
        var signedHeaders = string.Join(';', headers.Select(header => header.Name));
        var canonicalHeaders = new StringBuilder();
        foreach (var (name, value) in headers)
        {
            canonicalHeaders.Append(name).Append(':').Append(value).Append('\n');
        }

        var canonicalRequest = string.Join(
            '\n',
            request.Method,
            S3UriEncoder.EncodePath(request.Path),
            CanonicalQueryString(request.QueryParameters),
            canonicalHeaders.ToString(),
            signedHeaders,
            request.PayloadHash);
        return (canonicalRequest, signedHeaders);
    }

    private static string CanonicalQueryString(IReadOnlyList<KeyValuePair<string, string>> parameters) =>
        string.Join('&', parameters
            .Select(parameter => (Name: S3UriEncoder.Encode(parameter.Key), Value: S3UriEncoder.Encode(parameter.Value)))
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
            .ThenBy(parameter => parameter.Value, StringComparer.Ordinal)
            .Select(parameter => parameter.Name + "=" + parameter.Value));

    private static string CanonicalHeaderValue(string value) => string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
