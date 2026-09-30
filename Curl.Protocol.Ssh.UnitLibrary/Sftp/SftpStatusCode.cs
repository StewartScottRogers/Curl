using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// The <c>SSH_FX_*</c> codes of an <c>SSH_FXP_STATUS</c> packet that this library tells
/// apart, and what curl 8.21.0 makes of each code: the exit code (curl's
/// <c>sftp_libssh2_error_to_CURLE</c>) and the description it prints after <c>Could not
/// open remote file for reading: </c>. Every code from 0 to 21 and 22 and 99 were measured
/// 2026-09-29 against an <c>OPEN</c> answered with that status (BL-569, ADR-0220).
/// </summary>
internal static class SftpStatusCode
{
    /// <summary><c>SSH_FX_OK</c>: the request succeeded.</summary>
    internal const uint Ok = 0;

    /// <summary><c>SSH_FX_EOF</c>: a read started at or past the end of the file.</summary>
    internal const uint EndOfFile = 1;

    /// <summary>
    /// The description curl prints for a code libssh2 has no text for, and, as measured,
    /// for <see cref="EndOfFile" />.
    /// </summary>
    internal const string UnknownDescription = "Unknown error in libssh2";

    // Indexed by code: SSH_FX_OK to SSH_FX_LINK_LOOP (21). Codes 0 and 1 end no open in
    // curl's way - OK is waited past and EOF is unknown - so their rows are the fallback.
    private static readonly (CurlExitCode ExitCode, string Description)[] Outcomes =
    [
        (CurlExitCode.Ssh, UnknownDescription),
        (CurlExitCode.Ssh, UnknownDescription),
        (CurlExitCode.RemoteFileNotFound, "No such file or directory"),
        (CurlExitCode.RemoteAccessDenied, "Permission denied"),
        (CurlExitCode.Ssh, "Operation failed"),
        (CurlExitCode.Ssh, "Bad message from SFTP server"),
        (CurlExitCode.Ssh, "Not connected to SFTP server"),
        (CurlExitCode.Ssh, "Connection to SFTP server lost"),
        (CurlExitCode.Ssh, "Operation not supported by SFTP server"),
        (CurlExitCode.Ssh, "Invalid handle"),
        (CurlExitCode.RemoteFileNotFound, "No such file or directory"),
        (CurlExitCode.RemoteFileExists, "File already exists"),
        (CurlExitCode.RemoteAccessDenied, "File is write protected"),
        (CurlExitCode.Ssh, "No media"),
        (CurlExitCode.RemoteDiskFull, "Disk full"),
        (CurlExitCode.RemoteDiskFull, "User quota exceeded"),
        (CurlExitCode.Ssh, "Unknown principal"),
        (CurlExitCode.RemoteAccessDenied, "File lock conflict"),
        (CurlExitCode.QuoteError, "Directory not empty"),
        (CurlExitCode.Ssh, "Not a directory"),
        (CurlExitCode.Ssh, "Invalid filename"),
        (CurlExitCode.Ssh, "Link points to itself"),
    ];

    // curl's own text for each exit code a status maps to, printed alone when a failure
    // has no message of its own, as for REALPATH.
    private static readonly Dictionary<CurlExitCode, string> ExitCodeTexts = new()
    {
        [CurlExitCode.Ssh] = "Error in the SSH layer",
        [CurlExitCode.RemoteFileNotFound] = "Remote file not found",
        [CurlExitCode.RemoteAccessDenied] = "Access denied to remote resource",
        [CurlExitCode.RemoteFileExists] = "Remote file already exists",
        [CurlExitCode.RemoteDiskFull] = "Disk full or allocation exceeded",
        [CurlExitCode.QuoteError] = "Quote command returned error",
    };

    /// <summary>
    /// Gets the exit code curl reports for <paramref name="code" />: 78 for no such file or
    /// path, 9 for permission denied, write protect and lock conflict, 70 for a full disk or
    /// quota, 73 for a file that exists, 21 for a directory not empty, 79 for the rest.
    /// </summary>
    /// <param name="code">The status code.</param>
    /// <returns>The exit code.</returns>
    internal static CurlExitCode ExitCodeFor(uint code) => OutcomeOf(code).ExitCode;

    /// <summary>
    /// Gets the description curl prints for <paramref name="code" />.
    /// </summary>
    /// <param name="code">The status code.</param>
    /// <returns>The description, or <see cref="UnknownDescription" />.</returns>
    internal static string DescriptionOf(uint code) => OutcomeOf(code).Description;

    /// <summary>
    /// Gets curl's text for the exit code <paramref name="code" /> maps to, which curl prints
    /// when the failed request has no message of its own.
    /// </summary>
    /// <param name="code">The status code.</param>
    /// <returns>The exit code's text, such as <c>Remote file not found</c>.</returns>
    internal static string ExitCodeTextFor(uint code) => ExitCodeTexts[ExitCodeFor(code)];

    private static (CurlExitCode ExitCode, string Description) OutcomeOf(uint code) =>
        code < Outcomes.Length ? Outcomes[code] : (CurlExitCode.Ssh, UnknownDescription);
}
