using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Extension.Coap.Observe;

/// <summary>
/// A <c>coap://</c> transfer <c>--coap-observe</c> asks to observe (ADR-1064): the transfer's own context, which the
/// CoAP handler recognises by its type and serves by registering an observation (RFC 7641 section 3.1).
/// </summary>
/// <param name="inner">The transfer's own context.</param>
internal sealed class CoapObserveContext(ITransferContext inner) : ITransferContext
{
    public Stream Output => inner.Output;
    public HttpRequestOptions? Http => inner.Http;
    public CurlUrl Url => inner.Url;
    public Stream? Upload => inner.Upload;
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
