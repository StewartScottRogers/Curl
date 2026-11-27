using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// How the A and AAAA queries are run, and the sub-transfer lines written between them.
public sealed partial class DohDnsResolver
{
    private bool IsTraced => !ReferenceEquals(SubTransferEvents, NoTransferEvents.Instance);

    private Task<DohQueryResult[]> RunAddressQueriesAsync(List<(byte[] Query, DnsRecordType Type)> queries, CancellationToken cancellationToken)
    {
        var kept = TakeKeptConnections(queries.Count);
        return queries.Count > 1
            ? RunBothQueriesAsync(queries, kept, cancellationToken)
            : QueryOnAConnectionEachAsync(queries, kept, cancellationToken);
    }

    // Both queries go on a kept HTTP/2 connection (ADR-1441). Over TLS with no kept connection, the
    // second waits for the first one's (ADR-1435). Else each query goes on a kept HTTP/1.x connection
    // or a new one.
    private Task<DohQueryResult[]> RunBothQueriesAsync(List<(byte[] Query, DnsRecordType Type)> queries, List<DohConnection> kept, CancellationToken cancellationToken)
    {
        if (kept.Find(connection => connection.IsHttp2) is { } shared)
        {
            return QueryOnOneHttp2ConnectionAsync(shared, queries, isKept: true, cancellationToken);
        }

        return kept.Count == 0 && _dohServer.UseTls
            ? RunQueriesAfterTheFirstConnectionAsync(queries, cancellationToken)
            : QueryOnAConnectionEachAsync(queries, kept, cancellationToken);
    }

    // Untraced, the queries run in parallel as curl runs them, and their outcomes are reported once
    // all are in; traced, one after another, each outcome after its own sub-transfer's lines, so each
    // sub-transfer's lines come whole. The last query's outcome, AAAA's, is reported first, as curl
    // 8.22.0 reports it, and traced its sub-transfer runs first too (ADR-0380, BL-2973), so it takes
    // the first kept connection (ADR-1441). The results keep the queries' order.
    private async Task<DohQueryResult[]> QueryOnAConnectionEachAsync(List<(byte[] Query, DnsRecordType Type)> queries, List<DohConnection> kept, CancellationToken cancellationToken)
    {
        Task<DohQueryResult> RunAsync(int index) =>
            QueryAsync(kept.ElementAtOrDefault(queries.Count - 1 - index), queries[index].Query, queries[index].Type, cancellationToken);

        var results = IsTraced
            ? new DohQueryResult[queries.Count]
            : await Task.WhenAll(Enumerable.Range(0, queries.Count).Select(RunAsync)).ConfigureAwait(false);
        for (var index = queries.Count - 1; index >= 0; index--)
        {
            if (IsTraced)
            {
                results[index] = await RunAsync(index).ConfigureAwait(false);
            }

            ReportOutcome(results[index], index);
        }

        return results;
    }

    // curl 8.22.0 ends each query with its error line, or, when it succeeded, the completed line (BL-2970).
    private void ReportOutcome(DohQueryResult result, int queriesToGo)
    {
        if (DohTraceLines.FailureLine(_describeExitCode, result) is { } failureLine)
        {
            _dohTrace.ReportInfo(failureLine);
        }
        else
        {
            SubTransferEvents.ReportInfo($"DoH request complete, {queriesToGo} to go");
        }
    }

    // The A query and the AAAA query, in that order (AAAA runs first), leaving out the one -4 or -6 rules out.
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
}
