using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Protocols;
using Libcurl.Options;

namespace Curl.Console;

/// <summary>
/// The HTTP and protocol half of <see cref="EasySetoptsFromCommandLine" /> (BL-2493, ADR-0652): the
/// <c>curl_easy_setopt</c> calls <c>config2setopts.c</c> makes for those groups, and what of them stays on the hooks.
/// The <c>-F</c> form is not among the calls: the runner builds it as a <c>curl_mime</c> tree on the library and sets it
/// as <c>CURLOPT_MIMEPOST</c> itself (<see cref="CurlMimeFromFormBody" />).
/// </summary>
internal static partial class EasySetoptsFromCommandLine
{
    // CURLOPT_STREAM_WEIGHT keeps 1 to 256 and ignores anything else.
    private const long LeastStreamWeight = 1;

    private const long MostStreamWeight = 256;

    // CURLOPT_TFTP_BLKSIZE raises anything below 8 to 512 and cuts anything above 65464.
    private const long LeastTftpBlockSize = 8;

    private const long MostTftpBlockSize = 65464;

    // CURLOPT_SSH_HOST_PUBLIC_KEY_MD5 refuses a string of any other length.
    private const int Md5HexDigits = 32;

    private static readonly EasyHttpOptions UnsetHttp = new();

    private static readonly EasyProtocolOptions UnsetProtocols = new();

    // CURLOPT_POSTFIELDS comes before CURLOPT_POSTFIELDSIZE_LARGE, as the tool sets them.
    private static readonly SetoptRow<EasyHttpOptions>[] HttpRows =
    [
        new(CurlOption.Httpheader, h => ListOf(h.HttpHeaders)),
        new(CurlOption.Useragent, h => h.UserAgent),
        new(CurlOption.Referer, h => h.Referer),
        new(CurlOption.Autoreferer, h => FlagOf(h.AutoReferer)),
        new(CurlOption.AcceptEncoding, h => h.AcceptEncoding),
        new(CurlOption.TransferEncoding, h => FlagOf(h.TransferEncoding)),
        new(CurlOption.HttpContentDecoding, h => FlagOf(h.HttpContentDecoding)),
        new(CurlOption.HttpTransferDecoding, h => FlagOf(h.HttpTransferDecoding)),
        new(CurlOption.Followlocation, h => (long)h.FollowLocation),
        new(CurlOption.Maxredirs, h => h.MaxRedirects, IsSettableMaxRedirects),
        new(CurlOption.Postredir, h => (long)h.PostRedirect),
        new(CurlOption.Post, h => FlagOf(h.Post)),
        new(CurlOption.Postfields, h => h.PostFields),
        new(CurlOption.PostfieldsizeLarge, h => h.PostFieldSize, IsSettablePostFieldSize),
        new(CurlOption.Httpget, h => FlagOf(h.HttpGet)),
        new(CurlOption.MimeOptions, h => (long)h.MimeOptions),
        new(CurlOption.HttpVersion, h => (long)h.HttpVersion),
        new(CurlOption.Http09Allowed, h => FlagOf(h.Http09Allowed)),
        new(CurlOption.IgnoreContentLength, h => FlagOf(h.IgnoreContentLength)),
        new(CurlOption.Expect100TimeoutMs, h => WholeMillisecondsOf(h.Expect100Timeout), h => WholeMillisecondsOf(h.Expect100Timeout) is not null),
        new(CurlOption.Http200aliases, h => ListOf(h.Http200Aliases)),
        new(CurlOption.RequestTarget, h => h.RequestTarget),
        new(CurlOption.Cookie, h => h.Cookie),
        new(CurlOption.Cookiejar, h => h.CookieJar),
        new(CurlOption.Cookiesession, h => FlagOf(h.CookieSession)),
        new(CurlOption.Altsvc, h => h.AltSvc),
        new(CurlOption.AltsvcCtrl, h => (long)h.AltSvcControl),
        new(CurlOption.Hsts, h => h.Hsts),
        new(CurlOption.HstsCtrl, h => (long)h.HstsControl),
        new(CurlOption.Pipewait, h => FlagOf(h.PipeWait)),
        new(CurlOption.StreamWeight, h => (long)h.StreamWeight, h => h.StreamWeight is >= (int)LeastStreamWeight and <= (int)MostStreamWeight),
        new(CurlOption.SslEnableAlpn, h => FlagOf(h.SslEnableAlpn)),
    ];

    private static readonly SetoptRow<EasyProtocolOptions>[] ProtocolRows =
    [
        new(CurlOption.Ftpport, p => p.Ftp.ActiveAddress),
        new(CurlOption.Quote, p => ListOf(p.Ftp.Quote)),
        new(CurlOption.Prequote, p => ListOf(p.Ftp.PreQuote)),
        new(CurlOption.Postquote, p => ListOf(p.Ftp.PostQuote)),
        new(CurlOption.Append, p => FlagOf(p.Ftp.Append)),
        new(CurlOption.FtpUseEprt, p => FlagOf(p.Ftp.UseEprt)),
        new(CurlOption.FtpUseEpsv, p => FlagOf(p.Ftp.UseEpsv)),
        new(CurlOption.FtpUsePret, p => FlagOf(p.Ftp.UsePret)),
        new(CurlOption.FtpCreateMissingDirs, p => (long)p.Ftp.CreateMissingDirectories),
        new(CurlOption.FtpAlternativeToUser, p => p.Ftp.AlternativeToUser),
        new(CurlOption.FtpSkipPasvIp, p => FlagOf(p.Ftp.SkipPasvIp)),
        new(CurlOption.Ftpsslauth, p => (long)p.Ftp.SslAuthentication),
        new(CurlOption.FtpSslCcc, p => (long)p.Ftp.ClearCommandChannel),
        new(CurlOption.FtpAccount, p => p.Ftp.Account),
        new(CurlOption.FtpFilemethod, p => (long)p.Ftp.FileMethod),
        new(CurlOption.SshAuthTypes, p => (long)p.Ssh.AuthenticationTypes),
        new(CurlOption.SshCompression, p => FlagOf(p.Ssh.Compression)),
        new(CurlOption.SshHostPublicKeyMd5, p => p.Ssh.HostPublicKeyMd5, IsSettableHostPublicKeyMd5),
        new(CurlOption.SshHostPublicKeySha256, p => p.Ssh.HostPublicKeySha256),
        new(CurlOption.SshPublicKeyfile, p => p.Ssh.PublicKeyFile),
        new(CurlOption.SshPrivateKeyfile, p => p.Ssh.PrivateKeyFile),
        new(CurlOption.SshKnownhosts, p => p.Ssh.KnownHostsFile),
        new(CurlOption.MailFrom, p => p.Smtp.MailFrom),
        new(CurlOption.MailRcpt, p => ListOf(p.Smtp.MailRecipients)),
        new(CurlOption.MailAuth, p => p.Smtp.MailAuth),
        new(CurlOption.MailRcptAllowfails, p => FlagOf(p.Smtp.MailRecipientAllowFails)),
        new(CurlOption.TftpBlksize, p => (long)p.Tftp.BlockSize, IsSettableTftpBlockSize),
        new(CurlOption.TftpNoOptions, p => FlagOf(p.Tftp.NoOptions)),
        new(CurlOption.Telnetoptions, p => ListOf(p.Telnet.TelnetOptions)),
        new(CurlOption.RtspRequest, p => (long)p.Rtsp.Request),
        new(CurlOption.RtspSessionId, p => p.Rtsp.SessionId),
        new(CurlOption.RtspStreamUri, p => p.Rtsp.StreamUri),
        new(CurlOption.RtspTransport, p => p.Rtsp.Transport),
        new(CurlOption.RtspClientCseq, p => p.Rtsp.ClientCSeq),
        new(CurlOption.RtspServerCseq, p => p.Rtsp.ServerCSeq),
        new(CurlOption.WsOptions, p => (long)p.WebSocket.Options),
    ];

    // The HTTP rows, with one CURLOPT_COOKIEFILE per -b file after CURLOPT_COOKIE, as the tool sets one per file.
    private static IEnumerable<EasySetopt> HttpAndProtocolSetoptsOf(EasyOptions options) =>
        SetoptsOf(HttpRows, options.Http, UnsetHttp)
            .Concat(options.Http.CookieFiles.Select(file => new EasySetopt(CurlOption.Cookiefile, file)))
            .Concat(SetoptsOf(ProtocolRows, options.Protocols, UnsetProtocols));

    // The -F form (set through CURLOPT_MIMEPOST by the runner when it can build it), the curl_formadd form, the HTTP/2
    // stream dependency and the cookie list (the tool sets none of the three), and a value a setter would refuse or hold to
    // another value stay on the hooks.
    private static EasyHttpOptions HttpKeptOnHooks(EasyHttpOptions http) =>
        UnsetHttp with
        {
            MimePost = http.MimePost,
            HttpPost = http.HttpPost,
            StreamDependency = http.StreamDependency,
            CookieList = http.CookieList,
            MaxRedirects = IsSettableMaxRedirects(http) ? UnsetHttp.MaxRedirects : http.MaxRedirects,
            PostFieldSize = IsSettablePostFieldSize(http) ? UnsetHttp.PostFieldSize : http.PostFieldSize,
            Expect100Timeout = KeptUnlessWhole(http.Expect100Timeout, TimeSpan.TicksPerMillisecond, UnsetHttp.Expect100Timeout),
            StreamWeight = http.StreamWeight is >= (int)LeastStreamWeight and <= (int)MostStreamWeight ? UnsetHttp.StreamWeight : http.StreamWeight,
        };

    // An SSH host key MD5 that is not 32 hex digits and a TFTP block size CURLOPT_TFTP_BLKSIZE would not keep stay on the hooks.
    private static EasyProtocolOptions ProtocolsKeptOnHooks(EasyProtocolOptions protocols) =>
        UnsetProtocols with
        {
            Ssh = UnsetProtocols.Ssh with { HostPublicKeyMd5 = IsSettableHostPublicKeyMd5(protocols) ? null : protocols.Ssh.HostPublicKeyMd5 },
            Tftp = UnsetProtocols.Tftp with { BlockSize = IsSettableTftpBlockSize(protocols) ? UnsetProtocols.Tftp.BlockSize : protocols.Tftp.BlockSize },
        };

    // CURLOPT_MAXREDIRS and CURLOPT_POSTFIELDSIZE_LARGE refuse anything below -1.
    private static bool IsSettableMaxRedirects(EasyHttpOptions http) => http.MaxRedirects >= -1;

    private static bool IsSettablePostFieldSize(EasyHttpOptions http) => http.PostFieldSize >= -1;

    // CURLOPT_SSH_HOST_PUBLIC_KEY_MD5 refuses anything but 32 hex digits.
    private static bool IsSettableHostPublicKeyMd5(EasyProtocolOptions protocols) => protocols.Ssh.HostPublicKeyMd5 is null or { Length: Md5HexDigits };

    // CURLOPT_TFTP_BLKSIZE keeps only a size within the TFTP block size range.
    private static bool IsSettableTftpBlockSize(EasyProtocolOptions protocols) => protocols.Tftp.BlockSize is >= (int)LeastTftpBlockSize and <= (int)MostTftpBlockSize;
}
