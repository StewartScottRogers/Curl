namespace Curl.Extension.Abstractions.Transfer;

/// <summary>
/// The context a hook's <see cref="TransferContextRequests.WithRequest" /> gives: the transfer's own context,
/// but sent to another URL with another HTTP method and request body, so an extension's own request (a WebDAV
/// <c>PROPFIND</c>, a sync <c>GET</c>) goes through curl's HTTP stack (TLS, proxy, <c>--user</c>, redirects).
/// </summary>
/// <param name="inner">The transfer's own context.</param>
/// <param name="url">The URL the request goes to.</param>
/// <param name="http">The HTTP options with the request's method, or <see langword="null" /> for a transfer that has none.</param>
/// <param name="postData">The request body.</param>
/// <param name="credentials">The credentials to send, or <see langword="null" /> for none.</param>
internal sealed class RetargetedTransferContext(ITransferContext inner, CurlUrl url, HttpRequestOptions? http, ReadOnlyMemory<byte>? postData, System.Net.NetworkCredential? credentials) : ITransferContext
{
    public CurlUrl Url => url;
    public Stream Output => inner.Output;
    public Stream? Upload => inner.Upload;
    public long? ResumeFrom => inner.ResumeFrom;
    public bool ResumeUploadFromUnknownOffset => inner.ResumeUploadFromUnknownOffset;
    public ByteRange? Range => inner.Range;
    public string? RangeText => inner.RangeText;
    public long? MaxFileSize => inner.MaxFileSize;
    public bool NoBody => inner.NoBody;
    public TimeCondition? TimeCondition => inner.TimeCondition;
    public bool RemoteTime => inner.RemoteTime;
    public Stream? HeaderOutput => inner.HeaderOutput;
    public Stream? DumpHeaderOutput => inner.DumpHeaderOutput;
    public ReadOnlyMemory<byte>? PostData => postData;
    public bool PostWithoutData => inner.PostWithoutData;
    public System.Net.NetworkCredential? Credentials => credentials;
    public IReadOnlyList<string> TelnetOptions => inner.TelnetOptions;
    public int? TftpBlockSize => inner.TftpBlockSize;
    public bool TftpNoOptions => inner.TftpNoOptions;
    public bool FtpDisableEpsv => inner.FtpDisableEpsv;
    public bool FtpSkipPasvIp => inner.FtpSkipPasvIp;
    public FtpFileMethod FtpFileMethod => inner.FtpFileMethod;
    public bool FtpCreateDirectories => inner.FtpCreateDirectories;
    public string? FtpAccount => inner.FtpAccount;
    public string? FtpAlternativeToUser => inner.FtpAlternativeToUser;
    public bool FtpSendPret => inner.FtpSendPret;
    public bool ListOnly => inner.ListOnly;
    public bool UseAscii => inner.UseAscii;
    public bool Append => inner.Append;
    public string? FtpPort => inner.FtpPort;
    public bool FtpUseEprt => inner.FtpUseEprt;
    public TimeSpan FtpAcceptTimeout => inner.FtpAcceptTimeout;
    public TimeSpan FtpServerResponseTimeout => inner.FtpServerResponseTimeout;
    public TransportSecurityLevel SslLevel => inner.SslLevel;
    public bool FtpSslControlOnly => inner.FtpSslControlOnly;
    public FtpCommandChannelClearing FtpCommandChannelClearing => inner.FtpCommandChannelClearing;
    public IReadOnlyList<string> QuoteCommands => inner.QuoteCommands;
    public bool ConvertLineEndings => inner.ConvertLineEndings;
    public UnixFileMode CreateFileMode => inner.CreateFileMode;
    public bool PathAsIs => inner.PathAsIs;
    public TimeSpan? ConnectTimeout => inner.ConnectTimeout;
    public TimeSpan? MaxTime => inner.MaxTime;
    public long? OperationStarted => inner.OperationStarted;
    public ProxyEndpoint? Proxy => inner.Proxy;
    public MailRequestOptions? Mail => inner.Mail;
    public SshOptions? Ssh => inner.Ssh;
    public TimeProvider TimeProvider => inner.TimeProvider;
    public ITransferEvents Events => inner.Events;
    public ITransferProgress Progress => inner.Progress;
    public CancellationToken CancellationToken => inner.CancellationToken;
    public HttpRequestOptions? Http => http;
    public IDiagnosticLog DiagnosticLog => inner.DiagnosticLog;
}
