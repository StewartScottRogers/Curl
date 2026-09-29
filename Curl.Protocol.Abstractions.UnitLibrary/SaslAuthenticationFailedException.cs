namespace Curl.Protocol.Abstractions;

/// <summary>
/// Thrown by an <see cref="ISaslExchange" /> when a challenge cannot be answered and curl
/// fails the transfer at once rather than cancelling the exchange, as curl 8.21.0's Schannel
/// build fails a DIGEST-MD5 challenge SSPI rejects with exit 94 (ADR-0139, BL-781).
/// </summary>
/// <remarks>
/// The SMTP, IMAP and POP3 handlers end the transfer with <see cref="ExitCode" /> and
/// <see cref="Exception.Message" />, sending nothing more: no answer, no cancel line (<c>*</c>)
/// and no <c>QUIT</c> or <c>LOGOUT</c>, as measured.
/// </remarks>
public sealed class SaslAuthenticationFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SaslAuthenticationFailedException" /> class.
    /// </summary>
    /// <param name="exitCode">The exit code curl ends the transfer with.</param>
    /// <param name="message">The message curl prints for it.</param>
    public SaslAuthenticationFailedException(CurlExitCode exitCode, string message)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>Gets the exit code curl ends the transfer with.</summary>
    public CurlExitCode ExitCode { get; }
}
