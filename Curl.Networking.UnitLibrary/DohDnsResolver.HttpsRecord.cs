using System.Collections.Concurrent;
using System.Net;

namespace Curl.Networking;

// The HTTPS (type 65) query: the record --ech and the HTTPS-origin resolve ask for.
public sealed partial class DohDnsResolver
{
    // The HTTPS records ResolveHttpsOriginAsync asked for, by host and port, null for none.
    private readonly ConcurrentDictionary<(string Host, int Port), ServiceBindingRecord?> _httpsRecords = new();

    /// <summary>
    /// Fetches <paramref name="host" />'s HTTPS record (RFC 9460) through the DoH server, the
    /// query curl 8.21.0 adds to the A and AAAA ones under <c>--ech true</c> or <c>hard</c> to find
    /// the host's ECHConfigList (ADR-0312, BL-707). The name asked for is the host itself on port
    /// 443 and <c>_&lt;port&gt;._https.&lt;host&gt;</c> on any other port (RFC 9460 section 9.1), and
    /// the first HTTPS record of the answer is decoded, as curl decodes only the first. A record
    /// <see cref="ResolveHttpsOriginAsync" /> already asked for the host and port is returned without
    /// asking again.
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

        return _httpsRecords.TryGetValue((host, port), out var known)
            ? known
            : await QueryHttpsRecordAsync(host, port, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves an <c>https://</c> origin as curl 8.22.0's HTTPSRR build does (ADR-0727, BL-2920): its
    /// HTTPS query first, for the host on port 443 and <c>_&lt;port&gt;._https.&lt;host&gt;</c> on any
    /// other, answered before the A and AAAA queries <see cref="ResolveAsync" /> then sends. The record
    /// is kept, so <see cref="ResolveHttpsRecordAsync" /> for the same host and port asks nothing more,
    /// as curl reuses the one resolve under <c>--ech</c>. Its sub-transfer reports on
    /// <see cref="SubTransferEvents" /> and its <c>--trace-config doh</c> lines
    /// (<see cref="DohTraceLines.ReportHttpsRecord" />, ADR-0727's BL-2954 amendment) all come before the
    /// A and AAAA queries' lines.
    /// </summary>
    /// <param name="host">The origin's host name or literal address.</param>
    /// <param name="port">The port the transfer connects to.</param>
    /// <param name="cancellationToken">Cancels the look-up.</param>
    /// <returns>The addresses <see cref="ResolveAsync" /> returns for <paramref name="host" />.</returns>
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveHttpsOriginAsync(string host, int port, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        _httpsRecords[(host, port)] = await QueryHttpsRecordAsync(host, port, cancellationToken).ConfigureAwait(false);
        return await ResolveAsync(host, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ServiceBindingRecord? HttpsRecordFor(string host, int port) => _httpsRecords.GetValueOrDefault((host, port));

    /// <summary>
    /// Gets which of the HTTPS query's trace lines are written: <see cref="DohHttpsRecordTrace.All" />
    /// unless the run's <c>--trace-config</c> leaves some out (ADR-0727's BL-2979 amendment).
    /// </summary>
    public DohHttpsRecordTrace HttpsRecordTrace { get; init; } = DohHttpsRecordTrace.All;

    // Posts the HTTPS query, reports its trace lines and decodes the answer's first record; null
    // when nothing is asked or answered, and for a status outside 200-299, which curl 8.22.0 refuses.
    private async ValueTask<ServiceBindingRecord?> QueryHttpsRecordAsync(string host, int port, CancellationToken cancellationToken)
    {
        if (HttpsQueryFor(host, port) is not { } query)
        {
            return null;
        }

        TakeConnectionIdsFor(1);
        var result = await QueryAsync(query, DnsRecordType.Https, SubTransferEvents, cancellationToken).ConfigureAwait(false);
        DohTraceLines.ReportHttpsRecord(_dohTrace, _describeExitCode, result, HttpsRecordTrace);
        return FirstHttpsRecordOf(result);
    }

    // The answer's first HTTPS record decoded; null for a status outside 200-299, which curl 8.22.0
    // refuses, a failed exchange or decode, no record, or a first record that does not decode.
    private static ServiceBindingRecord? FirstHttpsRecordOf(DohQueryResult result) =>
        result.HasSuccessStatus ? FirstHttpsRecordOf(result.Answer) : null;

    private static ServiceBindingRecord? FirstHttpsRecordOf(DnsAnswer? answer) =>
        answer is { Failure: DnsMessageFailure.None, HttpsRecordData: [var first, ..] }
            ? ServiceBindingRecordDecoder.Decode(first).Record
            : null;

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
}
