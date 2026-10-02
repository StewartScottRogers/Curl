using System.Net;
using System.Net.Sockets;
using System.Text;

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
/// Once both queries have finished, the lines curl prints under <c>-v --trace-config doh</c> go to
/// the trace sink (<see cref="DohTraceLines" />, BL-850); the composition root passes a real sink
/// only for that option.
/// </para>
/// </remarks>
public sealed class DohDnsResolver : IDnsResolver, IEchConfigListLookup
{
    private readonly IConnector _connector;
    private readonly Uri _dohUrl;
    private readonly ConnectTarget _dohServer;
    private readonly ITransferEvents _dohTrace;
    private readonly Func<CurlExitCode, string> _describeExitCode;

    // The DoH connections opened so far: curl numbers them after the transfer's own #0, so the
    // first is #1 (ADR-0380).
    private long _connectionCount;

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
    /// e.g. <c>[DNS] DoH: Too small type A for example.test</c>.
    /// </param>
    /// <param name="describeExitCode">
    /// Gives curl's <c>curl_easy_strerror</c> text for the exit code a DoH connection or exchange
    /// failed with, the text of its <c>[DNS] DoH request &lt;text&gt;</c> line.
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
    /// Gets the events each DoH sub-transfer reports its own <c>-v</c> lines on, as curl 8.21.0
    /// writes them under <c>--trace-config dns</c>, <c>doh</c> or <c>all</c> (BL-1180): the
    /// connector's connect lines, <c>using HTTP/1.x</c>, the POST's head and body, <c>upload
    /// completely sent off</c>, the answer's head and body, <c>Connection #N to host H:P left
    /// intact</c> and <c>a DoH request is completed, K to go</c>. The composition root wraps them so
    /// each line gets curl's <c>[DNS] </c> prefix. When given, the queries run one after another, A's
    /// whole before AAAA's, so the lines come in a fixed order (ADR-0380); the default,
    /// <see cref="NoTransferEvents.Instance" />, reports nothing and runs them in parallel.
    /// </summary>
    public ITransferEvents SubTransferEvents { get; init; } = NoTransferEvents.Instance;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal))
        {
            return [literal];
        }

        if (TcpConnector.IsLocalhost(host))
        {
            return [IPAddress.IPv6Loopback, IPAddress.Loopback];
        }

        var queryA = DnsQueryEncoder.Encode(host, DnsRecordType.A);
        if (queryA.Failure != DnsMessageFailure.None)
        {
            return [];
        }

        var results = DohQueryResult.AsOneEntry(await RunAddressQueriesAsync(AddressQueries(host, queryA.Bytes), cancellationToken).ConfigureAwait(false));
        DohTraceLines.Report(_dohTrace, _describeExitCode, host, results);
        return ResolvedAddresses(results);
    }

    // Untraced, the queries run in parallel as curl runs them; traced, one after another, so each
    // sub-transfer's lines come whole, A's first (ADR-0380).
    private async Task<DohQueryResult[]> RunAddressQueriesAsync(List<(byte[] Query, DnsRecordType Type)> queries, CancellationToken cancellationToken)
    {
        if (ReferenceEquals(SubTransferEvents, NoTransferEvents.Instance))
        {
            return await Task.WhenAll(queries.Select(query => QueryAsync(query.Query, query.Type, cancellationToken))).ConfigureAwait(false);
        }

        var results = new DohQueryResult[queries.Count];
        for (var index = 0; index < queries.Count; index++)
        {
            results[index] = await QueryAsync(queries[index].Query, queries[index].Type, cancellationToken).ConfigureAwait(false);
            SubTransferEvents.ReportInfo($"a DoH request is completed, {queries.Count - index - 1} to go");
        }

        return results;
    }

    // curl resolves from its one entry only when a query counts as answered, and then every
    // address in it counts, a failed decode's too (measured, BL-958); IPv6 first.
    private static IPAddress[] ResolvedAddresses(DohQueryResult[] results) =>
        results.Any(result => result.CountsAsAnswered)
            ? [.. Enumerable.Reverse(results).SelectMany(result => result.Addresses)]
            : [];

    // The A query and the AAAA query, A first, leaving out the one -4 or -6 rules out.
    private List<(byte[] Query, DnsRecordType Type)> AddressQueries(string host, byte[] queryA)
    {
        List<(byte[] Query, DnsRecordType Type)> queries = [];
        if (AddressFamily != AddressFamily.InterNetworkV6)
        {
            queries.Add((queryA, DnsRecordType.A));
        }

        if (AddressFamily != AddressFamily.InterNetwork)
        {
            queries.Add((DnsQueryEncoder.Encode(host, DnsRecordType.Aaaa).Bytes, DnsRecordType.Aaaa));
        }

        return queries;
    }

    /// <summary>
    /// Fetches <paramref name="host" />'s HTTPS record (RFC 9460) through the DoH server, the
    /// query curl 8.21.0 adds to the A and AAAA ones under <c>--ech true</c> or <c>hard</c> to find
    /// the host's ECHConfigList (ADR-0312, BL-707). The name asked for is the host itself on port
    /// 443 and <c>_&lt;port&gt;._https.&lt;host&gt;</c> on any other port (RFC 9460 section 9.1), and
    /// the first HTTPS record of the answer is decoded, as curl decodes only the first.
    /// </summary>
    /// <param name="host">The host name, already converted to its ASCII form.</param>
    /// <param name="port">The port the transfer connects to.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>
    /// The record, its <see cref="ServiceBindingRecord.EchConfigList" /> among its parameters; or
    /// <see langword="null" /> for an IP address literal or <c>localhost</c>, which are never asked
    /// for, and when the query fails, the answer holds no HTTPS record or its first one does not decode.
    /// </returns>
    public async ValueTask<ServiceBindingRecord?> ResolveHttpsRecordAsync(string host, int port, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (HttpsQueryFor(host, port) is not { } query)
        {
            return null;
        }

        var result = await QueryAsync(query, DnsRecordType.Https, cancellationToken).ConfigureAwait(false);
        return result.Answer is { Failure: DnsMessageFailure.None, HttpsRecordData: [var first, ..] }
            ? ServiceBindingRecordDecoder.Decode(first).Record
            : null;
    }

    /// <summary>
    /// Finds <paramref name="host" />'s ECHConfigList for <c>--ech true</c> or <c>hard</c>: the
    /// <c>ech</c> parameter of the HTTPS record <see cref="ResolveHttpsRecordAsync" /> fetches (ADR-0327).
    /// </summary>
    /// <param name="host">The host the transfer connects to.</param>
    /// <param name="port">The port it connects to.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The list's bytes, or <see langword="null" /> when there is no record or it has no <c>ech</c>.</returns>
    public async ValueTask<byte[]?> FindEchConfigListAsync(string host, int port, CancellationToken cancellationToken) =>
        await ResolveHttpsRecordAsync(host, port, cancellationToken).ConfigureAwait(false) is { EchConfigList.IsEmpty: false } record
            ? record.EchConfigList.ToArray()
            : null;

    /// <summary>The HTTPS query for <paramref name="host" />, or <see langword="null" /> for a literal, <c>localhost</c> or a name that does not encode.</summary>
    private static byte[]? HttpsQueryFor(string host, int port)
    {
        if (IPAddress.TryParse(host.Trim('[', ']'), out _) || TcpConnector.IsLocalhost(host))
        {
            return null;
        }

        var query = DnsQueryEncoder.Encode(HttpsQueryName(host, port), DnsRecordType.Https);
        return query.Failure == DnsMessageFailure.None ? query.Bytes : null;
    }

    /// <summary>The name curl asks the HTTPS record of: the host on port 443, <c>_&lt;port&gt;._https.&lt;host&gt;</c> on any other.</summary>
    /// <param name="host">The host name.</param>
    /// <param name="port">The port the transfer connects to.</param>
    /// <returns>The query name.</returns>
    internal static string HttpsQueryName(string host, int port) =>
        port == 443 ? host : $"_{port}._https.{host}";

    // The request line and header block of the POST curl sends for one query.
    private byte[] BuildRequestHead(byte[] query)
    {
        var host = _dohUrl.IsDefaultPort ? _dohUrl.Host : $"{_dohUrl.Host}:{_dohUrl.Port}";
        var head = $"POST {_dohUrl.PathAndQuery} HTTP/1.1\r\n"
            + $"Host: {host}\r\n"
            + "Accept: */*\r\n"
            + "Content-Type: application/dns-message\r\n"
            + $"Content-Length: {query.Length}\r\n"
            + "\r\n";
        return Encoding.Latin1.GetBytes(head);
    }

    private async Task<DohQueryResult> QueryAsync(byte[] query, DnsRecordType recordType, CancellationToken cancellationToken)
    {
        var events = SubTransferEvents;
        var connectionNumber = Interlocked.Increment(ref _connectionCount);
        var connected = await _connector.ConnectAsync(_dohServer with { Events = events }, cancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return new DohQueryResult(recordType, connected.ExitCode, null);
        }

        await using (connection.ConfigureAwait(false))
        {
            events.ReportInfo("using HTTP/1.x");
            var body = await ExchangeAsync(connection, BuildRequestHead(query), query, events, cancellationToken).ConfigureAwait(false);
            if (body is null)
            {
                return new DohQueryResult(recordType, CurlExitCode.RecvError, null);
            }

            events.ReportInfo(FormattableString.Invariant($"Connection #{connectionNumber} to host {_dohServer.Host}:{_dohServer.Port} left intact"));
            return new DohQueryResult(recordType, CurlExitCode.Ok, DnsAnswerDecoder.Decode(body, recordType));
        }
    }

    // Writes the POST and reads the answer's body; null when the connection fails mid-exchange or
    // the response is not read whole, which curl reports as "Failure when receiving data from the peer".
    private static async ValueTask<byte[]?> ExchangeAsync(IConnection connection, byte[] head, byte[] query, ITransferEvents events, CancellationToken cancellationToken)
    {
        try
        {
            byte[] request = [.. head, .. query];
            await connection.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            events.ReportRequestHeader(head);
            events.ReportDataSent(query);
            events.ReportInfo($"upload completely sent off: {query.Length} bytes");
            return await DohResponseReader.ReadBodyAsync(connection, events, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
