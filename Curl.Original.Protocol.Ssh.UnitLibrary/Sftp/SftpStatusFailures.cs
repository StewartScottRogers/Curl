using Curl.Ssh;

namespace Curl.Original.Protocol.Ssh.Sftp;

/// <summary>
/// Creates the <see cref="SshTransferException" /> for an SFTP request the server answered
/// with a failed <c>SSH_FX_*</c> status, worded as curl 8.21.0 words each (ADR-0220,
/// ADR-0241, ADR-0244). They live beside <see cref="SftpStatusCode" /> rather than on the
/// exception, which the transport layer in <c>Curl.Ssh.UnitLibrary</c> owns.
/// </summary>
internal static class SftpStatusFailures
{
    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server answers <c>SSH_FXP_OPEN</c>
    /// with a failed status: the status's exit code and <c>Could not open remote file for
    /// reading: &lt;description&gt;</c>, measured for every code (BL-569, ADR-0220).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException OpenFailed(uint status) =>
        new(SftpStatusCode.ExitCodeFor(status), $"Could not open remote file for reading: {SftpStatusCode.DescriptionOf(status)}");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server answers an upload's
    /// <c>SSH_FXP_OPEN</c> with a failed status: the status's exit code and <c>Upload
    /// failed: &lt;description&gt; (&lt;status&gt;/-31)</c>, libssh2's
    /// <c>LIBSSH2_ERROR_SFTP_PROTOCOL</c>, measured 2026-09-29 for codes 1, 2, 3, 4 and 11
    /// (BL-571, ADR-0244).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException UploadFailed(uint status) =>
        new(SftpStatusCode.ExitCodeFor(status), $"Upload failed: {SftpStatusCode.DescriptionOf(status)} ({status}/-31)");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when an upload's second <c>SSH_FXP_OPEN</c>,
    /// after <c>--ftp-create-dirs</c> made the directories, fails too: the status's exit
    /// code and <c>Creating the dir/file failed: &lt;description&gt;</c>, measured 2026-09-29
    /// (BL-571, ADR-0244).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException CreateFailed(uint status) =>
        new(SftpStatusCode.ExitCodeFor(status), $"Creating the dir/file failed: {SftpStatusCode.DescriptionOf(status)}");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server answers
    /// <c>SSH_FXP_OPENDIR</c> with a failed status: the status's exit code and <c>Could not
    /// open directory for reading: &lt;description&gt;</c>, measured 2026-09-29 for codes 1,
    /// 2, 3 and 4 (BL-570, ADR-0241).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException OpenDirectoryFailed(uint status) =>
        new(SftpStatusCode.ExitCodeFor(status), $"Could not open directory for reading: {SftpStatusCode.DescriptionOf(status)}");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when the server answers
    /// <c>SSH_FXP_READDIR</c> with a failed status: the status's exit code and <c>Could not
    /// open remote file for reading: &lt;description&gt; :: -31</c>, libssh2's
    /// <c>LIBSSH2_ERROR_SFTP_PROTOCOL</c>, measured 2026-09-29 (BL-570, ADR-0241).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException ReadDirectoryFailed(uint status) =>
        new(SftpStatusCode.ExitCodeFor(status), $"Could not open remote file for reading: {SftpStatusCode.DescriptionOf(status)} :: -31");

    /// <summary>
    /// Creates the failure curl 8.21.0 reports when a request with no message of its own,
    /// such as <c>SSH_FXP_REALPATH</c>, ends in a failed status: the status's exit code
    /// and that exit code's text, measured for codes 2, 3 and 4 (BL-569, ADR-0220).
    /// </summary>
    /// <param name="status">The <c>SSH_FX_*</c> code.</param>
    /// <returns>The exception.</returns>
    internal static SshTransferException RequestFailed(uint status) =>
        new(SftpStatusCode.ExitCodeFor(status), SftpStatusCode.ExitCodeTextFor(status));
}
