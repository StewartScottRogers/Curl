using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// What a <c>-T</c> upload over SFTP is asked to do beyond sending its bytes.
/// </summary>
/// <param name="ResumeFrom">The <c>-C</c> offset, 0 when not resuming.</param>
/// <param name="ResumeFromRemoteSize">Whether <c>-C -</c> asked to resume from the size <c>STAT</c> gives the remote file.</param>
/// <param name="Append">Whether <c>-a</c> asked to append to the remote file.</param>
/// <param name="CreateDirectories">Whether <c>--ftp-create-dirs</c> asked to create the missing directories.</param>
/// <param name="CreateFileMode">The permission bits the open carries, curl's <c>--create-file-mode</c>.</param>
internal sealed record SftpUploadOptions(long ResumeFrom, bool ResumeFromRemoteSize, bool Append, bool CreateDirectories, UnixFileMode CreateFileMode)
{
    /// <summary>
    /// Reads the options from the transfer's context; a negative <c>-C</c> offset counts as 0.
    /// </summary>
    /// <param name="context">The transfer's context.</param>
    /// <returns>The options.</returns>
    internal static SftpUploadOptions From(ITransferContext context) =>
        new(
            Math.Max(0, context.ResumeFrom ?? 0),
            context.ResumeUploadFromUnknownOffset,
            context.Append,
            context.FtpCreateDirectories,
            context.CreateFileMode);
}
