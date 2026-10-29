using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Resolves a host name through an RFC 8484 DNS-over-HTTPS server, as curl 8.21.0 does for
/// <c>--doh-url</c> (ADR-0152, BL-641): it POSTs an A query and an AAAA query in parallel, each on
/// a connection of its own, and returns the AAAA answer's addresses and then the A answer's. Under
/// <c>-4</c> or <c>-6</c> (<see cref="AddressFamily" />) it sends only that family's query (BL-939).
/// </summary>
/// <remarks>
/// <para>
/// Each POST is the one curl sends, byte for byte: <c>POST &lt;path&gt; HTTP/1.1</c>, <c>Host</c>
/// (with the port only when it is not the scheme's default), <c>Accept: */*</c>,
/// <c>Content-Type: application/dns-message</c>, <c>Content-Length</c>, and the query
/// <see cref="DnsQueryEncoder" /> writes; there is no <c>User-Agent</c>. The response is read by
/// <see cref="DohResponseReader" /> and decoded by <see cref="DnsAnswerDecoder" />.
/// </para>
/// <para>
/// A query that cannot connect, whose response is not read whole, or whose answer does not decode
/// to an address of its type yields none, and when neither yields any the resolver returns an
/// empty list, so <see cref="TcpConnector" /> fails the transfer with exit 6
/// <c>Could not resolve host: &lt;host&gt;</c>. An IP address literal and <c>localhost</c> never
/// reach the server, as measured: the literal is returned as it is, and <c>localhost</c> and
/// every name under <c>.localhost</c> as <c>::1</c> and <c>127.0.0.1</c>.
/// </para>
/// <para>
/// A query that failed has the error line curl 8.22.0 prints under <c>-v --trace-config doh</c> go
/// to the trace sink (<see cref="DohTraceLines" />, BL-2970) in place of its completed line; the
/// composition root passes a real sink only for that option.
/// </para>
/// </remarks>
public sealed partial class DohDnsResolver : IHttpsOriginDnsResolver, IDnsResolverWithFailureReason, IEchConfigListLookup
{
    private readonly IConnector _connector;
    private readonly Uri _dohUrl;
    private readonly ConnectTarget _dohServer;
    private readonly ITransferEvents _dohTrace;
    private readonly Func<CurlExitCode, string> _describeExitCode;

    // The DoH connections opened so far: curl numbers them after the transfer's own #0, so the
    // first is #1 (ADR-0380).
    private long _connectionCount;

    // The run's %{conn_id}s the DoH connections have taken: curl reuses an idle DoH connection, so a
    // query takes a new number only when more queries run at once than numbers were taken (CGR-286).
    private readonly object _connectionIdsLock = new();
    private int _connectionIdsTaken;

    /// <summary>Initializes a new instance of the <see cref="DohDnsResolver" /> class.</summary>
    /// <param name="connector">
    /// Opens each DoH connection: a <see cref="TcpConnector" /> of its own, built with the system
    /// resolver (the DoH server's own name is resolved as any host is) and a TLS provider built
    /// from the DoH TLS options only (ADR-0152).
    /// </param>
    /// <param name="dohUrl">The absolute <c>--doh-url</c>, whose scheme says whether TLS is used.</param>
    public DohDnsResolver(IConnector connector, Uri dohUrl)
        : this(connector, dohUrl, NoTransferEvents.Instance, static _ => string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DohDnsResolver" /> class that reports curl's
    /// <c>--trace-config doh</c> lines.
    /// </summary>
    /// <param name="connector">
    /// Opens each DoH connection: a <see cref="TcpConnector" /> of its own, built with the system
    /// resolver and a TLS provider built from the DoH TLS options only (ADR-0152).
    /// </param>
    /// <param name="dohUrl">The absolute <c>--doh-url</c>, whose scheme says whether TLS is used.</param>
    /// <param name="dohTrace">
    /// Receives the <c>--trace-config doh</c> lines as <see cref="ITransferEvents.ReportInfo" /> texts,
    /// e.g. <c>[DoH] [DoH] [A] error decoding response: No such name</c>.
    /// </param>
    /// <param name="describeExitCode">
    /// Gives curl's <c>curl_easy_strerror</c> text for the exit code a DoH connection or exchange
    /// failed with, the text of its <c>[DoH] [DoH] [A] error: &lt;text&gt;</c> line.
    /// </param>
    public DohDnsResolver(IConnector connector, Uri dohUrl, ITransferEvents dohTrace, Func<CurlExitCode, string> describeExitCode)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(dohUrl);
        ArgumentNullException.ThrowIfNull(dohTrace);
        ArgumentNullException.ThrowIfNull(describeExitCode);
        if (!dohUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("The DoH URL must be absolute.", nameof(dohUrl));
        }

        _connector = connector;
        _dohUrl = dohUrl;
        _dohTrace = dohTrace;
        _describeExitCode = describeExitCode;
        var isHttps = string.Equals(dohUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        _dohServer = new ConnectTarget(dohUrl.IdnHost, dohUrl.Port, isHttps) { PoolScheme = dohUrl.Scheme };
    }

    /// <summary>
    /// Gets the address family <c>-4</c> or <c>-6</c> limits the transfer to:
    /// <see cref="AddressFamily.InterNetwork" /> sends only the A query and
    /// <see cref="AddressFamily.InterNetworkV6" /> only the AAAA query, as curl 8.21.0 was measured to
    /// (BL-939); any other value, the default, sends both.
    /// </summary>
    public AddressFamily AddressFamily { get; init; } = AddressFamily.Unspecified;

    /// <summary>
    /// Gets the function that takes the run's next <c>%{conn_id}</c>, called once for each DoH connection
    /// curl opens (one per query running at once, reused by later queries) so the connection numbers
    /// <c>-v</c> prints for the transfer's own connections count them (CGR-286); <see langword="null" /> to take none.
    /// </summary>
    public Func<long>? TakeConnectionId { get; init; }

    // Takes a connection number for each of the queries running at once that no taken number covers.
    private void TakeConnectionIdsFor(int queriesAtOnce)
    {
        lock (_connectionIdsLock)
        {
            while (_connectionIdsTaken < queriesAtOnce)
            {
                _connectionIdsTaken++;
                TakeConnectionId?.Invoke();
            }
        }
    }

    /// <summary>
    /// Gets the events each DoH sub-transfer reports its own <c>-v</c> lines on, as curl 8.21.0
    /// writes them under <c>--trace-config dns</c>, <c>doh</c> or <c>all</c> (BL-1180): the
    /// connector's connect lines, <c>using HTTP/1.x</c>, the POST's head and body, <c>upload
    /// completely sent off</c>, the answer's head and body, <c>Connection #N to host H:P left
    /// intact</c> and <c>DoH request complete, K to go</c>. The composition root wraps them so
    /// each line gets curl 8.22.0's <c>[DoH] </c> prefix. When given, the queries run one after
    /// another, AAAA's whole before A's as curl 8.22.0 runs them, so the lines come in a fixed order (ADR-0380); the default,
    /// <see cref="NoTransferEvents.Instance" />, reports nothing and runs them in parallel.
    /// </summary>
    public ITransferEvents SubTransferEvents { get; init; } = NoTransferEvents.Instance;

    /// <summary>
    /// Gets the proxy the DoH requests go through, as curl 8.22.0's DoH sub-transfers take the
    /// proxy environment variables (CGR-037, ADR-0904): an <c>http://</c> DoH URL through an HTTP or
    /// HTTPS proxy is POSTed to the proxy in absolute form with <c>Proxy-Connection: Keep-Alive</c>;
    /// an <c>https://</c> one, or any through a SOCKS proxy, is tunnelled. <see langword="null" />,
    /// the default, connects to the DoH server directly.
    /// </summary>
    public ProxyEndpoint? Proxy { get; init; }

    // Whether each POST goes to an HTTP or HTTPS proxy in absolute form rather than through a tunnel.
    private bool ForwardsThroughProxy =>
        Proxy is { Kind: ProxyKind.Http or ProxyKind.Http10 or ProxyKind.Https } && !_dohServer.UseTls;

    // Where each DoH connection is opened: the DoH server, the proxy it is forwarded to, or the
    // DoH server through a tunnel.
    private ConnectTarget DohConnectTarget => Proxy switch
    {
        null => _dohServer,
        { } proxy when ForwardsThroughProxy =>
            new ConnectTarget(proxy.Host, proxy.Port, proxy.Kind == ProxyKind.Https) { IsForwardProxy = true, PoolScheme = _dohServer.PoolScheme },
        { } proxy => _dohServer with { Proxy = proxy },
    };

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        (await ResolveWithFailureReasonAsync(host, cancellationToken).ConfigureAwait(false)).Addresses;

    /// <summary>
    /// Resolves <paramref name="host" /> as <see cref="ResolveAsync" /> does, reporting a name that cannot be put in
    /// a query as <see cref="DnsLookupFailure.DohBadLabel" /> or <see cref="DnsLookupFailure.DohNameTooLong" />,
    /// before anything is sent, so the transfer fails with curl 8.22.0's exit 27 <c>Failed to encode DoH packet [N]</c>
    /// rather than exit 6 (CGR-158).
    /// </summary>
    /// <param name="host">The host name or literal address to resolve.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The addresses, or why there are none.</returns>
    public async ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal))
        {
            return new DnsResolution([literal], DnsLookupFailure.None);
        }

        if (TcpConnector.IsLocalhost(host))
        {
            return new DnsResolution([IPAddress.IPv6Loopback, IPAddress.Loopback], DnsLookupFailure.None);
        }

        var queryA = DnsQueryEncoder.Encode(host, DnsRecordType.A);
        if (queryA.Failure != DnsMessageFailure.None)
        {
            return new DnsResolution([], EncodingFailureOf(queryA.Failure));
        }

        var results = DohQueryResult.AsOneEntry(await RunAddressQueriesAsync(AddressQueries(host, queryA.Bytes), cancellationToken).ConfigureAwait(false));
        return new DnsResolution(ResolvedAddresses(results), DnsLookupFailure.None);
    }

    // The lookup failure for a query DnsQueryEncoder refused: a bad label or a name too long.
    private static DnsLookupFailure EncodingFailureOf(DnsMessageFailure failure) =>
        failure == DnsMessageFailure.NameTooLong ? DnsLookupFailure.DohNameTooLong : DnsLookupFailure.DohBadLabel;
}
