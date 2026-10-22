using System.Globalization;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Protocols;

namespace Libcurl.Options;

/// <summary>
/// The setter rows of every <c>CURLOPT_*</c> <see cref="EasyProtocolOptions"/> holds (FTP, SSH, SMTP,
/// TFTP, TELNET, RTSP and WebSocket), each checking its value as curl 8.22.0's <c>lib/setopt.c</c> does.
/// </summary>
internal static class EasyProtocolOptionSetters
{
    /// <summary>The smallest <c>CURLOPT_TFTP_BLKSIZE</c> libcurl accepts (<c>setopt.c</c>); a smaller value is not stored.</summary>
    private const long SmallestTftpBlockSize = 8;

    /// <summary>The largest <c>CURLOPT_TFTP_BLKSIZE</c> libcurl accepts: 65464, the 65535-byte UDP datagram less the TFTP and IP headers.</summary>
    private const long LargestTftpBlockSize = 65464;

    /// <summary>The TFTP block size RFC 1350 uses when no <c>CURLOPT_TFTP_BLKSIZE</c> is set.</summary>
    private const int DefaultTftpBlockSize = 512;

    /// <summary>Adds the protocol rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        AddFtpSetters(setters);
        AddSshSetters(setters);
        AddMailTftpAndTelnetSetters(setters);
        AddRtspAndWebSocketSetters(setters);
    }

    /// <summary>Adds the FTP rows (<c>CURLOPT_FTPPORT</c>, the quote lists, the EPSV and EPRT switches and the rest).</summary>
    private static void AddFtpSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Ftpport] = new(SetActiveAddress);
        setters[CurlOption.Quote] = new(SetQuote);
        setters[CurlOption.Prequote] = new(SetPreQuote);
        setters[CurlOption.Postquote] = new(SetPostQuote);
        setters[CurlOption.Append] = new(SetAppend);
        setters[CurlOption.FtpUseEprt] = new(SetUseEprt);
        setters[CurlOption.FtpUseEpsv] = new(SetUseEpsv);
        setters[CurlOption.FtpUsePret] = new(SetUsePret);
        setters[CurlOption.FtpCreateMissingDirs] = new(SetCreateMissingDirectories);
        setters[CurlOption.FtpAlternativeToUser] = new(SetAlternativeToUser);
        setters[CurlOption.FtpSkipPasvIp] = new(SetSkipPasvIp);
        setters[CurlOption.Ftpsslauth] = new(SetSslAuthentication);
        setters[CurlOption.FtpSslCcc] = new(SetClearCommandChannel);
        setters[CurlOption.FtpAccount] = new(SetAccount);
        setters[CurlOption.FtpFilemethod] = new(SetFileMethod);
    }

    /// <summary>Adds the SSH, SFTP and SCP rows (authentication types, host key and key file options).</summary>
    private static void AddSshSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.SshAuthTypes] = new(SetAuthenticationTypes);
        setters[CurlOption.SshCompression] = new(SetCompression);
        setters[CurlOption.SshHostPublicKeyMd5] = new(SetHostPublicKeyMd5);
        setters[CurlOption.SshHostPublicKeySha256] = new(SetHostPublicKeySha256);
        setters[CurlOption.SshPublicKeyfile] = new(SetPublicKeyFile);
        setters[CurlOption.SshPrivateKeyfile] = new(SetPrivateKeyFile);
        setters[CurlOption.SshKnownhosts] = new(SetKnownHostsFile);
    }

    /// <summary>Adds the SMTP and IMAP mail rows with the TFTP and TELNET rows, none of which has enough options to need its own method.</summary>
    private static void AddMailTftpAndTelnetSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.MailFrom] = new(SetMailFrom);
        setters[CurlOption.MailRcpt] = new(SetMailRecipients);
        setters[CurlOption.MailAuth] = new(SetMailAuth);
        setters[CurlOption.MailRcptAllowfails] = new(SetMailRecipientAllowFails);
        setters[CurlOption.TftpBlksize] = new(SetTftpBlockSize);
        setters[CurlOption.TftpNoOptions] = new(SetTftpNoOptions);
        setters[CurlOption.Telnetoptions] = new(SetTelnetOptions);
    }

    /// <summary>Adds the RTSP rows and <c>CURLOPT_WS_OPTIONS</c>.</summary>
    private static void AddRtspAndWebSocketSetters(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.RtspRequest] = new(SetRtspRequest);
        setters[CurlOption.RtspSessionId] = new(SetRtspSessionId);
        setters[CurlOption.RtspStreamUri] = new(SetRtspStreamUri);
        setters[CurlOption.RtspTransport] = new(SetRtspTransport);
        setters[CurlOption.RtspClientCseq] = new(SetRtspClientCSeq);
        setters[CurlOption.RtspServerCseq] = new(SetRtspServerCSeq);
        setters[CurlOption.WsOptions] = new(SetWebSocketOptions);
    }

    private static CurlCode SetActiveAddress(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { ActiveAddress = (string?)v });

    private static CurlCode SetQuote(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { Quote = ListOf(v) });

    private static CurlCode SetPreQuote(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { PreQuote = ListOf(v) });

    private static CurlCode SetPostQuote(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { PostQuote = ListOf(v) });

    private static CurlCode SetAppend(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { Append = IsOn(v) });

    private static CurlCode SetUseEprt(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { UseEprt = IsOn(v) });

    private static CurlCode SetUseEpsv(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { UseEpsv = IsOn(v) });

    private static CurlCode SetUsePret(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { UsePret = IsOn(v) });

    private static CurlCode SetAlternativeToUser(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { AlternativeToUser = (string?)v });

    private static CurlCode SetSkipPasvIp(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { SkipPasvIp = IsOn(v) });

    private static CurlCode SetAccount(ref EasyOptions o, object? v) => Ftp(ref o, f => f with { Account = (string?)v });

    /// <summary><c>CURLOPT_FTP_CREATE_MISSING_DIRS</c>: 0 to 2 (<c>CURLFTP_CREATE_DIR_*</c>), else <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetCreateMissingDirectories(ref EasyOptions o, object? v) =>
        InRange(v, (long)FtpCreateMissingDirectories.CreateAndRetry, out var n)
            ? Ftp(ref o, f => f with { CreateMissingDirectories = (FtpCreateMissingDirectories)n })
            : CurlCode.BadFunctionArgument;

    /// <summary><c>CURLOPT_FTPSSLAUTH</c>: 0 to 2 (<c>CURLFTPAUTH_*</c>), else <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetSslAuthentication(ref EasyOptions o, object? v) =>
        InRange(v, (long)FtpSslAuthentication.Tls, out var n)
            ? Ftp(ref o, f => f with { SslAuthentication = (FtpSslAuthentication)n })
            : CurlCode.BadFunctionArgument;

    /// <summary><c>CURLOPT_FTP_SSL_CCC</c>: 0 to 2 (<c>CURLFTPSSL_CCC_*</c>), else <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetClearCommandChannel(ref EasyOptions o, object? v) =>
        InRange(v, (long)FtpClearCommandChannel.Active, out var n)
            ? Ftp(ref o, f => f with { ClearCommandChannel = (FtpClearCommandChannel)n })
            : CurlCode.BadFunctionArgument;

    /// <summary><c>CURLOPT_FTP_FILEMETHOD</c>: 0 to 3 (<c>CURLFTPMETHOD_*</c>), else <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private static CurlCode SetFileMethod(ref EasyOptions o, object? v) =>
        InRange(v, (long)FtpFileMethod.SingleCwd, out var n)
            ? Ftp(ref o, f => f with { FileMethod = (FtpFileMethod)n })
            : CurlCode.BadFunctionArgument;

    /// <summary><c>CURLOPT_SSH_AUTH_TYPES</c>: the low 32 bits are kept, unchecked, as libcurl's <c>(int)arg</c> does.</summary>
    private static CurlCode SetAuthenticationTypes(ref EasyOptions o, object? v) =>
        Ssh(ref o, s => s with { AuthenticationTypes = (SshAuthenticationTypes)unchecked((uint)NumberOf(v)) });

    private static CurlCode SetCompression(ref EasyOptions o, object? v) => Ssh(ref o, s => s with { Compression = IsOn(v) });

    /// <summary><c>CURLOPT_SSH_HOST_PUBLIC_KEY_MD5</c>: any string is stored, as libcurl's <c>Curl_setstropt</c> does, and the host key check at connect time refuses one that is not the key's fingerprint; <see langword="null"/> clears it.</summary>
    private static CurlCode SetHostPublicKeyMd5(ref EasyOptions o, object? v) =>
        Ssh(ref o, s => s with { HostPublicKeyMd5 = (string?)v });

    private static CurlCode SetHostPublicKeySha256(ref EasyOptions o, object? v) => Ssh(ref o, s => s with { HostPublicKeySha256 = (string?)v });

    private static CurlCode SetPublicKeyFile(ref EasyOptions o, object? v) => Ssh(ref o, s => s with { PublicKeyFile = (string?)v });

    private static CurlCode SetPrivateKeyFile(ref EasyOptions o, object? v) => Ssh(ref o, s => s with { PrivateKeyFile = (string?)v });

    private static CurlCode SetKnownHostsFile(ref EasyOptions o, object? v) => Ssh(ref o, s => s with { KnownHostsFile = (string?)v });

    private static CurlCode SetMailFrom(ref EasyOptions o, object? v) => Smtp(ref o, m => m with { MailFrom = (string?)v });

    private static CurlCode SetMailRecipients(ref EasyOptions o, object? v) => Smtp(ref o, m => m with { MailRecipients = ListOf(v) });

    private static CurlCode SetMailAuth(ref EasyOptions o, object? v) => Smtp(ref o, m => m with { MailAuth = (string?)v });

    private static CurlCode SetMailRecipientAllowFails(ref EasyOptions o, object? v) => Smtp(ref o, m => m with { MailRecipientAllowFails = IsOn(v) });

    /// <summary><c>CURLOPT_TFTP_BLKSIZE</c>: below 8 becomes 512 and above 65464 becomes 65464, as libcurl does.</summary>
    private static CurlCode SetTftpBlockSize(ref EasyOptions o, object? v)
    {
        var size = NumberOf(v);
        var blockSize = size < SmallestTftpBlockSize ? DefaultTftpBlockSize : (int)Math.Min(size, LargestTftpBlockSize);
        return Tftp(ref o, t => t with { BlockSize = blockSize });
    }

    private static CurlCode SetTftpNoOptions(ref EasyOptions o, object? v) => Tftp(ref o, t => t with { NoOptions = IsOn(v) });

    /// <summary><c>CURLOPT_TELNETOPTIONS</c>: a copy of the <c>NAME=value</c> list; <see langword="null"/> empties it.</summary>
    private static CurlCode SetTelnetOptions(ref EasyOptions o, object? v)
    {
        o = o with { Protocols = o.Protocols with { Telnet = o.Protocols.Telnet with { TelnetOptions = ListOf(v) } } };
        return CurlCode.Ok;
    }

    /// <summary><c>CURLOPT_RTSP_REQUEST</c>: an unknown value becomes <see cref="RtspRequest.None"/>, as libcurl's switch default does.</summary>
    private static CurlCode SetRtspRequest(ref EasyOptions o, object? v)
    {
        var request = (RtspRequest)NumberOf(v);
        return Rtsp(ref o, r => r with { Request = Enum.IsDefined(request) ? request : RtspRequest.None });
    }

    /// <summary><c>CURLOPT_RTSP_SESSION_ID</c>: every set is counted, so the engine hands on one that repeats the value before it (BL-2787).</summary>
    private static CurlCode SetRtspSessionId(ref EasyOptions o, object? v) =>
        Rtsp(ref o, r => r with { SessionId = (string?)v, SessionIdSetCount = r.SessionIdSetCount + 1 });

    /// <summary><c>CURLOPT_RTSP_STREAM_URI</c>: <see langword="null"/> restores the default <c>*</c>, which libcurl sends for no URI.</summary>
    private static CurlCode SetRtspStreamUri(ref EasyOptions o, object? v) => Rtsp(ref o, r => r with { StreamUri = (string?)v ?? "*" });

    private static CurlCode SetRtspTransport(ref EasyOptions o, object? v) => Rtsp(ref o, r => r with { Transport = (string?)v });

    private static CurlCode SetRtspClientCSeq(ref EasyOptions o, object? v) => Rtsp(ref o, r => r with { ClientCSeq = NumberOf(v) });

    private static CurlCode SetRtspServerCSeq(ref EasyOptions o, object? v) => Rtsp(ref o, r => r with { ServerCSeq = NumberOf(v) });

    /// <summary><c>CURLOPT_WS_OPTIONS</c>: only the <c>CURLWS_RAW_MODE</c> and <c>CURLWS_NOAUTOPONG</c> bits are kept.</summary>
    private static CurlCode SetWebSocketOptions(ref EasyOptions o, object? v)
    {
        var bits = (WebSocketOptionBits)(NumberOf(v) & (long)(WebSocketOptionBits.RawMode | WebSocketOptionBits.NoAutoPong));
        o = o with { Protocols = o.Protocols with { WebSocket = o.Protocols.WebSocket with { Options = bits } } };
        return CurlCode.Ok;
    }

    /// <summary>Replaces the FTP options with what <paramref name="change"/> returns for them; always <see cref="CurlCode.Ok"/>.</summary>
    private static CurlCode Ftp(ref EasyOptions options, Func<EasyFtpOptions, EasyFtpOptions> change)
    {
        options = options with { Protocols = options.Protocols with { Ftp = change(options.Protocols.Ftp) } };
        return CurlCode.Ok;
    }

    /// <summary>Replaces the SSH options with what <paramref name="change"/> returns for them; always <see cref="CurlCode.Ok"/>.</summary>
    private static CurlCode Ssh(ref EasyOptions options, Func<EasySshOptions, EasySshOptions> change)
    {
        options = options with { Protocols = options.Protocols with { Ssh = change(options.Protocols.Ssh) } };
        return CurlCode.Ok;
    }

    /// <summary>Replaces the SMTP options with what <paramref name="change"/> returns for them; always <see cref="CurlCode.Ok"/>.</summary>
    private static CurlCode Smtp(ref EasyOptions options, Func<EasySmtpOptions, EasySmtpOptions> change)
    {
        options = options with { Protocols = options.Protocols with { Smtp = change(options.Protocols.Smtp) } };
        return CurlCode.Ok;
    }

    /// <summary>Replaces the TFTP options with what <paramref name="change"/> returns for them; always <see cref="CurlCode.Ok"/>.</summary>
    private static CurlCode Tftp(ref EasyOptions options, Func<EasyTftpOptions, EasyTftpOptions> change)
    {
        options = options with { Protocols = options.Protocols with { Tftp = change(options.Protocols.Tftp) } };
        return CurlCode.Ok;
    }

    /// <summary>Replaces the RTSP options with what <paramref name="change"/> returns for them; always <see cref="CurlCode.Ok"/>.</summary>
    private static CurlCode Rtsp(ref EasyOptions options, Func<EasyRtspOptions, EasyRtspOptions> change)
    {
        options = options with { Protocols = options.Protocols with { Rtsp = change(options.Protocols.Rtsp) } };
        return CurlCode.Ok;
    }

    /// <summary>Whether <paramref name="value"/> is 0 to <paramref name="largest"/>, the range <c>setopt.c</c> checks enum-valued options against.</summary>
    private static bool InRange(object? value, long largest, out long number)
    {
        number = NumberOf(value);
        return number >= 0 && number <= largest;
    }

    /// <summary>A copy of the string list <paramref name="value"/> (<c>curl_slist</c>), so the caller may change its own afterwards; an empty list for <see langword="null"/>.</summary>
    private static IReadOnlyList<string> ListOf(object? value) => value is IEnumerable<string> lines ? [.. lines] : [];

    /// <summary>Whether the option's integer value is not 0, as libcurl treats an on/off option.</summary>
    private static bool IsOn(object? value) => NumberOf(value) != 0;

    /// <summary>The option's integer value, whether it arrived as an <see cref="int"/>, a <see cref="long"/> or an enum.</summary>
    private static long NumberOf(object? value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);
}
