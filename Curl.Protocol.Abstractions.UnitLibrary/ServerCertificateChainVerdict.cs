namespace Curl.Protocol.Abstractions;

/// <summary>
/// What <see cref="ITlsProvider.VerifyServerCertificateChain" /> decided about a server
/// certificate chain: accepted, or rejected with the curl exit code and message curl's own TLS
/// would give for it (ADR-1052).
/// </summary>
public sealed class ServerCertificateChainVerdict
{
    private ServerCertificateChainVerdict(CurlExitCode exitCode, string? errorMessage)
    {
        ExitCode = exitCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>Gets the verdict that accepts the chain.</summary>
    public static ServerCertificateChainVerdict Accepted { get; } = new(CurlExitCode.Ok, null);

    /// <summary>Gets a value indicating whether the chain is accepted.</summary>
    public bool IsAccepted => ExitCode == CurlExitCode.Ok;

    /// <summary>
    /// Gets the curl exit code for a rejected chain (<see cref="CurlExitCode.PeerFailedVerification" />,
    /// 60, for one the trust settings do not accept), or <see cref="CurlExitCode.Ok" /> when it is accepted.
    /// </summary>
    public CurlExitCode ExitCode { get; }

    /// <summary>Gets curl's message for a rejected chain, or <see langword="null" /> when it is accepted.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Returns a verdict that rejects the chain.</summary>
    /// <param name="exitCode">The curl exit code; never <see cref="CurlExitCode.Ok" />.</param>
    /// <param name="errorMessage">curl's message for the rejection.</param>
    /// <returns>The rejection.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exitCode" /> is <see cref="CurlExitCode.Ok" />.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="errorMessage" /> is <see langword="null" />.</exception>
    public static ServerCertificateChainVerdict Rejected(CurlExitCode exitCode, string errorMessage)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(exitCode, CurlExitCode.Ok);
        ArgumentNullException.ThrowIfNull(errorMessage);
        return new ServerCertificateChainVerdict(exitCode, errorMessage);
    }
}
