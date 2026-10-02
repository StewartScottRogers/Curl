namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <see cref="IOException" /> a connection whose TLS handshake was deferred to its first
/// write throws from that write when the handshake fails, carrying the curl exit code and the
/// message curl prints for the failure, as the connect would have (<c>--tls-earlydata</c>, BL-1105).
/// </summary>
/// <remarks>
/// <c>Curl.Networking</c> throws it, knowing why the handshake failed; a protocol handler
/// reports <see cref="ExitCode" /> and the message as the transfer's failure instead of a
/// send failure.
/// </remarks>
public sealed class DeferredTlsHandshakeFailedException : IOException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeferredTlsHandshakeFailedException" /> class.
    /// </summary>
    /// <param name="exitCode">The curl exit code the failed handshake maps to.</param>
    /// <param name="message">The message curl prints for the failed handshake.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exitCode" /> is <see cref="CurlExitCode.Ok" />, which is not a failure.
    /// </exception>
    public DeferredTlsHandshakeFailedException(CurlExitCode exitCode, string message)
        : base(message)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(exitCode, CurlExitCode.Ok);
        ExitCode = exitCode;
    }

    /// <summary>Gets the curl exit code the failed handshake maps to.</summary>
    public CurlExitCode ExitCode { get; }
}
