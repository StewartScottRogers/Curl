namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <see cref="IOException" /> an <see cref="IMultiplexedStream" /> throws when its QUIC
/// connection is lost, carrying the curl exit code and the message curl prints for it
/// (ADR-0144, section 7).
/// </summary>
/// <remarks>
/// <c>Curl.Quic</c> throws it, knowing why the connection was lost; the HTTP handler
/// reports <see cref="ExitCode" /> and the message as the transfer's failure.
/// </remarks>
public sealed class MultiplexedConnectionFailedException : IOException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultiplexedConnectionFailedException" /> class.
    /// </summary>
    /// <param name="exitCode">The curl exit code the loss maps to.</param>
    /// <param name="message">The message curl prints for the loss.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exitCode" /> is <see cref="CurlExitCode.Ok" />, which is not a failure.
    /// </exception>
    public MultiplexedConnectionFailedException(CurlExitCode exitCode, string message)
        : base(message)
    {
        if (exitCode == CurlExitCode.Ok)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exitCode),
                exitCode,
                "A lost connection cannot report CurlExitCode.Ok.");
        }

        ExitCode = exitCode;
    }

    /// <summary>
    /// Gets the curl exit code the loss maps to, never <see cref="CurlExitCode.Ok" />.
    /// </summary>
    public CurlExitCode ExitCode { get; }
}
