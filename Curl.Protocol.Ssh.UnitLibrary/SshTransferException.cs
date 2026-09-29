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

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when no method it tried authenticated the
    /// user and <c>keyboard-interactive</c> was not among them: exit 67 and
    /// <c>Authentication failure</c>, measured 2026-09-29 (BL-567).
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException AuthenticationFailure() =>
        new(CurlExitCode.LoginDenied, "Authentication failure");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when <c>keyboard-interactive</c>, the last
    /// method it tries, fails: exit 67 with no message of its own, so curl prints the exit
    /// code's text, <c>Login denied</c>. Measured 2026-09-29 (BL-567).
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException LoginDenied() =>
        new(CurlExitCode.LoginDenied, "Login denied");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server's answer to the
    /// <c>none</c> request that asks for its method list cannot be read: exit 79 with no
    /// message of its own, so curl prints the exit code's text, <c>Error in the SSH
    /// layer</c>. Measured 2026-09-29 (BL-567) for a close, a disconnect and a malformed
    /// <c>SSH_MSG_USERAUTH_FAILURE</c>.
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException SshLayerError() =>
        new(CurlExitCode.Ssh, "Error in the SSH layer");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when libssh2 cannot start the SFTP session:
    /// exit 2 and <c>Failure initializing sftp session: &lt;description&gt;</c>, measured
    /// 2026-09-29 (BL-569, ADR-0220).
    /// </summary>
    /// <param name="description">libssh2's description, such as <c>Unable to request SFTP subsystem</c>.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpInitializationFailed(string description) =>
        new(CurlExitCode.FailedInit, $"Failure initializing sftp session: {description}");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server answers <c>SSH_FXP_OPEN</c>
    /// with a failed status: the status's exit code and <c>Could not open remote file for
    /// reading: &lt;description&gt;</c>, measured for every code (BL-569, ADR-0220).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpOpenFailed(uint status) =>
        new(Sftp.SftpStatusCode.ExitCodeFor(status), $"Could not open remote file for reading: {Sftp.SftpStatusCode.DescriptionOf(status)}");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when a request with no message of its own,
    /// such as <c>SSH_FXP_REALPATH</c>, ends in a failed status: the status's exit code
    /// and that exit code's text, measured for codes 2, 3 and 4 (BL-569, ADR-0220).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpRequestFailed(uint status) =>
        new(Sftp.SftpStatusCode.ExitCodeFor(status), Sftp.SftpStatusCode.ExitCodeTextFor(status));
}
