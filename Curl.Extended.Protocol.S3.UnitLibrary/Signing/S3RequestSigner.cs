using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Extended.Protocol.S3.Signing;

/// <summary>
/// Signs an S3 request with AWS Signature Version 4 as the Amazon S3 API reference "Authenticating
/// Requests (AWS Signature Version 4)" says, for a payload sent in a single chunk: it returns the
/// <c>x-amz-date</c>, <c>x-amz-content-sha256</c> and <c>Authorization</c> headers to add.
/// </summary>
/// <param name="timeProvider">The clock whose current UTC time is the request's timestamp.</param>
public sealed class S3RequestSigner(TimeProvider timeProvider)
{
    /// <summary>The payload hash of a request whose payload is not signed.</summary>
    public const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Service = "s3";
    private const string TerminationString = "aws4_request";

    /// <summary>Signs <paramref name="request"/> with <paramref name="credentials"/> at the clock's current time.</summary>
    /// <param name="request">The request to sign; its headers must include <c>host</c> and must not include the headers this adds.</param>
    /// <param name="credentials">The access key and region to sign with.</param>
    /// <returns>The <c>x-amz-date</c>, <c>x-amz-content-sha256</c> and <c>Authorization</c> headers, in that order.</returns>
    public IReadOnlyList<KeyValuePair<string, string>> Sign(S3SigningRequest request, S3Credentials credentials)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credentials);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var timestamp = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var amazonHeaders = new[]
        {
            new KeyValuePair<string, string>("x-amz-date", timestamp),
            new KeyValuePair<string, string>("x-amz-content-sha256", request.PayloadHash),
        };
        var (canonicalRequest, signedHeaders) = S3CanonicalRequest.Build(request with { Headers = [.. request.Headers, .. amazonHeaders] });
        var scope = $"{date}/{credentials.Region}/{Service}/{TerminationString}";
        var stringToSign = $"{Algorithm}\n{timestamp}\n{scope}\n{Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
        var signature = Hex(HmacSha256(SigningKey(credentials, date), stringToSign));
        var authorization = $"{Algorithm} Credential={credentials.AccessKeyId}/{scope},SignedHeaders={signedHeaders},Signature={signature}";
        return [.. amazonHeaders, new KeyValuePair<string, string>("Authorization", authorization)];
    }

    private static byte[] SigningKey(S3Credentials credentials, string date)
    {
        var dateKey = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + credentials.SecretAccessKey), date);
        var dateRegionKey = HmacSha256(dateKey, credentials.Region);
        var dateRegionServiceKey = HmacSha256(dateRegionKey, Service);
        return HmacSha256(dateRegionServiceKey, TerminationString);
    }

    private static byte[] HmacSha256(byte[] key, string text) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(text));

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
