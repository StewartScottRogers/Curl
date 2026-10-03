namespace Curl.Cli;

/// <summary>
/// The <c>--libcurl</c> lines that need what the console learns about a transfer's files (BL-1177), measured
/// with curl 8.21.0 (Schannel) on 2026-10-02: the <c>-T</c> URL, upload flag and file size, the
/// <c>--etag-compare</c> header, the <c>-C -</c> offset and the SSH known-hosts file.
/// </summary>
public static partial class LibcurlSourceCode
{
    /// <summary>Stands in a transfer's lines where curl's tool fails to set <c>CURLOPT_SSH_KNOWNHOSTS</c>; never written.</summary>
    private const string KnownHostsSetoptFailed = "\0CURLOPT_SSH_KNOWNHOSTS failed";

    /// <summary>
    /// The URL <c>CURLOPT_URL</c> quotes: for a <c>-T</c> upload the URL <see cref="UploadTransferUrl" />
    /// resolves, the file name appended when the path names no file; otherwise, or when the URL cannot be
    /// parsed, the URL as given.
    /// </summary>
    private static string UploadUrlOf(LibcurlTransfer transfer) =>
        transfer.UploadFile is { } file && UploadTransferUrl.TryResolve(transfer.Url, file, out string resolved) ? resolved : transfer.Url;

    /// <summary>
    /// The offset <c>CURLOPT_RESUME_FROM_LARGE</c> writes: the <c>-C</c> offset; for <c>-C -</c> on an upload
    /// <c>-1</c>, so libcurl asks the server, and on a download the output file's size; zero, which is not
    /// written, when there is none.
    /// </summary>
    private static long ResumeOffsetOf(LibcurlTransfer transfer) =>
        !transfer.Options.ResumeFromOutputSize ? transfer.Options.ResumeFrom ?? 0
        : transfer.UploadFile is not null ? -1
        : transfer.OutputFileSize ?? 0;

    /// <summary>
    /// The lines after <c>--compressed-ssh</c>'s on an <c>scp</c> or <c>sftp</c> URL: the known-hosts file, or
    /// <see cref="KnownHostsSetoptFailed" /> when none was found and no host key fingerprint lets the
    /// transfer go on without one.
    /// </summary>
    private static List<string> KnownHostsLines(LibcurlTransfer transfer)
    {
        List<string> lines = [];
        AddStringIf(lines, "CURLOPT_SSH_KNOWNHOSTS", transfer.SshKnownHostsFile);
        AddIf(
            lines,
            transfer.SshKnownHostsFileMissing && transfer.Options.SshHostPublicKeyMd5 is null && transfer.Options.SshHostPublicKeySha256 is null,
            KnownHostsSetoptFailed);
        return lines;
    }
}
