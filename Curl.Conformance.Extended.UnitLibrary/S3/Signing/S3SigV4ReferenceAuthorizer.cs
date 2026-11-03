using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Conformance.Extended.S3.Signing;

/// <summary>
/// Serves only a request signed with the one access key it was given, as the Amazon S3 API reference "Authenticating Requests
/// (AWS Signature Version 4)" says for the <c>Authorization</c> header and a payload in a single chunk: <c>AWS4-HMAC-SHA256</c>,
/// the credential scope <c>date/region/s3/aws4_request</c>, an <c>x-amz-date</c> within 15 minutes of the clock, <c>host</c> and
/// every <c>x-amz-*</c> header signed, an <c>x-amz-content-sha256</c> that is the body's hash (or <c>UNSIGNED-PAYLOAD</c>), the
/// expected <c>x-amz-security-token</c> when one is given, and the signature recomputed with BCL <see cref="HMACSHA256" /> from the
/// documentation alone. Any other request gets <c>403 SignatureDoesNotMatch</c>.
/// </summary>
/// <param name="accessKeyId">The access key ID.</param>
/// <param name="secretAccessKey">The secret access key.</param>
/// <param name="region">The region the credential scope must name.</param>
/// <param name="clock">The server's clock.</param>
public sealed class S3SigV4ReferenceAuthorizer(string accessKeyId, string secretAccessKey, string region, TimeProvider clock) : IS3RequestAuthorizer
{
    /// <summary>The signing algorithm.</summary>
    public const string Algorithm = "AWS4-HMAC-SHA256";

    /// <summary>The hashed payload of a request whose payload is not signed.</summary>
    public const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    /// <summary>The refusal's message, as S3 words it.</summary>
    public const string RefusalMessage = "The request signature we calculated does not match the signature you provided. Check your key and signing method.";

    private const string TimestampFormat = "yyyyMMdd'T'HHmmss'Z'";

    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(15);

    /// <summary>Gets the session token the request must carry as <c>x-amz-security-token</c>; <see langword="null" /> for none.</summary>
    public string? SessionToken { get; init; }

    /// <inheritdoc />
    public S3ReferenceError? Authorize(S3ReferenceRequest request) =>
        FaultOf(request) is null ? null : new S3ReferenceError(403, "SignatureDoesNotMatch", RefusalMessage);

    /// <summary>Says why <paramref name="request" /> is not signed as Signature Version 4 requires.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The fault, or <see langword="null" /> when the signature verifies.</returns>
    public string? FaultOf(S3ReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (AuthorizationOf(request.Headers.GetValueOrDefault("Authorization")) is not { } authorization)
        {
            return "The request has no AWS4-HMAC-SHA256 Authorization header with Credential, SignedHeaders and Signature.";
        }

        if (!DateTimeOffset.TryParseExact(request.Headers.GetValueOrDefault("x-amz-date"), TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset signedAt))
        {
            return "The request has no x-amz-date in the form yyyyMMddTHHmmssZ.";
        }

        Func<string?>[] checks =
        [
            () => (clock.GetUtcNow() - signedAt).Duration() > AllowedClockSkew ? "x-amz-date is more than 15 minutes from the server's time." : null,
            () => authorization.Credential == $"{accessKeyId}/{CredentialScope(signedAt)}" ? null : $"The credential is not {accessKeyId}/{CredentialScope(signedAt)}.",
            () => request.Headers.GetValueOrDefault("x-amz-security-token") == SessionToken ? null : "x-amz-security-token is not the session token.",
            () => FaultOfSignedHeaders(request, authorization.SignedHeaders),
            () => FaultOfPayload(request),
            () => FaultOfSignature(request, authorization),
        ];
        return checks.Select(check => check()).FirstOrDefault(fault => fault is not null);
    }

    /// <summary>Computes the signature of <paramref name="request" /> over <paramref name="signedHeaders" />, dated by its <c>x-amz-date</c>.</summary>
    /// <param name="request">The request, with its <c>x-amz-date</c> and <c>x-amz-content-sha256</c>.</param>
    /// <param name="signedHeaders">The signed header names, sorted and joined by <c>;</c>.</param>
    /// <returns>The lower-case hex signature.</returns>
    public string SignatureOf(S3ReferenceRequest request, string signedHeaders)
    {
        ArgumentNullException.ThrowIfNull(request);
        string timestamp = request.Headers["x-amz-date"];
        DateTimeOffset signedAt = DateTimeOffset.ParseExact(timestamp, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        string canonicalRequest = S3SigV4CanonicalRequest.Of(request, signedHeaders, request.Headers["x-amz-content-sha256"]);
        string stringToSign = $"{Algorithm}\n{timestamp}\n{CredentialScope(signedAt)}\n{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
        byte[] key = Encoding.UTF8.GetBytes("AWS4" + secretAccessKey);
        foreach (string step in new[] { signedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture), region, "s3", "aws4_request" })
        {
            key = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(step));
        }

        return Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(stringToSign)));
    }

    private string CredentialScope(DateTimeOffset signedAt) => $"{signedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}/{region}/s3/aws4_request";

    private static Authorization? AuthorizationOf(string? header)
    {
        Dictionary<string, string> fields = FieldsOf(header ?? string.Empty);
        string?[] values = [.. new[] { "Credential", "SignedHeaders", "Signature" }.Select(fields.GetValueOrDefault)];
        return values.Contains(null) ? null : new Authorization(values[0]!, values[1]!, values[2]!);
    }

    private static Dictionary<string, string> FieldsOf(string header) =>
        header.StartsWith(Algorithm + " ", StringComparison.Ordinal)
            ? header[(Algorithm.Length + 1)..].Split(',', StringSplitOptions.TrimEntries)
                .Select(field => field.Split('=', 2))
                .Where(nameAndValue => nameAndValue.Length == 2)
                .ToDictionary(nameAndValue => nameAndValue[0], nameAndValue => nameAndValue[1], StringComparer.Ordinal)
            : [];

    private static string? FaultOfSignedHeaders(S3ReferenceRequest request, string signedHeaders)
    {
        string[] names = signedHeaders.Split(';');
        if (signedHeaders != string.Join(';', names.Distinct().Order(StringComparer.Ordinal)) || signedHeaders != signedHeaders.ToLowerInvariant())
        {
            return $"SignedHeaders {signedHeaders} is not lower case, sorted and without repeats.";
        }

        string[] mustSign = ["host", .. request.Headers.Keys.Select(name => name.ToLowerInvariant()).Where(name => name.StartsWith("x-amz-", StringComparison.Ordinal))];
        string[] unsigned = [.. mustSign.Except(names), .. names.Where(name => !request.Headers.ContainsKey(name))];
        return unsigned.Length == 0 ? null : $"SignedHeaders {signedHeaders} leaves out a sent host or x-amz-* header, or names one not sent: {string.Join(", ", unsigned)}.";
    }

    private static string? FaultOfPayload(S3ReferenceRequest request)
    {
        string payload = request.Headers["x-amz-content-sha256"];
        return payload == UnsignedPayload || payload == Convert.ToHexStringLower(SHA256.HashData(request.Body))
            ? null
            : $"x-amz-content-sha256 {payload} is not the body's SHA-256.";
    }

    private string? FaultOfSignature(S3ReferenceRequest request, Authorization authorization) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(SignatureOf(request, authorization.SignedHeaders)), Encoding.ASCII.GetBytes(authorization.Signature))
            ? null
            : "The signature does not match the one computed from the canonical request.";

    private sealed record Authorization(string Credential, string SignedHeaders, string Signature);
}
