using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Ends an RTSP transfer with the exit code and message curl 8.21.0 gives the failure; the
/// handler turns it into a failed <see cref="TransferResult" />.
/// </summary>
/// <param name="exitCode">The exit code the transfer fails with.</param>
/// <param name="message">curl's message, without the <c>curl: (N) </c> prefix.</param>
internal sealed class RtspTransferException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets the exit code the transfer fails with.</summary>
    internal CurlExitCode ExitCode { get; } = exitCode;
}
