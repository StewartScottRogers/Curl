using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.GoogleCloudStorage.Signing;

/// <summary>
/// Signs a Cloud Storage XML API request with an HMAC key as the "V4 signing process" says, in the
/// <c>Authorization</c> header form: it returns the <c>x-goog-date</c>, <c>x-goog-content-sha256</c>
/// and <c>Authorization</c> headers to add.
/// </summary>
/// <param name="timeProvider">The clock whose current UTC time is the request's timestamp.</param>
public sealed class GcsRequestSigner(TimeProvider timeProvider)
{
    /// <summary>The payload hash of a request whose payload is not signed.</summary>
    public const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    private const string Algorithm = "GOOG4-HMAC-SHA256";
    private const string Service = "storage";
    private const string TerminationString = "goog4_request";

    /// <summary>Signs <paramref name="request"/> with <paramref name="key"/> at the clock's current time.</summary>
    /// <param name="request">The request to sign; its headers must include <c>host</c> and must not include the headers this adds.</param>
    /// <param name="key">The HMAC key and region to sign with.</param>
    /// <returns>The <c>x-goog-date</c>, <c>x-goog-content-sha256</c> and <c>Authorization</c> headers, in that order.</returns>
    public IReadOnlyList<KeyValuePair<string, string>> Sign(GcsSigningRequest request, GcsHmacKey key)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(key);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var timestamp = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var googleHeaders = new[]
        {
            new KeyValuePair<string, string>("x-goog-date", timestamp),
            new KeyValuePair<string, string>("x-goog-content-sha256", request.PayloadHash),
        };
        var (canonicalRequest, signedHeaders) = GcsCanonicalRequest.Build(request with { Headers = [.. request.Headers, .. googleHeaders] });
        var scope = CredentialScope(date, key.Region);
        var signature = Hex(HmacSha256(SigningKey(key, date), StringToSign(timestamp, scope, canonicalRequest)));
        var authorization = $"{Algorithm} Credential={key.AccessId}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}";
        return [.. googleHeaders, new KeyValuePair<string, string>("Authorization", authorization)];
    }

    /// <summary>Builds the credential scope <c>date/region/storage/goog4_request</c>.</summary>
    /// <param name="date">The request's date as <c>yyyyMMdd</c>.</param>
    /// <param name="region">The region of the key.</param>
    /// <returns>The credential scope.</returns>
    internal static string CredentialScope(string date, string region) => $"{date}/{region}/{Service}/{TerminationString}";

    /// <summary>Builds the string to sign: the algorithm, the timestamp, the credential scope and the hex SHA-256 of the canonical request, each on its own line.</summary>
    /// <param name="timestamp">The request's timestamp as <c>yyyyMMdd'T'HHmmss'Z'</c>.</param>
    /// <param name="scope">The credential scope.</param>
    /// <param name="canonicalRequest">The canonical request.</param>
    /// <returns>The string to sign.</returns>
    internal static string StringToSign(string timestamp, string scope, string canonicalRequest) =>
        $"{Algorithm}\n{timestamp}\n{scope}\n{Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

    private static byte[] SigningKey(GcsHmacKey key, string date)
    {
        var dateKey = HmacSha256(Encoding.UTF8.GetBytes("GOOG4" + key.Secret), date);
        var dateRegionKey = HmacSha256(dateKey, key.Region);
        var dateRegionServiceKey = HmacSha256(dateRegionKey, Service);
        return HmacSha256(dateRegionServiceKey, TerminationString);
    }

    private static byte[] HmacSha256(byte[] key, string text) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(text));

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
