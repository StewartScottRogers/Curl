using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Signs an HTTP request with AWS Signature Version 4 as curl 8.21.0's <c>--aws-sigv4</c> does
/// (<c>lib/http_aws_sigv4.c</c>): the canonical request, the string to sign, the signing key,
/// and the <c>Authorization</c>, <c>X-&lt;Provider&gt;-Date</c> and, for S3,
/// <c>x-&lt;provider&gt;-content-sha256</c> headers (ADR-0178).
/// </summary>
/// <param name="timeProvider">Gives the signing time, unless a custom date header gives it.</param>
/// <param name="argumentEncoding">
/// Turns the credentials, URL and headers into the bytes curl hashes and escapes: the encoding
/// its arguments reach it in (<see cref="CredentialEncoding.ForPlatform" />).
/// </param>
/// <param name="diagnosticLog">Where the scope a request is signed in is logged (BL-1151); <see langword="null" /> logs nothing.</param>
public sealed class AwsSigV4Signer(TimeProvider timeProvider, Encoding argumentEncoding, IDiagnosticLog? diagnosticLog = null)
{
    private readonly AuthDiagnosticLog log = new(diagnosticLog);

    private const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    /// <summary>Signs <paramref name="request" />.</summary>
    /// <param name="request">The request to sign.</param>
    /// <returns>
    /// The headers to send; <see cref="AwsSigV4SigningResult.NotSigned" /> when a custom
    /// <c>Authorization</c> header is present; or curl's error: exit 43 for <c>--path-as-is</c>
    /// or an empty first provider, 3 when neither the value nor the host names the service or
    /// region, 100 for a query of 128 components or more, 27 for a custom date header written
    /// <c>Name;</c>.
    /// </returns>
    public AwsSigV4SigningResult Sign(AwsSigV4Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PathAsIs)
        {
            return AwsSigV4SigningResult.Failed(CurlExitCode.BadFunctionArgument, "Cannot use sigv4 authentication with path-as-is flag");
        }

        if (AwsSigV4Headers.FindCustomHeader(request.CustomHeaders, "Authorization") is not null)
        {
            return AwsSigV4SigningResult.NotSigned;
        }

        AwsSigV4Scope? scope = AwsSigV4Scope.Parse(request.SigV4Parameter, request.HostName, out AwsSigV4SigningResult? failure);
        return scope is null ? failure! : SignInScope(request, scope);
    }

    private static bool SignsAsS3(AwsSigV4Scope scope) =>
        scope.Provider0.Equals("aws", StringComparison.OrdinalIgnoreCase) && scope.Service.Equals("s3", StringComparison.OrdinalIgnoreCase);

    private static string HashHex(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private AwsSigV4SigningResult SignInScope(AwsSigV4Request request, AwsSigV4Scope scope)
    {
        log.AwsSigV4ScopeChosen(scope.Provider0 + ":" + scope.Provider1, scope.Region, scope.Service);
        (string payloadHash, string? contentHashLine) = PayloadHash(request, scope);
        List<string> lines = CollectLines(request, contentHashLine);
        string provider1 = AsciiCase.ToLower(scope.Provider1);
        string dateHeaderName = "X-" + AsciiCase.ToUpper(provider1[..1]) + provider1[1..] + "-Date";
        string? customDate = AwsSigV4Headers.FindCustomHeader(request.CustomHeaders, dateHeaderName)
            ?? AwsSigV4Headers.FindCustomHeader(request.CustomHeaders, "Date");
        string? timestamp = customDate is null ? Now() : TimestampFrom(customDate);
        if (timestamp is null)
        {
            return AwsSigV4SigningResult.Failed(CurlExitCode.OutOfMemory, "Out of memory");
        }

        string? dateHeaderLine = null;
        if (customDate is null)
        {
            lines.Add("x-" + provider1 + "-date:" + timestamp);
            dateHeaderLine = dateHeaderName + ": " + timestamp;
        }

        string? query = AwsSigV4UriEncoding.CanonicalQuery(request.Query, argumentEncoding);
        return query is null
            ? AwsSigV4SigningResult.Failed(CurlExitCode.TooLarge, "HTTP request too large")
            : Finish(request, scope, new SigningInputs(payloadHash, lines, timestamp, query, dateHeaderLine, contentHashLine));
    }

    private AwsSigV4SigningResult Finish(AwsSigV4Request request, AwsSigV4Scope scope, SigningInputs inputs)
    {
        List<string> headers = AwsSigV4Headers.SortAndMerge(inputs.Lines);
        string signedHeaders = string.Join(';', headers.Select(AwsSigV4Headers.NameOf));
        string canonicalRequest = string.Join(
            '\n',
            request.Method,
            AwsSigV4UriEncoding.CanonicalPath(request.Path, scope.Service, argumentEncoding),
            inputs.Query,
            string.Concat(headers.Select(line => line + "\n")),
            signedHeaders,
            inputs.PayloadHash);

        string date = inputs.Timestamp[..Math.Min(8, inputs.Timestamp.Length)];
        string requestType = AsciiCase.ToLower(scope.Provider0) + "4_request";
        string credentialScope = string.Join('/', date, scope.Region, scope.Service, requestType);
        string algorithm = AsciiCase.ToUpper(scope.Provider0) + "4-HMAC-SHA256";
        string stringToSign = string.Join('\n', algorithm, inputs.Timestamp, credentialScope, HashHex(argumentEncoding.GetBytes(canonicalRequest)));

        byte[] key = argumentEncoding.GetBytes(AsciiCase.ToUpper(scope.Provider0) + "4" + request.Password);
        foreach (string part in new[] { date, scope.Region, scope.Service, requestType, stringToSign })
        {
            key = HMACSHA256.HashData(key, argumentEncoding.GetBytes(part));
        }

        string authorization = "Authorization: " + algorithm
            + " Credential=" + AwsSigV4UriEncoding.EscapeText(request.UserName, argumentEncoding) + "/" + credentialScope
            + ", SignedHeaders=" + signedHeaders
            + ", Signature=" + Convert.ToHexStringLower(key);
        string[] headerLines = new[] { authorization, inputs.DateHeaderLine, inputs.ContentHashLine }.OfType<string>().ToArray();
        return AwsSigV4SigningResult.Signed(headerLines, canonicalRequest, stringToSign);
    }

    /// <summary>
    /// Gets the payload hash: a custom <c>x-&lt;provider&gt;-content-sha256</c> header's value;
    /// for S3, the body's hash when it is known (GET, HEAD, an empty upload, in-memory POST
    /// fields) and <c>UNSIGNED-PAYLOAD</c> otherwise, with the header that carries it;
    /// otherwise the hash of the POST fields, or of nothing.
    /// </summary>
    private (string PayloadHash, string? ContentHashLine) PayloadHash(AwsSigV4Request request, AwsSigV4Scope scope)
    {
        string contentHashName = "x-" + scope.Provider1 + "-content-sha256";
        string? custom = CustomHeaderValue(request.CustomHeaders, contentHashName);
        if (custom is not null)
        {
            return (custom, null);
        }

        string bodyHash = HashHex(request.PostFields ?? []);
        if (!SignsAsS3(scope))
        {
            return (bodyHash, null);
        }

        string hash = S3PayloadIsKnown(request) ? bodyHash : UnsignedPayload;
        return (hash, contentHashName + ": " + hash);
    }

    /// <summary>
    /// Gets a custom header's value with its blanks trimmed; <see langword="null" /> when there
    /// is no such header or it is written <c>Name;</c>.
    /// </summary>
    private static string? CustomHeaderValue(IReadOnlyList<string> headers, string name)
    {
        string? header = AwsSigV4Headers.FindCustomHeader(headers, name);
        int colon = header?.IndexOf(':', StringComparison.Ordinal) ?? -1;
        return colon < 0 ? null : header![(colon + 1)..].Trim(' ', '\t');
    }

    /// <summary>
    /// Gets a value indicating whether curl knows an S3 request's body: a GET or HEAD, an empty
    /// upload, or in-memory POST fields.
    /// </summary>
    private static bool S3PayloadIsKnown(AwsSigV4Request request) =>
        request.IsGetOrHead || request.UploadFileSize == 0 || request.PostFields is not null;

    private List<string> CollectLines(AwsSigV4Request request, string? contentHashLine)
    {
        List<string> lines = [];
        if (AwsSigV4Headers.FindCustomHeader(request.CustomHeaders, "Host") is null)
        {
            lines.Add("Host: " + request.HostHeaderValue);
        }

        if (contentHashLine is not null)
        {
            lines.Add(contentHashLine);
        }

        lines.AddRange(request.CustomHeaders.Select(AwsSigV4Headers.ToSignedLine).OfType<string>());
        return lines.Select(AwsSigV4Headers.Trim).ToList();
    }

    private string Now() => timeProvider.GetUtcNow().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads the timestamp from a custom date header: its value's leading run of ASCII letters and
    /// digits when that is 16 long (<c>20200102T030405Z</c>), otherwise empty; <see langword="null" />
    /// when the header has no <c>:</c>, which curl fails as out of memory.
    /// </summary>
    private static string? TimestampFrom(string header)
    {
        int colon = header.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return null;
        }

        int start = AwsSigV4Headers.SkipBlanks(header, colon + 1);
        int end = start;
        while (end < header.Length && char.IsAsciiLetterOrDigit(header[end]))
        {
            end++;
        }

        return end - start == 16 ? header[start..end] : string.Empty;
    }

    private sealed record SigningInputs(
        string PayloadHash,
        List<string> Lines,
        string Timestamp,
        string Query,
        string? DateHeaderLine,
        string? ContentHashLine);
}
