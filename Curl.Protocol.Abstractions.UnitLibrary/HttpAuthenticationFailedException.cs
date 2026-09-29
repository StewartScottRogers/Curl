namespace Curl.Protocol.Abstractions;

/// <summary>
/// Thrown by an <see cref="IHttpAuthenticator" /> when a challenge cannot be answered and curl
/// fails the transfer rather than taking the response as it is, as curl 8.21.0's SSPI build
/// fails an NTLM Type 2 message SSPI refuses with exit 94 (ADR-0181).
/// </summary>
/// <remarks>
/// The HTTP handler ends the transfer with <see cref="ExitCode" /> and
/// <see cref="Exception.Message" />, writing none of the response's body.
/// </remarks>
public sealed class HttpAuthenticationFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpAuthenticationFailedException" /> class.
    /// </summary>
    /// <param name="exitCode">The exit code curl ends the transfer with.</param>
    /// <param name="message">The message curl prints for it.</param>
    public HttpAuthenticationFailedException(CurlExitCode exitCode, string message)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>Gets the exit code curl ends the transfer with.</summary>
    public CurlExitCode ExitCode { get; }
}
