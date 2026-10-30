using System.Net.Sockets;
using Curl.Api.Options.Network;
using Curl.Api.Options.Tls;
using Curl.Core;
using Curl.Networking;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>
    /// Creates the run's resolver: the <see cref="CreateDohResolver" /> one when <c>--doh-url</c> is
    /// given, since curl asks the DoH server whichever resolver it was built with (BL-642); otherwise
    /// the hand-built <see cref="DnsServerResolver" /> when any of
    /// <c>--dns-servers</c>, <c>--dns-interface</c>, <c>--dns-ipv4-addr</c> and <c>--dns-ipv6-addr</c>
    /// is given, as curl's c-ares build resolves then (ADR-0170, BL-694), asking only the
    /// <c>-4</c> or <c>-6</c> family's records; otherwise the <see cref="SystemDnsResolver" />.
    /// </summary>
    /// <param name="network">The option group's network options, whose DNS settings and IP version pick the resolver.</param>
    /// <param name="tls">The option group's TLS options, whose DoH settings the DoH connector's handshakes apply.</param>
    /// <param name="timeProvider">The clock the hand-built resolver and the DoH connections time on.</param>
    /// <param name="tcpDialer">Opens the plaintext TCP connections to the DoH server.</param>
    /// <param name="dohTrace">
    /// Receives the DoH resolver's <c>--trace-config doh</c> lines (BL-1102); <see langword="null" />
    /// for none.
    /// </param>
    /// <param name="easyErrorText">Curl's easy error text for each exit code, which the DoH resolver's <c>--trace-config doh</c> lines carry (BL-1102): <c>CurlEasyErrorText.Of</c> (in <c>Libcurl/Misc/ErrorText</c>) passed in by <c>Curl.Console</c> in production; <see langword="null" /> for an empty text.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components, for the DoH connector's DNS filter lines; <see langword="null" /> for none.</param>
    /// <param name="takeDohConnectionId">Takes the run's next <c>%{conn_id}</c> for each DoH connection (CGR-286); <see langword="null" /> to take none.</param>
    /// <param name="allowsPlainHttpDohUrl">
    /// Whether a DoH URL that is not <c>https</c> is used, as curl's debug build allows; <see langword="false" />, as curl
    /// 8.22.0's release build, makes such a URL an <see cref="UnusableDohUrlResolver" /> (CGR-163, ADR-0859).
    /// </param>
    /// <returns>The resolver.</returns>
    internal static IDnsResolver CreateDnsResolver(EasyNetworkOptions network, EasyTlsOptions tls, TimeProvider timeProvider, ITcpDialer tcpDialer, ITransferEvents? dohTrace = null, Func<CurlExitCode, string>? easyErrorText = null, EngineTraceComponents? traces = null, bool allowsPlainHttpDohUrl = false, Func<long>? takeDohConnectionId = null)
    {
        if (network.DohUrl is { } dohUrl)
        {
            return !allowsPlainHttpDohUrl && !IsHttpsDohUrl(dohUrl)
                ? new UnusableDohUrlResolver()
                : CreateDohResolver(dohUrl, CreateDohConnector(traces ?? EngineTraceComponents.None, tls, tcpDialer, timeProvider, resolveOverrides: ResolveOverrides.Parse(network.Resolve)), AddressFamilyOf(network), dohTrace, easyErrorText, traces, takeConnectionId: takeDohConnectionId);
        }

        DnsServerResolverOptions resolverOptions = new(
            network.DnsServers,
            network.DnsInterface,
            network.DnsLocalIPv4,
            network.DnsLocalIPv6,
            AddressFamilyOf(network));
        return resolverOptions.IsAnyGiven
            ? new DnsServerResolver(resolverOptions, timeProvider)
            : new SystemDnsResolver();
    }

    /// <summary>
    /// The resolver the TCP and UDP connectors ask on a DNS cache miss: <paramref name="resolver" />, its addresses
    /// shuffled under <see cref="EasyNetworkOptions.DnsShuffleAddresses" /> (<c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c>,
    /// <see cref="ShufflingDnsResolver" />, BL-2922), then wrapped by <paramref name="wrapLookUps" />.
    /// </summary>
    /// <param name="resolver">The resolver the options pick.</param>
    /// <param name="network">The option group's network options.</param>
    /// <param name="wrapLookUps">Wraps the resolver the connectors look names up through.</param>
    /// <returns>The resolver the connectors look names up through.</returns>
    internal static IDnsResolver LookUpsOf(IDnsResolver resolver, EasyNetworkOptions network, Func<IDnsResolver, IDnsResolver> wrapLookUps) =>
        wrapLookUps(ShufflingDnsResolver.Over(resolver, network.DnsShuffleAddresses));

    /// <summary>
    /// Creates the resolver <c>--doh-url</c> asks for: a <see cref="DohDnsResolver" /> over
    /// <paramref name="connector" /> for the URL <see cref="DohUrlOf" /> makes of the value, or, when it
    /// makes none, an <see cref="UnusableDohUrlResolver" />, so every name but an IP literal or <c>localhost</c> fails
    /// to resolve with exit 6 as curl 8.22.0 fails <c>--doh-url bogus</c> and an <c>ftp://</c> DoH URL (measured, BL-642).
    /// </summary>
    /// <param name="dohUrl">The <c>--doh-url</c> value.</param>
    /// <param name="connector">Opens each DoH connection: <see cref="CreateDohConnector" />'s in production.</param>
    /// <param name="addressFamily">
    /// The <c>-4</c> or <c>-6</c> family (<see cref="AddressFamilyOf" />), whose query alone is sent (BL-939).
    /// </param>
    /// <param name="dohTrace">
    /// Receives the <c>--trace-config doh</c> lines, with <paramref name="easyErrorText" />'s texts
    /// (BL-1102), and each DoH sub-transfer's own lines through a <see cref="DohSubTransferEvents" />
    /// (BL-1180); <see langword="null" /> for none.
    /// </param>
    /// <param name="easyErrorText">Curl's easy error text for each exit code, which the DoH resolver's <c>--trace-config doh</c> lines carry (BL-1102): <c>CurlEasyErrorText.Of</c> (in <c>Libcurl/Misc/ErrorText</c>) passed in by <c>Curl.Console</c> in production; <see langword="null" /> for an empty text.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components, which gate the HTTPS query's trace lines (<see cref="HttpsRecordTraceOf" />); <see langword="null" /> to write them all.</param>
    /// <param name="readEnvironmentVariable">Reads the proxy environment variables the DoH requests go through (<see cref="DohProxyOf" />); <see langword="null" /> for the process's environment.</param>
    /// <param name="takeConnectionId">Takes the run's next <c>%{conn_id}</c> for each DoH connection (CGR-286); <see langword="null" /> to take none.</param>
    /// <returns>The resolver.</returns>
    internal static IDnsResolver CreateDohResolver(string dohUrl, IConnector connector, AddressFamily addressFamily, ITransferEvents? dohTrace = null, Func<CurlExitCode, string>? easyErrorText = null, EngineTraceComponents? traces = null, Func<string, string?>? readEnvironmentVariable = null, Func<long>? takeConnectionId = null) =>
        DohUrlOf(dohUrl) is { } url && DohProxyOf(url, readEnvironmentVariable ?? Environment.GetEnvironmentVariable, out ProxyEndpoint? proxy)
            ? NewDohResolver(connector, url, proxy, addressFamily, dohTrace, easyErrorText, traces, takeConnectionId)
            : new UnusableDohUrlResolver();

    // curl writes the [DNS] filter lines under the dns component and the sub-transfers' own lines under doh (BL-3814); no components given writes both.
    internal static DohSubTransferEvents DohSubTransferEventsOf(ITransferEvents dohTrace, EngineTraceComponents? traces)
    {
        var doh = traces is not null && TraceComponentIsOn(traces, "doh");
        var dns = traces is not null && TraceComponentIsOn(traces, "dns");
        return new DohSubTransferEvents(dohTrace, PlatformTlsBackend.ForProcess, writesDnsFilterLines: dns || !doh, writesSubTransferLines: doh || !dns);
    }

    private static bool TraceComponentIsOn(EngineTraceComponents traces, string component) =>
        traces.Components.Contains(component) || traces.Components.Contains("all") || traces.Components.Contains("network");

    // The DoH resolver CreateDohResolver makes of a usable DoH URL and the proxy chosen for it.
    private static DohDnsResolver NewDohResolver(IConnector connector, Uri url, ProxyEndpoint? proxy, AddressFamily addressFamily, ITransferEvents? dohTrace, Func<CurlExitCode, string>? easyErrorText, EngineTraceComponents? traces, Func<long>? takeConnectionId) =>
        new(connector, url, dohTrace ?? NoTransferEvents.Instance, easyErrorText ?? NoEasyErrorText)
        {
            AddressFamily = addressFamily,
            SubTransferEvents = dohTrace is null ? NoTransferEvents.Instance : DohSubTransferEventsOf(dohTrace, traces),
            HttpsRecordTrace = HttpsRecordTraceOf(traces),
            Proxy = proxy,
            TakeConnectionId = takeConnectionId,
        };

    /// <summary>
    /// The proxy curl 8.22.0's DoH sub-transfers go through (CGR-037, ADR-0904): the one the proxy
    /// environment variables (<c>http_proxy</c>, <c>https_proxy</c>, <c>ALL_PROXY</c>, <c>NO_PROXY</c>)
    /// name for the DoH URL, as <see cref="ProxySelector" /> reads them; <c>-x</c> and
    /// <c>--noproxy</c> never apply, as <c>lib/doh.c</c> copies neither to the sub-transfer.
    /// </summary>
    /// <param name="dohUrl">The DoH URL.</param>
    /// <param name="readEnvironmentVariable">Reads one environment variable.</param>
    /// <param name="proxy">The proxy; <see langword="null" /> for a direct connection or unusable proxy text.</param>
    /// <returns><see langword="false" /> when the proxy text is unusable, which fails every DoH request, so every name fails to resolve.</returns>
    internal static bool DohProxyOf(Uri dohUrl, Func<string, string?> readEnvironmentVariable, out ProxyEndpoint? proxy) =>
        new ProxySelector(readEnvironmentVariable).TrySelect(CurlUrl.Parse(dohUrl.AbsoluteUri), null, null, out proxy, out _);

    /// <summary>The easy error text of a run given none: empty for every exit code.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>An empty text.</returns>
    private static string NoEasyErrorText(CurlExitCode exitCode) => string.Empty;

    /// <summary>
    /// Which HTTPS-RR DoH trace lines curl 8.22.0 writes for the run's <c>--trace-config</c>
    /// (ADR-0727's BL-2979 amendment): its <c>[DoH]</c> lines under the <c>doh</c> feature, and its
    /// <c>[DNS] [HTTPS]</c> lines under <c>doh</c> and <c>dns</c> both; <c>network</c> and <c>all</c>
    /// turn on both features (<c>lib/curl_trc.c</c>'s <c>trc_feats</c>).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns>The gate; <see cref="DohHttpsRecordTrace.All" /> for no components given.</returns>
    internal static DohHttpsRecordTrace HttpsRecordTraceOf(EngineTraceComponents? traces)
    {
        if (traces is null)
        {
            return DohHttpsRecordTrace.All;
        }

        var both = traces.Components.Contains("network") || traces.Components.Contains("all");
        var doh = both || traces.Components.Contains("doh");
        return new DohHttpsRecordTrace(doh, doh && (both || traces.Components.Contains("dns")));
    }

    /// <summary>
    /// The DoH URL curl makes of a <c>--doh-url</c> value: the value as it is when it names a scheme,
    /// and with <c>https://</c> in front when it does not, as curl 8.22.0's DoH sub-transfer assumes
    /// HTTPS (measured: a scheme-less DoH URL's server gets a TLS ClientHello; CGR-038, ADR-0961).
    /// </summary>
    /// <param name="dohUrl">The <c>--doh-url</c> value.</param>
    /// <returns>
    /// The absolute <c>http</c> or <c>https</c> URL, or <see langword="null" /> when the value does not
    /// parse as one.
    /// </returns>
    internal static Uri? DohUrlOf(string dohUrl)
    {
        // curl's URL parser refuses a space or control character, so the lookup fails with exit 6 (CGR-283).
        if (HasSpaceOrControlCharacter(dohUrl))
        {
            return null;
        }

        string withScheme = dohUrl.Contains("://", StringComparison.Ordinal) ? dohUrl : "https://" + dohUrl;
        return Uri.TryCreate(withScheme, UriKind.Absolute, out Uri? url)
            && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? url
            : null;
    }

    // Whether the --doh-url value holds a space, a TAB or another control character, which curl's URL parser refuses.
    private static bool HasSpaceOrControlCharacter(string dohUrl) =>
        dohUrl.Any(character => character <= ' ' || character == '\u007f');

    /// <summary>
    /// Whether <see cref="DohUrlOf" /> makes an <c>https</c> URL of a <c>--doh-url</c> value, the only DoH URL curl
    /// 8.22.0's release build uses: its DoH sub-transfer allows the <c>https</c> protocol alone, so an <c>http</c> one
    /// fails every name with exit 6 and never reaches the server (measured, CGR-163, ADR-0859), while a scheme-less one
    /// is <c>https</c> (CGR-038, ADR-0961).
    /// </summary>
    /// <param name="dohUrl">The <c>--doh-url</c> value.</param>
    /// <returns><see langword="true" /> for an <c>https</c> DoH URL.</returns>
    internal static bool IsHttpsDohUrl(string dohUrl) =>
        DohUrlOf(dohUrl)?.Scheme == Uri.UriSchemeHttps;

    /// <summary>
    /// Creates the connector the DoH queries are sent through (ADR-0152): a <see cref="TcpConnector" />
    /// of its own over the <see cref="SystemDnsResolver" /> (the DoH server's own name is resolved as any
    /// host is), <paramref name="tcpDialer" />, and a TLS provider routed as
    /// <see cref="CreateTlsProvider" /> routes the options <see cref="TlsClientOptionsMapping.DohOf" />
    /// maps. The <c>--resolve</c> entries answer for the DoH server's name, as curl 8.22.0's DoH
    /// probes look it up in the DNS cache those entries were added to (CGR-155, ADR-0852); they are
    /// loaded into the connector's cache when it is made, so no DoH sub-transfer writes their
    /// <c>Added ... to DNS cache</c> lines. No <c>--connect-to</c>, <c>-x</c> or <c>-4</c>/<c>-6</c>
    /// applies to it; the proxy environment variables do, through <see cref="DohProxyOf" /> (CGR-037).
    /// Under <see cref="TracesDns" /> it writes the DNS filter's lines for each DoH sub-transfer too,
    /// as curl 8.21.0 does (BL-1180).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components, for the DNS filter lines.</param>
    /// <param name="tls">The option group's TLS options.</param>
    /// <param name="tcpDialer">Opens each plaintext connection.</param>
    /// <param name="timeProvider">The clock the connector and its TLS provider time on.</param>
    /// <param name="hostResolver">Resolves the DoH server's own name; <see langword="null" /> for the <see cref="SystemDnsResolver" />. The conformance harness passes its in-memory resolver (BL-2919).</param>
    /// <param name="resolveOverrides">The option group's <c>--resolve</c> entries; <see langword="null" /> for none.</param>
    /// <returns>The connector.</returns>
    internal static TcpConnector CreateDohConnector(EngineTraceComponents traces, EasyTlsOptions tls, ITcpDialer tcpDialer, TimeProvider timeProvider, IDnsResolver? hostResolver = null, ResolveOverrides? resolveOverrides = null)
    {
        TcpConnector connector = new(
            hostResolver ?? new SystemDnsResolver(),
            tcpDialer,
            CreateTlsProvider(TlsClientOptionsMapping.DohOf(tls), timeProvider),
            timeProvider,
            DohProxyTunnelOptions,
            resolveOverrides)
        {
            TracesDnsFilter = TracesDns(traces),
        };
        connector.LoadResolveEntries(NoTransferEvents.Instance);
        return connector;
    }

    /// <summary>
    /// The CONNECT a DoH sub-transfer sends through a proxy from the environment: no
    /// <c>User-Agent</c>, as curl 8.22.0's carries none (measured, CGR-037).
    /// </summary>
    internal static HttpProxyTunnelOptions DohProxyTunnelOptions { get; } = new(null, System.Text.Encoding.UTF8);

    /// <summary>
    /// The resolver the transports use under the harness: <paramref name="dialing" />'s, or, when <c>--doh-url</c> is given,
    /// the <see cref="CreateDohResolver" /> one whose connections dial through <paramref name="dialing" />'s dialer and
    /// whose server name resolves through its resolver, so the DoH POSTs reach the in-memory servers (ADR-0658, BL-2919).
    /// </summary>
    /// <param name="dialing">What the harness's engine dials through.</param>
    /// <param name="network">The option group's network options, whose DoH URL and IP version pick the resolver.</param>
    /// <param name="tls">The option group's TLS options, whose DoH settings the DoH connector's handshakes apply.</param>
    /// <param name="timeProvider">The clock the DoH connections time on.</param>
    /// <param name="dohTrace">Receives the DoH resolver's <c>--trace-config doh</c> lines; <see langword="null" /> for none.</param>
    /// <param name="easyErrorText">Curl's easy error text for each exit code; <see langword="null" /> for an empty text.</param>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns>The resolver.</returns>
    private static IDnsResolver DialingResolverOf(EngineDialing dialing, EasyNetworkOptions network, EasyTlsOptions tls, TimeProvider timeProvider, ITransferEvents? dohTrace, Func<CurlExitCode, string>? easyErrorText, EngineTraceComponents traces) =>
        network.DohUrl is { } dohUrl
            ? CreateDohResolver(dohUrl, CreateDohConnector(traces, tls, dialing.TcpDialer, timeProvider, dialing.DnsResolver, ResolveOverrides.Parse(network.Resolve)), AddressFamilyOf(network), dohTrace, easyErrorText, traces)
            : dialing.DnsResolver;

    /// <summary>The variable curl's debug build reads the DNS server to ask from (<c>override-dns</c>).</summary>
    internal const string DnsServerVariableName = "CURL_DNS_SERVER";

    /// <summary>
    /// The resolver an option group of the dialing runner uses: with a non-empty
    /// <paramref name="dnsServer" />, a <see cref="DnsServerResolver" /> asking that server
    /// (<c>ip:port</c>, parsed as <c>--dns-servers</c> is) for the <c>-4</c> or <c>-6</c> family's
    /// records through <paramref name="datagramConnector" />, as curl's debug build honours
    /// <c>CURL_DNS_SERVER</c> (ADR-0492, BL-2248); otherwise <paramref name="dnsResolver" />.
    /// </summary>
    /// <param name="network">The option group's network options, whose IP version the queries ask for.</param>
    /// <param name="dnsServer">The <c>CURL_DNS_SERVER</c> value, or <see langword="null" /> when not honoured or not set.</param>
    /// <param name="dnsResolver">The resolver the runner was given.</param>
    /// <param name="datagramConnector">Carries the queries.</param>
    /// <returns>The resolver.</returns>
    internal static IDnsResolver DnsServerVariableResolverOf(EasyNetworkOptions network, string? dnsServer, IDnsResolver dnsResolver, IDatagramConnector datagramConnector) =>
        string.IsNullOrEmpty(dnsServer)
            ? dnsResolver
            : new DnsServerResolver(new DnsServerResolverOptions(dnsServer, null, null, null, AddressFamilyOf(network)), TimeProvider.System, datagramConnector);
}
