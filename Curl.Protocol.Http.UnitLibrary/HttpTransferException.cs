using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Ends an HTTP transfer early with the curl exit code and message the transfer reports.
/// Thrown while a response is read and caught by the handler that turns it into a
/// <see cref="TransferResult" />; it never leaves this library.
/// </summary>
/// <param name="exitCode">The curl exit code the transfer reports.</param>
/// <param name="message">The message curl prints for the failure.</param>
internal sealed class HttpTransferException(CurlExitCode exitCode, string message)
    : Exception(message)
{
    /// <summary>
    /// Gets the curl exit code the transfer reports.
    /// </summary>
    internal CurlExitCode ExitCode { get; } = exitCode;
}
