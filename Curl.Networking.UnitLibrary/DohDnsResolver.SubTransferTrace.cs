using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// How the A and AAAA queries are run, and the sub-transfer lines written between them.
public sealed partial class DohDnsResolver
{
    // Untraced, the queries run in parallel as curl runs them; traced, one after another, so each
    // sub-transfer's lines come whole, A's first (ADR-0380).
    private async Task<DohQueryResult[]> RunAddressQueriesAsync(List<(byte[] Query, DnsRecordType Type)> queries, CancellationToken cancellationToken)
    {
        if (ReferenceEquals(SubTransferEvents, NoTransferEvents.Instance))
        {
            return await Task.WhenAll(queries.Select(query => QueryAsync(query.Query, query.Type, SubTransferEvents, cancellationToken))).ConfigureAwait(false);
        }

        var results = new DohQueryResult[queries.Count];
        for (var index = 0; index < queries.Count; index++)
        {
            results[index] = await QueryAsync(queries[index].Query, queries[index].Type, SubTransferEvents, cancellationToken).ConfigureAwait(false);
            SubTransferEvents.ReportInfo($"a DoH request is completed, {queries.Count - index - 1} to go");
        }

        return results;
    }

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
}
