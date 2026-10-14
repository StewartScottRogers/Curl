namespace Curl.HttpMessageSignatures;

/// <summary>
/// A request curl 8.22.0 refuses to sign. Every such refusal is curl's
/// <c>CURLE_BAD_FUNCTION_ARGUMENT</c>, exit code 43, and <see cref="Exception.Message" /> is the
/// text curl prints after <c>curl: (43) </c>.
/// </summary>
public sealed class HttpSignatureException : Exception
{
    /// <summary>
    /// The text curl prints for <c>CURLE_BAD_FUNCTION_ARGUMENT</c> when the signing code fails
    /// without a message of its own (a control character in a quoted value, or a quote or
    /// backslash in a component).
    /// </summary>
    public const string BadArgumentMessage = "A libcurl function was given a bad argument";

    /// <summary>The exit code curl ends with when it refuses to sign.</summary>
    public const int ExitCode = 43;

    /// <summary>Creates the exception with curl's message.</summary>
    /// <param name="message">The text curl prints, such as <c>httpsig: hex key too long</c>.</param>
    public HttpSignatureException(string message)
        : base(message)
    {
    }
}
