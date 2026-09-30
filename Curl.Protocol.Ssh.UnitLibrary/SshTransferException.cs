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
    /// <summary>The exit 79 text curl prints for a failure of the SSH layer that has no message of its own.</summary>
    internal const string SshLayerErrorMessage = "Error in the SSH layer";

    /// <summary>
    /// Gets the curl exit code the transfer reports.
    /// </summary>
    internal CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>
    /// Gets a value indicating whether curl writes the message as a <c>-v</c> line too, as
    /// its <c>failf</c> does; <see langword="false" /> for a failure curl returns without a
    /// message of its own, whose text is the exit code's (ADR-0262).
    /// </summary>
    internal bool IsVerboseLine { get; private init; } = true;

    /// <summary>
    /// Gets a value indicating whether the failure came before curl finished setting up the
    /// session, so <c>-v</c> ends with <c>closing connection #N</c> rather than the connection
    /// left intact: the <c>SFTP</c> subsystem failing to start. Every failure before the
    /// transfer starts ends the connection whatever this says (ADR-0262).
    /// </summary>
    internal bool EndsConnection { get; private init; }

    /// <summary>
    /// Creates the exit 60 failure curl 8.21.0 reports when the known-hosts check refuses the
    /// host key: no message of its own, so curl prints the exit code's text.
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException KnownHostRefused() =>
        new(CurlExitCode.PeerFailedVerification, "SSL peer certificate or SSH remote key was not OK") { IsVerboseLine = false };

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
        new(CurlExitCode.LoginDenied, "Login denied") { IsVerboseLine = false };

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server's answer to the
    /// <c>none</c> request that asks for its method list cannot be read: exit 79 with no
    /// message of its own, so curl prints the exit code's text, <c>Error in the SSH
    /// layer</c>. Measured 2026-09-29 (BL-567) for a close, a disconnect and a malformed
    /// <c>SSH_MSG_USERAUTH_FAILURE</c>.
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException SshLayerError() =>
        new(CurlExitCode.Ssh, SshLayerErrorMessage) { IsVerboseLine = false };

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when libssh2 cannot start the SFTP session:
    /// exit 2 and <c>Failure initializing sftp session: &lt;description&gt;</c>, measured
    /// 2026-09-29 (BL-569, ADR-0220).
    /// </summary>
    /// <param name="description">libssh2's description, such as <c>Unable to request SFTP subsystem</c>.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpInitializationFailed(string description) =>
        new(CurlExitCode.FailedInit, $"Failure initializing sftp session: {description}") { EndsConnection = true };

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
    /// Creates the failure curl 8.21.0 reports when the server answers an upload's
    /// <c>SSH_FXP_OPEN</c> with a failed status: the status's exit code and <c>Upload
    /// failed: &lt;description&gt; (&lt;status&gt;/-31)</c>, libssh2's
    /// <c>LIBSSH2_ERROR_SFTP_PROTOCOL</c>, measured 2026-09-29 for codes 1, 2, 3, 4 and 11
    /// (BL-571, ADR-0244).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpUploadFailed(uint status) =>
        new(Sftp.SftpStatusCode.ExitCodeFor(status), $"Upload failed: {Sftp.SftpStatusCode.DescriptionOf(status)} ({status}/-31)");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when an upload's second <c>SSH_FXP_OPEN</c>,
    /// after <c>--ftp-create-dirs</c> made the directories, fails too: the status's exit
    /// code and <c>Creating the dir/file failed: &lt;description&gt;</c>, measured 2026-09-29
    /// (BL-571, ADR-0244).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpCreateFailed(uint status) =>
        new(Sftp.SftpStatusCode.ExitCodeFor(status), $"Creating the dir/file failed: {Sftp.SftpStatusCode.DescriptionOf(status)}");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server answers
    /// <c>SSH_FXP_OPENDIR</c> with a failed status: the status's exit code and <c>Could not
    /// open directory for reading: &lt;description&gt;</c>, measured 2026-09-29 for codes 1,
    /// 2, 3 and 4 (BL-570, ADR-0241).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpOpenDirectoryFailed(uint status) =>
        new(Sftp.SftpStatusCode.ExitCodeFor(status), $"Could not open directory for reading: {Sftp.SftpStatusCode.DescriptionOf(status)}");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server answers
    /// <c>SSH_FXP_READDIR</c> with a failed status: the status's exit code and <c>Could not
    /// open remote file for reading: &lt;description&gt; :: -31</c>, libssh2's
    /// <c>LIBSSH2_ERROR_SFTP_PROTOCOL</c>, measured 2026-09-29 (BL-570, ADR-0241).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpReadDirectoryFailed(uint status) =>
        new(Sftp.SftpStatusCode.ExitCodeFor(status), $"Could not open remote file for reading: {Sftp.SftpStatusCode.DescriptionOf(status)} :: -31");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server's answer to
    /// <c>SSH_FXP_READLINK</c> for a listed symbolic link is a failed status or names no
    /// target: exit 27 and <c>Out of memory</c>, measured 2026-09-29 (BL-570, ADR-0241).
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpReadLinkFailed() =>
        new(CurlExitCode.OutOfMemory, "Out of memory");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when a request with no message of its own,
    /// such as <c>SSH_FXP_REALPATH</c>, ends in a failed status: the status's exit code
    /// and that exit code's text, measured for codes 2, 3 and 4 (BL-569, ADR-0220).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpRequestFailed(uint status) =>
        new(Sftp.SftpStatusCode.ExitCodeFor(status), Sftp.SftpStatusCode.ExitCodeTextFor(status));

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when an <c>sftp://</c> URL's path decodes to
    /// a zero byte, which its <c>Curl_getworkingpath</c> refuses after <c>REALPATH</c>: exit 3
    /// with no message of its own, so curl prints the exit code's text, measured 2026-09-30
    /// for <c>/~/bl572/files/a%00.txt</c> (BL-974).
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException UrlPathHoldsZeroByte() =>
        new(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL") { IsVerboseLine = false };

    /// <summary>
    /// Creates the failure curl 8.21.0 reports for an SFTP <c>-Q</c> command that cannot be
    /// read or that the server refuses: exit 21 and curl's message, such as <c>rm
    /// "/f" failed: No such file or directory</c>, measured 2026-09-29 (BL-572, ADR-0247).
    /// </summary>
    /// <param name="message">curl's message.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpQuoteFailed(string message) =>
        new(CurlExitCode.QuoteError, message);

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when an SFTP download's range, or its
    /// <c>-C</c> offset, starts beyond the file: exit 33 and <c>Offset (N) was beyond file
    /// size (S)</c>, measured 2026-09-29 for <c>-r 11-</c>, <c>-r 20-30</c> and <c>-C 11</c>
    /// on a 10-byte file (BL-573, ADR-0253).
    /// </summary>
    /// <param name="offset">Where the range starts.</param>
    /// <param name="size">The file's size.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpRangeBeyondFileSize(long offset, long size) =>
        new(CurlExitCode.RangeError, $"Offset ({offset}) was beyond file size ({size})");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when an SFTP download's range, or its
    /// <c>-C</c> offset, starts at the file's end: exit 33 and <c>Bad range: start offset
    /// larger than end offset</c>, measured 2026-09-29 for <c>-r 10-</c> and <c>-C 10</c> on a
    /// 10-byte file (BL-573, ADR-0253).
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpRangeStartAfterEnd() =>
        new(CurlExitCode.RangeError, "Bad range: start offset larger than end offset");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when an SFTP download resumes with <c>-C</c>
    /// and <c>STAT</c> gave no size: exit 36 and <c>Offset (N) was beyond file size (S)</c>,
    /// measured 2026-09-29 as <c>Offset (3) was beyond file size (0)</c> for <c>-C 3</c> on an
    /// empty file (BL-573, ADR-0253).
    /// </summary>
    /// <param name="offset">The <c>-C</c> offset.</param>
    /// <param name="size">The size curl compares it with.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException SftpResumeBeyondFileSize(long offset, long size) =>
        new(CurlExitCode.BadDownloadResume, $"Offset ({offset}) was beyond file size ({size})");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server refuses the <c>session</c>
    /// channel an SCP transfer opens: exit 79 and libssh2's text for the reason code,
    /// measured 2026-09-29 as <c>Channel open failure (connect failed)</c> for OpenSSH's
    /// <c>MaxSessions 0</c> (BL-574, ADR-0225).
    /// </summary>
    /// <param name="reasonCode">The <c>SSH_MSG_CHANNEL_OPEN_FAILURE</c> reason code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException ScpChannelOpenFailed(uint reasonCode) =>
        new(CurlExitCode.Ssh, ChannelOpenFailureText(reasonCode));

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server refuses the <c>session</c>
    /// channel an SCP upload opens: exit 25 and libssh2's text for the reason code,
    /// measured 2026-09-29 as <c>Channel open failure (connect failed)</c> for OpenSSH's
    /// <c>MaxSessions 0</c> (BL-577, ADR-0258).
    /// </summary>
    /// <param name="reasonCode">The <c>SSH_MSG_CHANNEL_OPEN_FAILURE</c> reason code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException ScpUploadChannelOpenFailed(uint reasonCode) =>
        ScpUploadFailed(ChannelOpenFailureText(reasonCode));

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when an SCP upload's source has no known
    /// size, as standard input has: exit 25 and <c>SCP requires a known file size for
    /// upload</c>, before any channel is opened, measured 2026-09-29 (BL-577, ADR-0258).
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException ScpUploadSizeUnknown() =>
        ScpUploadFailed("SCP requires a known file size for upload");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when libssh2 cannot start an SCP upload:
    /// exit 25, to which curl maps every libssh2 failure there, and libssh2's message, such
    /// as <c>failed to send file</c>, measured 2026-09-29 (BL-577, ADR-0258).
    /// </summary>
    /// <param name="message">libssh2's message.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException ScpUploadFailed(string message) =>
        new(CurlExitCode.UploadFailed, message);

    private static string ChannelOpenFailureText(uint reasonCode) => reasonCode switch
    {
        1 => "Channel open failure (administratively prohibited)",
        2 => "Channel open failure (connect failed)",
        3 => "Channel open failure (unknown channel type)",
        4 => "Channel open failure (resource shortage)",
        _ => "Channel open failure",
    };

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server refuses the <c>exec</c>
    /// request that starts <c>scp</c>: exit 79 and libssh2's <c>Unable to complete request
    /// for channel-process-startup</c> (libssh2 1.11.1's <c>channel.c</c>, ADR-0225).
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException ScpExecRequestDenied() =>
        new(CurlExitCode.Ssh, "Unable to complete request for channel-process-startup");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the connection breaks while it reads
    /// the SCP server's <c>T</c> or <c>C</c> line: exit 79 and <c>Failed reading SCP
    /// response</c>, measured 2026-09-29 (BL-574, ADR-0225).
    /// </summary>
    /// <returns>The exception.</returns>
    internal static SshTransferException ScpResponseReadFailed() =>
        new(CurlExitCode.Ssh, "Failed reading SCP response");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports for an SCP answer libssh2 refuses - a remote
    /// error line, a malformed <c>T</c> or <c>C</c> line, or a channel that ends before
    /// them: exit 78 and libssh2's message, such as <c>Failed to recv file</c>, measured
    /// 2026-09-29 (BL-574, ADR-0225).
    /// </summary>
    /// <param name="message">libssh2's message.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException ScpProtocolError(string message) =>
        new(CurlExitCode.RemoteFileNotFound, message);
}
