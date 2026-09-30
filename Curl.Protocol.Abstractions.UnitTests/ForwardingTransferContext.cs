namespace Curl.Protocol.Abstractions;

/// <summary>
/// An <see cref="ITransferContext" /> that forwards every member to an inner context except
/// <see cref="ITransferContext.DiagnosticLog" />, which it leaves to the interface's
/// default, as a library's wrapper does until its instrumentation task forwards the log.
/// </summary>
internal sealed class ForwardingTransferContext(ITransferContext inner) : ITransferContext
{
    public CurlUrl Url => inner.Url;
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
    public ReadOnlyMemory<byte>? PostData => inner.PostData;
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
    public HttpRequestOptions? Http => inner.Http;
    public MailRequestOptions? Mail => inner.Mail;
    public SshOptions? Ssh => inner.Ssh;
    public TimeProvider TimeProvider => inner.TimeProvider;
    public ITransferEvents Events => inner.Events;
    public ITransferProgress Progress => inner.Progress;
    public CancellationToken CancellationToken => inner.CancellationToken;
}
