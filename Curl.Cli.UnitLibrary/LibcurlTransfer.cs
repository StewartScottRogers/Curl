namespace Curl.Cli;

/// <summary>
/// One transfer as <see cref="LibcurlSourceCode" /> writes it: its option group, its URL as the glob expanded
/// it, and what only the console learns about the transfer's files (BL-1177).
/// </summary>
/// <param name="Options">The transfer's option group.</param>
/// <param name="Url">The URL as the glob expanded it, before any scheme is guessed or <c>-T</c> file name appended.</param>
public sealed record LibcurlTransfer(CommandLineOptions Options, string Url)
{
    /// <summary>The transfer's <c>-T</c> file, <c>-</c> or <c>.</c> for standard input; <see langword="null" /> when it uploads nothing.</summary>
    public string? UploadFile { get; init; }

    /// <summary>The size of the <c>-T</c> file when it was opened; <see langword="null" /> for standard input or a file that cannot be opened.</summary>
    public long? UploadFileSize { get; init; }

    /// <summary>The <c>If-None-Match</c> lines <c>--etag-compare</c> added to the transfer's group so far, this transfer's last.</summary>
    public IReadOnlyList<string> IfNoneMatchHeaders { get; init; } = [];

    /// <summary>For <c>-C -</c>, the size of the output file the download resumes; <see langword="null" /> when it cannot be opened.</summary>
    public long? OutputFileSize { get; init; }

    /// <summary>The known-hosts file an <c>scp</c> or <c>sftp</c> transfer checks the host key against; <see langword="null" /> under <c>-k</c> or when none was found.</summary>
    public string? SshKnownHostsFile { get; init; }

    /// <summary>
    /// Whether an <c>scp</c> or <c>sftp</c> transfer checked by known hosts found no known-hosts file: without
    /// <c>--hostpubmd5</c> or <c>--hostpubsha256</c> curl's tool then fails before it sets
    /// <c>CURLOPT_SSH_KNOWNHOSTS</c>, and the source stops there.
    /// </summary>
    public bool SshKnownHostsFileMissing { get; init; }
}
