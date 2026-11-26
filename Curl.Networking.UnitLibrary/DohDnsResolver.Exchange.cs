using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// Each query's connection, POST and response, and how each of them can fail.
public sealed partial class DohDnsResolver
{
    private async Task<DohQueryResult> QueryAsync(byte[] query, DnsRecordType recordType, ITransferEvents events, CancellationToken cancellationToken)
    {
        var connectionNumber = Interlocked.Increment(ref _connectionCount);
        var connected = await ConnectToDohServerAsync(events, cancellationToken).ConfigureAwait(false);
        return await QueryOnAsync(connected, connectionNumber, query, recordType, events, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<ConnectResult> ConnectToDohServerAsync(ITransferEvents events, CancellationToken cancellationToken) =>
        _connector.ConnectAsync(DohConnectTarget with { Events = events }, cancellationToken);

    // curl sets CURLOPT_PIPEWAIT on its DoH sub-transfers, so over TLS the second query waits for the
    // first one's connection. A server that picked h2 gets both on it, A on stream 1 and AAAA on
    // stream 3; any other gets a connection for each query, traced the AAAA query first (ADR-1428).
    private async Task<DohQueryResult[]> RunQueriesAfterTheFirstConnectionAsync(List<(byte[] Query, DnsRecordType Type)> queries, CancellationToken cancellationToken)
    {
        TakeConnectionIdsFor(1);
        var isTraced = !ReferenceEquals(SubTransferEvents, NoTransferEvents.Instance);
        // curl's connect through a proxy was not measured, so it gets no waiting lines.
        var connectionNumber = Interlocked.Increment(ref _connectionCount);
        var connectEvents = isTraced && Proxy is null ? new DohPipeWaitTraceEvents(SubTransferEvents, connectionNumber) : SubTransferEvents;
        var connected = await ConnectToDohServerAsync(connectEvents, cancellationToken).ConfigureAwait(false);
        if (connected is { Connection: { } connection, ApplicationProtocol: "h2" })
        {
            return await QueryOnOneHttp2ConnectionAsync(connection, connectionNumber, queries, cancellationToken).ConfigureAwait(false);
        }

        TakeConnectionIdsFor(queries.Count);
        return await QueryOnAConnectionEachAsync(connected, connectionNumber, queries, isTraced, cancellationToken).ConfigureAwait(false);
    }

    // The first connection carries one query and a new one the other: traced, AAAA's whole before
    // A's; untraced, both at once. AAAA's outcome is reported first (ADR-0380).
    private async Task<DohQueryResult[]> QueryOnAConnectionEachAsync(ConnectResult connected, long connectionNumber, List<(byte[] Query, DnsRecordType Type)> queries, bool isTraced, CancellationToken cancellationToken)
    {
        var first = isTraced ? 1 : 0;
        var second = 1 - first;
        Task<DohQueryResult> OnFirstConnectionAsync() =>
            QueryOnAsync(connected, connectionNumber, queries[first].Query, queries[first].Type, SubTransferEvents, cancellationToken);
        Task<DohQueryResult> OnSecondConnectionAsync() =>
            QueryAsync(queries[second].Query, queries[second].Type, SubTransferEvents, cancellationToken);

        var results = new DohQueryResult[queries.Count];
        if (isTraced)
        {
            results[first] = await OnFirstConnectionAsync().ConfigureAwait(false);
            ReportOutcome(results[first], 1);
            results[second] = await OnSecondConnectionAsync().ConfigureAwait(false);
            ReportOutcome(results[second], 0);
            return results;
        }

        results = await Task.WhenAll(OnFirstConnectionAsync(), OnSecondConnectionAsync()).ConfigureAwait(false);
        ReportOutcome(results[1], 1);
        ReportOutcome(results[0], 0);
        return results;
    }

    // Sends every query on one HTTP/2 connection and reports each outcome as its stream ends.
    private async Task<DohQueryResult[]> QueryOnOneHttp2ConnectionAsync(IConnection connection, long connectionNumber, List<(byte[] Query, DnsRecordType Type)> queries, CancellationToken cancellationToken)
    {
        await using (connection.ConfigureAwait(false))
        {
            SubTransferEvents.ReportInfo("using HTTP/2");
            var results = new DohQueryResult[queries.Count];
            var queriesToGo = queries.Count;
            await ExchangeOverHttp2Async(connection, [.. queries.Select(query => query.Query)], SubTransferEvents, Answered, cancellationToken).ConfigureAwait(false);
            return results;

            void Answered(int index, DohResponseReader.Response? response)
            {
                queriesToGo--;
                // curl says the connection is left intact once, as its last stream ends.
                if (queriesToGo == 0 && response is not null)
                {
                    ReportLeftIntact(connectionNumber, SubTransferEvents);
                }

                results[index] = ResultOf(response, queries[index].Type);
                ReportOutcome(results[index], queriesToGo);
            }
        }
    }

    // Sends one query on its own connection, over HTTP/2 when the server picked h2.
    private async Task<DohQueryResult> QueryOnAsync(ConnectResult connected, long connectionNumber, byte[] query, DnsRecordType recordType, ITransferEvents events, CancellationToken cancellationToken)
    {
        if (connected.Connection is not { } connection)
        {
            return new DohQueryResult(recordType, connected.ExitCode, null);
        }

        await using (connection.ConfigureAwait(false))
        {
            var isHttp2 = connected.ApplicationProtocol == "h2";
            events.ReportInfo(isHttp2 ? "using HTTP/2" : "using HTTP/1.x");
            DohResponseReader.Response? response = null;
            if (isHttp2)
            {
                await ExchangeOverHttp2Async(connection, [query], events, (_, answer) => response = answer, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                response = await ExchangeAsync(connection, BuildRequestHead(query), query, events, cancellationToken).ConfigureAwait(false);
            }

            if (response is not null)
            {
                ReportLeftIntact(connectionNumber, events);
            }

            return ResultOf(response, recordType);
        }
    }

    private void ReportLeftIntact(long connectionNumber, ITransferEvents events) =>
        events.ReportInfo(FormattableString.Invariant($"Connection #{connectionNumber} to host {_dohServer.Host}:{_dohServer.Port} left intact"));

    // The query's result: a receive error with no response, else its status and, for a success, its answer.
    private static DohQueryResult ResultOf(DohResponseReader.Response? response, DnsRecordType recordType)
    {
        if (response is null)
        {
            return new DohQueryResult(recordType, CurlExitCode.RecvError, null);
        }

        // curl 8.22.0's doh_probe_done fails a status outside 200-299 without decoding the body (BL-2972).
        var result = new DohQueryResult(recordType, CurlExitCode.Ok, null) { StatusCode = response.StatusCode };
        return result.HasSuccessStatus ? result with { Answer = DecodeAnswer(response.Body, recordType) } : result;
    }

    // Writes the POST and reads the answer; null when the connection fails mid-exchange or the
    // response is not read whole, which curl reports as "Failure when receiving data from the peer".
    private static async ValueTask<DohResponseReader.Response?> ExchangeAsync(IConnection connection, byte[] head, byte[] query, ITransferEvents events, CancellationToken cancellationToken)
    {
        try
        {
            byte[] request = [.. head, .. query];
            await connection.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            events.ReportRequestHeader(head);
            events.ReportDataSent(query);
            events.ReportInfo($"upload completely sent off: {query.Length} bytes");
            return await DohResponseReader.ReadResponseAsync(connection, events, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
