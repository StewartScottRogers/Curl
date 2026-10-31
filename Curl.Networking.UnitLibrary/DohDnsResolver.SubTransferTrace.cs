using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// How the A and AAAA queries are run, and the sub-transfer lines written between them.
public sealed partial class DohDnsResolver
{
    // Untraced, the queries run in parallel as curl runs them, and their outcomes are reported
    // once both are in; traced, one after another, each outcome after its own sub-transfer's
    // lines, so each sub-transfer's lines come whole. Either way the last query's outcome, AAAA's, is
    // reported first, as curl 8.22.0 reports it, and traced its sub-transfer runs first too; the results
    // keep the queries' order (ADR-0380, BL-2973).
    private async Task<DohQueryResult[]> RunAddressQueriesAsync(List<(byte[] Query, DnsRecordType Type)> queries, CancellationToken cancellationToken)
    {
        TakeConnectionIdsFor(queries.Count);
        DohQueryResult[] results;
        if (ReferenceEquals(SubTransferEvents, NoTransferEvents.Instance))
        {
            results = await Task.WhenAll(queries.Select(query => QueryAsync(query.Query, query.Type, SubTransferEvents, cancellationToken))).ConfigureAwait(false);
            for (var index = results.Length - 1; index >= 0; index--)
            {
                ReportOutcome(results[index], index);
            }

            return results;
        }

        results = new DohQueryResult[queries.Count];
        for (var index = queries.Count - 1; index >= 0; index--)
        {
            results[index] = await QueryAsync(queries[index].Query, queries[index].Type, SubTransferEvents, cancellationToken).ConfigureAwait(false);
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
