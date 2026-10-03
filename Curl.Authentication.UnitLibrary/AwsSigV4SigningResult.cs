using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// What <see cref="AwsSigV4Signer" /> produced: the headers to add, nothing (a custom
/// <c>Authorization</c> header wins), or the error curl ends the transfer with.
/// </summary>
public sealed class AwsSigV4SigningResult
{
    private AwsSigV4SigningResult(
        CurlExitCode exitCode,
        string? errorMessage,
        IReadOnlyList<string> headerLines,
        string? canonicalRequest,
        string? stringToSign,
        IReadOnlyList<string> pickedFromHostLines)
    {
        ExitCode = exitCode;
        ErrorMessage = errorMessage;
        HeaderLines = headerLines;
        CanonicalRequest = canonicalRequest;
        StringToSign = stringToSign;
        PickedFromHostLines = pickedFromHostLines;
    }

    /// <summary>Gets the result when a custom <c>Authorization</c> header is present, so curl signs nothing.</summary>
    public static AwsSigV4SigningResult NotSigned { get; } = new(CurlExitCode.Ok, errorMessage: null, [], canonicalRequest: null, stringToSign: null, []);

    /// <summary>Gets <see cref="CurlExitCode.Ok" />, or the exit code curl fails the transfer with.</summary>
    public CurlExitCode ExitCode { get; }

    /// <summary>
    /// Gets the message curl prints after <c>curl: (N) </c> on failure; <see langword="null" />
    /// on success.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the header lines to send, without line endings, in curl's order: <c>Authorization</c>,
    /// then the date header unless a custom one was given, then the content hash header when
    /// signing for S3. Empty when nothing is signed or on failure.
    /// </summary>
    public IReadOnlyList<string> HeaderLines { get; }

    /// <summary>Gets the canonical request that was hashed; <see langword="null" /> when nothing was signed.</summary>
    public string? CanonicalRequest { get; }

    /// <summary>
    /// Gets the string to sign, which curl's <c>-v</c> shows as
    /// <c>aws_sigv4: String to sign (enclosed in []) - [...]</c>; <see langword="null" /> when
    /// nothing was signed.
    /// </summary>
    public string? StringToSign { get; }

    /// <summary>
    /// Gets the lines curl's <c>-v</c> writes before the string to sign when <c>--aws-sigv4</c> left
    /// out the service or region and they were taken from the host name, in curl's order:
    /// <c>aws_sigv4: picked service &lt;service&gt; from host</c>, then
    /// <c>aws_sigv4: picked region &lt;region&gt; from host</c>. Empty when the value named both,
    /// when nothing is signed or on failure.
    /// </summary>
    public IReadOnlyList<string> PickedFromHostLines { get; }

    /// <summary>Creates a failure.</summary>
    /// <param name="exitCode">The exit code curl ends with.</param>
    /// <param name="errorMessage">The message curl prints.</param>
    /// <returns>The failed result.</returns>
    internal static AwsSigV4SigningResult Failed(CurlExitCode exitCode, string errorMessage) =>
        new(exitCode, errorMessage, [], canonicalRequest: null, stringToSign: null, []);

    /// <summary>Creates a signed result.</summary>
    /// <param name="headerLines">The header lines to send.</param>
    /// <param name="canonicalRequest">The canonical request.</param>
    /// <param name="stringToSign">The string to sign.</param>
    /// <param name="pickedFromHostLines">The <c>-v</c> lines naming what was taken from the host name.</param>
    /// <returns>The signed result.</returns>
    internal static AwsSigV4SigningResult Signed(IReadOnlyList<string> headerLines, string canonicalRequest, string stringToSign, IReadOnlyList<string> pickedFromHostLines) =>
        new(CurlExitCode.Ok, errorMessage: null, headerLines, canonicalRequest, stringToSign, pickedFromHostLines);
}
