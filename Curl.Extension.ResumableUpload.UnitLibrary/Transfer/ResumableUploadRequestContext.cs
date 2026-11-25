using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// The context one resumable upload request gives the rest of curl's transfer: the transfer's own, but sent to another
/// URL with the flow's method, header fields and body, its response written to buffers the flow reads, and none of the
/// transfer's own body, <c>-C</c>, <c>-r</c> or <c>-D</c>.
/// </summary>
/// <param name="inner">The transfer's own context.</param>
/// <param name="url">The URL the request goes to.</param>
/// <param name="http">The HTTP options with the request's method and header fields.</param>
/// <param name="upload">The request body, or <see langword="null" /> for none.</param>
/// <param name="noBody">Whether the request is a <c>HEAD</c>, as <c>-I</c> sends it.</param>
/// <param name="output">Where the response body goes.</param>
/// <param name="headerOutput">Where the response head goes.</param>
/// <param name="credentials">The credentials to send, or <see langword="null" /> for none.</param>
internal sealed class ResumableUploadRequestContext(
    ITransferContext inner, CurlUrl url, HttpRequestOptions http, Stream? upload, bool noBody, Stream output, Stream headerOutput, System.Net.NetworkCredential? credentials) : ITransferContext
{
    public CurlUrl Url => url;
    public Stream Output => output;
    public Stream? Upload => upload;
    public long? ResumeFrom => null;
    public bool ResumeUploadFromUnknownOffset => false;
    public ByteRange? Range => null;
    public string? RangeText => null;
    public long? MaxFileSize => inner.MaxFileSize;
    public bool NoBody => noBody;
    public TimeCondition? TimeCondition => inner.TimeCondition;
    public bool RemoteTime => inner.RemoteTime;
    public Stream? HeaderOutput => headerOutput;
    public Stream? DumpHeaderOutput => null;
    public ReadOnlyMemory<byte>? PostData => null;
    public bool PostWithoutData => false;
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
    public string? NoProxy => inner.NoProxy;
    public MailRequestOptions? Mail => inner.Mail;
    public SshOptions? Ssh => inner.Ssh;
    public TimeProvider TimeProvider => inner.TimeProvider;
    public ITransferEvents Events => inner.Events;
    public ITransferProgress Progress => inner.Progress;
    public CancellationToken CancellationToken => inner.CancellationToken;
    public HttpRequestOptions? Http => http;
    public IDiagnosticLog DiagnosticLog => inner.DiagnosticLog;
}
