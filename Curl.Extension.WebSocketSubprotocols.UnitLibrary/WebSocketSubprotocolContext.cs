namespace Curl.Extension.WebSocketSubprotocols;

/// <summary>
/// The context <see cref="WebSocketSubprotocolTransferHook" /> gives curl's ws handler: the transfer's own, but with
/// the subprotocol's opening messages as its upload and <c>Sec-WebSocket-Protocol</c> after its own request headers.
/// </summary>
/// <param name="inner">The transfer's own context.</param>
/// <param name="upload">The upload that gives the opening messages, one text frame each.</param>
/// <param name="http">The HTTP options with the <c>Sec-WebSocket-Protocol</c> header added.</param>
internal sealed class WebSocketSubprotocolContext(ITransferContext inner, Stream upload, HttpRequestOptions http) : ITransferContext
{
    public Stream Output => inner.Output;
    public HttpRequestOptions? Http => http;
    public CurlUrl Url => inner.Url;
    public Stream? Upload => upload;
    public long? ResumeFrom => inner.ResumeFrom;
    public bool ResumeUploadFromUnknownOffset => inner.ResumeUploadFromUnknownOffset;
    public ByteRange? Range => inner.Range;
    public string? RangeText => inner.RangeText;
    public long? MaxFileSize => inner.MaxFileSize;
    public bool NoBody => inner.NoBody;
    public TimeCondition? TimeCondition => inner.TimeCondition;
    public bool RemoteTime => inner.RemoteTime;
    public ReadOnlyMemory<byte>? PostData => inner.PostData;
    public bool PostWithoutData => inner.PostWithoutData;
    public System.Net.NetworkCredential? Credentials => inner.Credentials;
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
    public IDiagnosticLog DiagnosticLog => inner.DiagnosticLog;
    public Stream? HeaderOutput => inner.HeaderOutput;
    public Stream? DumpHeaderOutput => inner.DumpHeaderOutput;
}
