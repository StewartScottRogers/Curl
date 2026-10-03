using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// The <c>--libcurl</c> lines for the FTP, SSH, TFTP, telnet, mail, verbose, rate-limit and protocol options
/// BL-1174 measured with curl 8.21.0 (Schannel) on 2026-10-02: each method writes its options' lines in the
/// order curl writes them, and <see cref="LibcurlSourceCode" />'s other parts call each at its place in that
/// order. Only the FTP and SSH lines depend on the URL's scheme; curl writes the others for every scheme.
/// </summary>
public static partial class LibcurlSourceCode
{
    /// <summary>The buffer size curl's tool sets, and the largest a <c>--limit-rate</c> lowers it from.</summary>
    private const long DefaultBufferSize = 102400;

    /// <summary>The schemes curl 8.21.0's Schannel build writes for <c>--proto all</c>: it has no SMB.</summary>
    private static readonly HashSet<string> SchemesWithoutLibcurlSupport = ["smb", "smbs"];

    private static readonly string[] DebugCallbackLines =
    [
        "  CURLOPT_DEBUGFUNCTION was set to a function pointer",
        "  CURLOPT_DEBUGDATA was set to an object pointer",
    ];

    private static readonly string[] SocketOptionCallbackLines =
    [
        "  CURLOPT_SOCKOPTFUNCTION was set to a function pointer",
        "  CURLOPT_SOCKOPTDATA was set to an object pointer",
    ];

    /// <summary>
    /// The list of options that cannot be written as source and the <c>curl_easy_perform</c> call; nothing
    /// with <c>-Z</c>, as curl 8.21.0 writes neither for a parallel transfer. <c>-v</c> and <c>--trace</c>
    /// add the debug callback first, <c>--mptcp</c> the open-socket callback and a non-zero
    /// <c>--ip-tos</c> or <c>--vlan-priority</c> the socket-option callback last.
    /// </summary>
    private static List<string> UngeneratableOptionLinesFor(CommandLineOptions options)
    {
        if (options.Parallel)
        {
            return [];
        }

        List<string> lines = [.. UngeneratableOptionHeaderLines];
        AddRangeIf(lines, options.Trace != TraceKind.None, DebugCallbackLines);
        lines.AddRange(StandardUngeneratableOptionLines);
        AddIf(lines, options.MultipathTcp, "  CURLOPT_OPENSOCKETFUNCTION was set to a function pointer");
        AddRangeIf(lines, options.IpTypeOfService != 0 || options.VlanPriority != 0, SocketOptionCallbackLines);
        lines.AddRange(UngeneratableOptionFooterLines);
        return lines;
    }

    /// <summary>
    /// <c>CURLOPT_BUFFERSIZE</c>: curl's 100 KiB, or a smaller <c>--limit-rate</c>, so that one read never
    /// exceeds a second's worth.
    /// </summary>
    private static string BufferSizeLine(CommandLineOptions options) =>
        Setopt("CURLOPT_BUFFERSIZE", $"{(options.LimitRate is > 0 and < DefaultBufferSize ? options.LimitRate : DefaultBufferSize)}L");

    /// <summary>The <c>--limit-rate</c> lines, between the <c>-Y</c>/<c>-y</c> lines and <c>-C</c>; nothing for a zero rate.</summary>
    private static List<string> RateLimitLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddIf(lines, options.LimitRate > 0, () => Setopt("CURLOPT_MAX_SEND_SPEED_LARGE", $"(curl_off_t){options.LimitRate}"));
        AddIf(lines, options.LimitRate > 0, () => Setopt("CURLOPT_MAX_RECV_SPEED_LARGE", $"(curl_off_t){options.LimitRate}"));
        return lines;
    }

    /// <summary>
    /// The lines curl 8.21.0 writes for an <c>ftp</c> or <c>ftps</c> URL, in its order: <c>-P</c>, the CCC
    /// mode and <c>--ftp-account</c>, the passive-IP skip (on unless <c>--no-ftp-skip-pasv-ip</c>), then
    /// <c>--ftp-method</c> (written whenever given, <c>multicwd</c> as <c>1L</c>),
    /// <c>--ftp-alternative-to-user</c> and <c>--ftp-pret</c>.
    /// </summary>
    private static List<string> FtpLines(CommandLineOptions options)
    {
        List<string> lines = [];
        AddStringIf(lines, "CURLOPT_FTPPORT", options.FtpPort);
        AddIf(lines, options.FtpClearCommandChannel != FtpClearCommandChannel.Off, () => Setopt("CURLOPT_FTP_SSL_CCC", options.FtpClearCommandChannel == FtpClearCommandChannel.Active ? "(long)CURLFTPSSL_CCC_ACTIVE" : "(long)CURLFTPSSL_CCC_PASSIVE"));
        AddStringIf(lines, "CURLOPT_FTP_ACCOUNT", options.FtpAccount);
        AddIf(lines, options.FtpSkipPasvIp, SetoptOn("CURLOPT_FTP_SKIP_PASV_IP"));
        AddIf(lines, options.FtpFileMethodGiven, () => Setopt("CURLOPT_FTP_FILEMETHOD", $"{(int)options.FtpFileMethod + 1}L"));
        AddStringIf(lines, "CURLOPT_FTP_ALTERNATIVE_TO_USER", options.FtpAlternativeToUser);
        AddIf(lines, options.FtpSendPret, SetoptOn("CURLOPT_FTP_USE_PRET"));
        return lines;
    }

    /// <summary>
    /// The lines curl 8.21.0 writes for an <c>scp</c> or <c>sftp</c> URL, after the key passwords: the private
    /// and public key files, the host key hashes, <c>--compressed-ssh</c> and the known-hosts file.
    /// </summary>
    private static List<string> SshLines(LibcurlTransfer transfer, string scheme)
    {
        if (scheme is not ("scp" or "sftp"))
        {
            return [];
        }

        CommandLineOptions options = transfer.Options;
        List<string> lines = [];
        AddStringIf(lines, "CURLOPT_SSH_PRIVATE_KEYFILE", options.PrivateKey);
        AddStringIf(lines, "CURLOPT_SSH_PUBLIC_KEYFILE", options.SshPublicKeyFile);
        AddStringIf(lines, "CURLOPT_SSH_HOST_PUBLIC_KEY_MD5", options.SshHostPublicKeyMd5);
        AddStringIf(lines, "CURLOPT_SSH_HOST_PUBLIC_KEY_SHA256", options.SshHostPublicKeySha256);
        AddIf(lines, options.SshCompression, SetoptOn("CURLOPT_SSH_COMPRESSION"));
        lines.AddRange(KnownHostsLines(transfer));
        return lines;
    }

    /// <summary>
    /// <c>CURLOPT_USE_SSL</c>, after the cipher lists: <c>--ssl-reqd</c> requires TLS on everything, which
    /// outranks <c>--ftp-ssl-control</c>'s control connection only, which outranks <c>--ssl</c>'s try.
    /// </summary>
    private static List<string> UseSslLines(CommandLineOptions options)
    {
        string? level = options.SslRequired ? "CURLUSESSL_ALL" : options.FtpSslControlOnly ? "CURLUSESSL_CONTROL" : options.SslTry ? "CURLUSESSL_TRY" : null;
        return level is null ? [] : [Setopt("CURLOPT_USE_SSL", $"(long){level}")];
    }

    /// <summary>
    /// The <c>-Q</c> lists, after <c>--crlf</c>: a command starting with <c>-</c> goes after the transfer, one
    /// starting with <c>+</c> just before it, each without its prefix, and any other before it starts.
    /// </summary>
    private static List<string> QuoteLines(CommandLineOptions options, LibcurlSourceVariables variables)
    {
        List<string> lines = [];
        AddStringListIf(lines, "CURLOPT_QUOTE", [.. options.QuoteCommands.Where(command => !command.StartsWith('-') && !command.StartsWith('+'))], variables);
        AddStringListIf(lines, "CURLOPT_POSTQUOTE", [.. options.QuoteCommands.Where(command => command.StartsWith('-')).Select(command => command[1..])], variables);
        AddStringListIf(lines, "CURLOPT_PREQUOTE", [.. options.QuoteCommands.Where(command => command.StartsWith('+')).Select(command => command[1..])], variables);
        return lines;
    }

    /// <summary>
    /// The lines after the keepalive ones and before <c>CURLOPT_RESOLVE</c>: <c>--tftp-blksize</c> (any value
    /// given), the mail sender and recipients, a non-zero <c>--create-file-mode</c>, and the <c>--proto</c> and
    /// <c>--proto-redir</c> schemes.
    /// </summary>
    private static List<string> MailFileModeAndProtocolLines(CommandLineOptions options, LibcurlSourceVariables variables)
    {
        List<string> lines = [];
        AddIf(lines, options.TftpBlockSize is not null, () => Setopt("CURLOPT_TFTP_BLKSIZE", $"{options.TftpBlockSize}L"));
        AddStringIf(lines, "CURLOPT_MAIL_FROM", options.MailFrom);
        AddStringListIf(lines, "CURLOPT_MAIL_RCPT", options.MailRecipients, variables);
        AddIf(lines, options.MailRecipientAllowFails, SetoptOn("CURLOPT_MAIL_RCPT_ALLOWFAILS"));
        AddIf(lines, options.CreateFileMode is { } mode && mode != 0, () => Setopt("CURLOPT_NEW_FILE_PERMS", $"{(int)options.CreateFileMode!.Value}L"));
        AddStringIf(lines, "CURLOPT_PROTOCOLS_STR", ProtocolList(options.AllowedProtocols));
        AddStringIf(lines, "CURLOPT_REDIR_PROTOCOLS_STR", ProtocolList(options.AllowedRedirectProtocols));
        return lines;
    }

    /// <summary>The schemes a <c>--proto</c> set allows, lowercase, sorted and joined by commas as curl writes them.</summary>
    private static string? ProtocolList(IReadOnlySet<string>? schemes) =>
        schemes is null
            ? null
            : string.Join(",", schemes.Select(scheme => scheme.ToLowerInvariant()).Where(scheme => !SchemesWithoutLibcurlSupport.Contains(scheme)).Order(StringComparer.Ordinal));

    /// <summary>
    /// The <c>--upload-flags</c> line, the last of a transfer's lines: written whenever the flags are not
    /// libcurl's default of <c>\Seen</c> alone.
    /// </summary>
    private static List<string> UploadFlagLines(CommandLineOptions options) =>
        options.UploadFlags == ImapUploadFlags.Seen ? [] : [Setopt("CURLOPT_UPLOAD_FLAGS", $"{(int)options.UploadFlags}L")];

    private static void AddRangeIf(List<string> lines, bool condition, string[] more)
    {
        if (condition)
        {
            lines.AddRange(more);
        }
    }
}
