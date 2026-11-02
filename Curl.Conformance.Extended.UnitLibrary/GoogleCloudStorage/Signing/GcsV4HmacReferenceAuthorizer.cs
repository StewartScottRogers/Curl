using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Conformance.Extended.GoogleCloudStorage.Signing;

/// <summary>
/// Serves only a request signed with the one HMAC key it was given, as Cloud Storage's "V4 signing process" says for the
/// <c>Authorization</c> header form (cloud.google.com/storage/docs/authentication/signatures): <c>GOOG4-HMAC-SHA256</c>, the credential
/// scope <c>date/region/storage/goog4_request</c>, an <c>x-goog-date</c> within 15 minutes of the clock, <c>host</c> and every
/// <c>x-goog-*</c> header signed, an <c>x-goog-content-sha256</c> that is the body's hash, and the signature recomputed with BCL
/// <see cref="HMACSHA256" /> from the documentation alone. Any other request gets <c>403 SignatureDoesNotMatch</c>.
/// </summary>
/// <param name="accessId">The HMAC key's access ID.</param>
/// <param name="secret">The HMAC key's secret.</param>
/// <param name="region">The region the credential scope must name, <c>auto</c> for any bucket.</param>
/// <param name="clock">The server's clock.</param>
public sealed class GcsV4HmacReferenceAuthorizer(string accessId, string secret, string region, TimeProvider clock) : IGcsReferenceAuthorizer
{
    /// <summary>The signing algorithm.</summary>
    public const string Algorithm = "GOOG4-HMAC-SHA256";

    /// <summary>The payload line of a request whose payload is not signed.</summary>
    public const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    /// <summary>The refusal's message, as Cloud Storage words it.</summary>
    public const string RefusalMessage = "The request signature we calculated does not match the signature you provided. Check your Google secret key and signing method.";

    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public GcsReferenceResponse? Refuse(GcsReferenceRequest request) =>
        FaultOf(request) is { } fault ? GcsReferenceResponse.Error(403, "Forbidden", "SignatureDoesNotMatch", RefusalMessage, fault) : null;

    /// <summary>Says why <paramref name="request" /> is not signed as the V4 signing process requires.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The fault, or <see langword="null" /> when the signature verifies.</returns>
    public string? FaultOf(GcsReferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (AuthorizationOf(request.Headers.GetValueOrDefault("Authorization")) is not { } authorization)
        {
            return "The request has no GOOG4-HMAC-SHA256 Authorization header with Credential, SignedHeaders and Signature.";
        }

        if (!DateTimeOffset.TryParseExact(request.Headers.GetValueOrDefault("x-goog-date"), "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset signedAt))
        {
            return "The request has no x-goog-date in the form yyyyMMddTHHmmssZ.";
        }

        Func<string?>[] checks =
        [
            () => FaultOfClock(signedAt),
            () => FaultOfCredential(authorization, signedAt),
            () => FaultOfSignedHeaders(request, authorization.SignedHeaders),
            () => FaultOfPayload(request),
            () => FaultOfSignature(request, authorization),
        ];
        return checks.Select(check => check()).FirstOrDefault(fault => fault is not null);
    }

    private string? FaultOfClock(DateTimeOffset signedAt) =>
        (clock.GetUtcNow() - signedAt).Duration() > AllowedClockSkew ? "x-goog-date is more than 15 minutes from the server's time." : null;

    private string? FaultOfCredential(Authorization authorization, DateTimeOffset signedAt) =>
        authorization.Credential == $"{accessId}/{CredentialScope(signedAt)}" ? null : $"The credential is not {accessId}/{CredentialScope(signedAt)}.";

    /// <summary>Computes the signature of <paramref name="request" /> over <paramref name="signedHeaders" />, dated by its <c>x-goog-date</c>.</summary>
    /// <param name="request">The request, with its <c>x-goog-date</c>.</param>
    /// <param name="signedHeaders">The signed header names, sorted and joined by <c>;</c>.</param>
    /// <returns>The lower-case hex signature.</returns>
    public string SignatureOf(GcsReferenceRequest request, string signedHeaders)
    {
        ArgumentNullException.ThrowIfNull(request);
        string timestamp = request.Headers["x-goog-date"];
        DateTimeOffset signedAt = DateTimeOffset.ParseExact(timestamp, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        string canonicalRequest = GcsV4CanonicalRequest.Of(request, signedHeaders, PayloadOf(request));
        string stringToSign = $"{Algorithm}\n{timestamp}\n{CredentialScope(signedAt)}\n{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
        byte[] key = Encoding.UTF8.GetBytes("GOOG4" + secret);
        foreach (string step in new[] { signedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture), region, "storage", "goog4_request" })
        {
            key = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(step));
        }

        return Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(stringToSign)));
    }

    private string CredentialScope(DateTimeOffset signedAt) => $"{signedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}/{region}/storage/goog4_request";

    private static string PayloadOf(GcsReferenceRequest request) => request.Headers.GetValueOrDefault("x-goog-content-sha256") ?? UnsignedPayload;

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

    private static bool IsCanonicalNameList(string signedHeaders, string[] names) =>
        signedHeaders == string.Join(';', names.Distinct().Order(StringComparer.Ordinal)) && signedHeaders == signedHeaders.ToLowerInvariant();

    private static string? FaultOfSignedHeaders(GcsReferenceRequest request, string signedHeaders)
    {
        string[] names = signedHeaders.Split(';');
        if (!IsCanonicalNameList(signedHeaders, names))
        {
            return $"SignedHeaders {signedHeaders} is not lower case, sorted and without repeats.";
        }

        string[] mustSign = ["host", .. request.Headers.Keys.Select(name => name.ToLowerInvariant()).Where(name => name.StartsWith("x-goog-", StringComparison.Ordinal))];
        string[] unsigned = [.. mustSign.Except(names), .. names.Where(name => !request.Headers.ContainsKey(name))];
        return unsigned.Length == 0 ? null : $"SignedHeaders {signedHeaders} leaves out a sent host or x-goog-* header, or names one not sent: {string.Join(", ", unsigned)}.";
    }

    private static string? FaultOfPayload(GcsReferenceRequest request)
    {
        string payload = PayloadOf(request);
        return payload == UnsignedPayload || payload == Convert.ToHexStringLower(SHA256.HashData(request.Body))
            ? null
            : $"x-goog-content-sha256 {payload} is not the body's SHA-256.";
    }

    private string? FaultOfSignature(GcsReferenceRequest request, Authorization authorization) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(SignatureOf(request, authorization.SignedHeaders)), Encoding.ASCII.GetBytes(authorization.Signature))
            ? null
            : "The signature does not match the one computed from the canonical request.";

    private sealed record Authorization(string Credential, string SignedHeaders, string Signature);
}
