using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;
using Curl.Core;
using Curl.Networking;

namespace Curl.Engine;

/// <summary>
/// The managed <see cref="ICurlEngine" /> (ADR-0506, ADR-0541): each easy handle's transfer runs on
/// the protocol handlers and transports <see cref="EngineComposition" /> builds from that handle's
/// <see cref="EasyOptions" />. Every cache - connections, TLS sessions, DNS - belongs to the engine
/// object, and none is static, so two engines in one process share nothing.
/// The multi, share, URL and MIME handles are phase 3 work (ADR-0506) and throw
/// <see cref="NotSupportedException" /> here.
/// </summary>
public sealed class ManagedCurlEngine : ICurlEngine
{
    private readonly ConnectionCache connections;
    private readonly TlsSessionCache tlsSessions;
    private readonly DnsCache dnsCache = new();
    private readonly Func<EasyOptions, ProtocolDispatcher> createDispatcher;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates the engine on the system clock and the real network.</summary>
    public ManagedCurlEngine()
        : this(TimeProvider.System, null)
    {
    }

    /// <summary>
    /// Creates the engine on <paramref name="timeProvider" />, building each transfer's dispatcher
    /// with <paramref name="createDispatcher" />, or <see cref="CreateDispatcher" />'s production one.
    /// </summary>
    /// <param name="timeProvider">The clock the engine's caches and transfers run on.</param>
    /// <param name="createDispatcher">Builds a transfer's dispatcher from its options; <see langword="null" /> for the production one.</param>
    internal ManagedCurlEngine(TimeProvider timeProvider, Func<EasyOptions, ProtocolDispatcher>? createDispatcher)
    {
        this.timeProvider = timeProvider;
        connections = new ConnectionCache(timeProvider);
        tlsSessions = new TlsSessionCache(timeProvider);
        this.createDispatcher = createDispatcher ?? CreateDispatcher;
    }

    /// <inheritdoc />
    public CurlVersionInfo VersionInfo { get; } = new()
    {
        Protocols =
        [
            "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap", "imaps", "ldap", "ldaps",
            "mqtt", "mqtts", "pop3", "pop3s", "rtsp", "scp", "sftp", "smb", "smbs", "smtp", "smtps", "telnet", "tftp",
            "ws", "wss",
        ],
    };

    /// <inheritdoc />
    public IEasyTransfer CreateEasy() => new ManagedEasyTransfer(createDispatcher, timeProvider);

    /// <summary>Not supported yet: the multi handle is phase 3's multi interface work (ADR-0506).</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IMultiTransfer CreateMulti() => throw new NotSupportedException("The multi interface is phase 3 work (ADR-0506).");

    /// <summary>Not supported yet: the share handle is phase 3's share interface work (ADR-0506).</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IShareHandle CreateShare() => throw new NotSupportedException("The share interface is phase 3 work (ADR-0506).");

    /// <summary>Not supported yet: the URL handle is phase 3's URL API work (ADR-0506).</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException("The URL API is phase 3 work (ADR-0506).");

    /// <summary>Not supported yet: the MIME handle is phase 3's MIME API work (ADR-0506).</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IMimeBuilder CreateMime() => throw new NotSupportedException("The MIME API is phase 3 work (ADR-0506).");

    /// <summary>Not supported yet: the form API is phase 3's MIME API work (ADR-0506).</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ICurlFormBuilder CreateForm() => throw new NotSupportedException("The form API is phase 3 work (ADR-0506).");

    /// <summary>Closes every connection the engine's transfers kept open (<c>curl_global_cleanup</c>).</summary>
    /// <returns>A task that completes once they are closed.</returns>
    public ValueTask DisposeAsync() => connections.DisposeAsync();

    /// <summary>
    /// Builds a transfer's dispatcher as <c>Curl.Console</c>'s composition builds an option group's:
    /// <c>EngineComposition.CreateTransports</c>
    /// over the engine's caches, and the handlers over its pooling connector.
    /// </summary>
    /// <param name="options">The transfer's options.</param>
    /// <returns>The dispatcher.</returns>
    internal ProtocolDispatcher CreateDispatcher(EasyOptions options)
    {
        EngineToolOptions tool = new(Parallel: false, ParallelImmediate: false, SslSessionsFile: null, MaxTime: options.Transfer.Timeout > TimeSpan.Zero ? options.Transfer.Timeout : null);
        CurlTransports transports = EngineComposition.CreateTransports(tool, options.Network, options.Tls, options.Http, options.Proxy, options.Authentication, timeProvider, connections, tlsSessions, runDnsCache: dnsCache);
        return new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            transports.PoolingConnector,
            transports.UdpDatagramConnector,
            transports.TlsProvider,
            transports.DnsResolver,
            proxyAuthSchemes: transports.ProxyTunnelOptions.ProxyAuthSchemes,
            negotiateOptions: NegotiateOptionsMapping.Of(options.Authentication),
            ftpDataConnector: EngineComposition.FtpDataConnectorOf(transports)));
    }
}
