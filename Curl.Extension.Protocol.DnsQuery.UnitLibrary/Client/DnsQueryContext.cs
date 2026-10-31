using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Extension.Protocol.DnsQuery.Client;

/// <summary>
/// A <c>dns://</c> transfer given <c>--dns-transport</c> or <c>--dns-json</c> (ADR-1140): the transfer's own context
/// with the transport and output format those options chose, which the DNS query handler reads.
/// </summary>
/// <param name="inner">The transfer's own context.</param>
/// <param name="transport">The transport's name, as <c>--dns-transport</c> gave it.</param>
/// <param name="json">Whether <c>--dns-json</c> asks for JSON in place of text.</param>
internal sealed class DnsQueryContext(ITransferContext inner, string transport, bool json) : ITransferContext
{
    /// <summary>Gets the transport's name, as <c>--dns-transport</c> gave it.</summary>
    public string Transport => transport;

    /// <summary>Gets a value indicating whether <c>--dns-json</c> asks for JSON in place of text.</summary>
    public bool Json => json;


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
