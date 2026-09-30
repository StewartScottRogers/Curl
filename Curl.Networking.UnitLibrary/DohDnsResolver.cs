using System.Net;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Resolves a host name through an RFC 8484 DNS-over-HTTPS server, as curl 8.21.0 does for
/// <c>--doh-url</c> (ADR-0152, BL-641): it POSTs an A query and an AAAA query in parallel, each on
/// a connection of its own, and returns the AAAA answer's addresses and then the A answer's.
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
public sealed class DohDnsResolver : IDnsResolver
{
    private readonly IConnector _connector;
    private readonly Uri _dohUrl;
    private readonly ConnectTarget _dohServer;
    private readonly ITransferEvents _dohTrace;
    private readonly Func<CurlExitCode, string> _describeExitCode;

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

        var ipv4 = QueryAsync(queryA.Bytes, DnsRecordType.A, cancellationToken);
        var ipv6 = QueryAsync(DnsQueryEncoder.Encode(host, DnsRecordType.Aaaa).Bytes, DnsRecordType.Aaaa, cancellationToken);
        DohQueryResult[] results = await Task.WhenAll(ipv4, ipv6).ConfigureAwait(false);
        DohTraceLines.Report(_dohTrace, _describeExitCode, host, results);
        return [.. results[1].Addresses, .. results[0].Addresses];
    }

    /// <summary>Builds the POST curl sends for one DNS query.</summary>
    /// <param name="query">The DNS query message.</param>
    /// <returns>The request line, header block and body.</returns>
    internal byte[] BuildRequest(byte[] query)
    {
        var host = _dohUrl.IsDefaultPort ? _dohUrl.Host : $"{_dohUrl.Host}:{_dohUrl.Port}";
        var head = $"POST {_dohUrl.PathAndQuery} HTTP/1.1\r\n"
            + $"Host: {host}\r\n"
            + "Accept: */*\r\n"
            + "Content-Type: application/dns-message\r\n"
            + $"Content-Length: {query.Length}\r\n"
            + "\r\n";
        return [.. Encoding.Latin1.GetBytes(head), .. query];
    }

    private async Task<DohQueryResult> QueryAsync(byte[] query, DnsRecordType recordType, CancellationToken cancellationToken)
    {
        var connected = await _connector.ConnectAsync(_dohServer, cancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return new DohQueryResult(recordType, connected.ExitCode, null);
        }

        await using (connection.ConfigureAwait(false))
        {
            var body = await ExchangeAsync(connection, BuildRequest(query), cancellationToken).ConfigureAwait(false);
            return body is null
                ? new DohQueryResult(recordType, CurlExitCode.RecvError, null)
                : new DohQueryResult(recordType, CurlExitCode.Ok, DnsAnswerDecoder.Decode(body, recordType));
        }
    }

    // Writes the POST and reads the answer's body; null when the connection fails mid-exchange or
    // the response is not read whole, which curl reports as "Failure when receiving data from the peer".
    private static async ValueTask<byte[]?> ExchangeAsync(IConnection connection, byte[] request, CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return await DohResponseReader.ReadBodyAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
