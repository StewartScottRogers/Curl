using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Ends an SSH transfer early with the curl exit code and message the transfer reports.
/// Thrown inside the session and turned into a <see cref="TransferResult" /> by the
/// handler; it never leaves this library.
/// </summary>
/// <param name="exitCode">The curl exit code the transfer reports.</param>
/// <param name="message">The message curl prints for the failure, without <c>curl: (N) </c>.</param>
internal sealed class SshTransferException(CurlExitCode exitCode, string message)
    : Exception(message)
{
    /// <summary>
    /// Gets the curl exit code the transfer reports.
    /// </summary>
    internal CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when libssh2's session startup fails:
    /// exit 2 and <c>Failure establishing ssh session: &lt;code&gt;, &lt;description&gt;</c>.
    /// </summary>
    /// <param name="libssh2ErrorCode">The libssh2 error code curl prints, one of <see cref="Libssh2ErrorCode" />.</param>
    /// <param name="description">The libssh2 description curl prints.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SessionEstablishmentFailed(int libssh2ErrorCode, string description) =>
        new(CurlExitCode.FailedInit, $"Failure establishing ssh session: {libssh2ErrorCode}, {description}");
}
