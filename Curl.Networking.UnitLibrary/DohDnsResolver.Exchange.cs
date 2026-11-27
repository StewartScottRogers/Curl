using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// Each query's connection, POST and response, and how each of them can fail.
public sealed partial class DohDnsResolver
{
    // Sends one query on the kept connection it was given, else on a new one. A kept connection the
    // server closed in the meantime answers nothing, so the query is then asked on a new one.
    private async Task<DohQueryResult> QueryAsync(DohConnection? kept, byte[] query, DnsRecordType recordType, CancellationToken cancellationToken)
    {
        if (kept is not null)
        {
            ReportReuse();
            if (await ExchangeOnAsync(kept, query, cancellationToken).ConfigureAwait(false) is { } response)
            {
                return ResultOf(response, recordType);
            }
        }

        var connectionNumber = NextConnectionNumber();
        var connected = await ConnectToDohServerAsync(SubTransferEvents, cancellationToken).ConfigureAwait(false);
        return await QueryOnAsync(connected, connectionNumber, query, recordType, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<ConnectResult> ConnectToDohServerAsync(ITransferEvents events, CancellationToken cancellationToken) =>
        _connector.ConnectAsync(DohConnectTarget with { Events = events }, cancellationToken);

    // curl sets CURLOPT_PIPEWAIT on its DoH sub-transfers, so over TLS the second query waits for the
    // first one's connection. A server that picked h2 gets both on it, A on stream 1 and AAAA on
    // stream 3 (ADR-1435); any other gets one query and, after its answer, the other, on the same
    // connection when that answer leaves it open and else on a new one (ADR-1441): traced AAAA goes
    // first (ADR-0380), untraced A.
    private async Task<DohQueryResult[]> RunQueriesAfterTheFirstConnectionAsync(List<(byte[] Query, DnsRecordType Type)> queries, CancellationToken cancellationToken)
    {
        var connectionNumber = NextConnectionNumber();
        var connected = await ConnectToDohServerAsync(FirstConnectEvents(connectionNumber), cancellationToken).ConfigureAwait(false);
        var run = connected is { Connection: { } connection, ApplicationProtocol: "h2" }
            ? QueryOnOneHttp2ConnectionAsync(new DohConnection(connection, connectionNumber, isHttp2: true), queries, isKept: false, cancellationToken)
            : QueryOneAfterTheOtherAsync(connected, connectionNumber, queries, cancellationToken);
        return await run.ConfigureAwait(false);
    }

    // The first connect's events: traced and with no proxy, whose connect curl was not measured for,
    // they carry the second sub-transfer's waiting lines.
    private ITransferEvents FirstConnectEvents(long connectionNumber) =>
        IsTraced && Proxy is null ? new DohPipeWaitTraceEvents(SubTransferEvents, connectionNumber) : SubTransferEvents;

    // One query on the connection just made, then the other on it when it was kept, else on a new one.
    // Traced, each outcome is reported after its query; untraced, both once both are in, AAAA's first.
    private async Task<DohQueryResult[]> QueryOneAfterTheOtherAsync(ConnectResult connected, long connectionNumber, List<(byte[] Query, DnsRecordType Type)> queries, CancellationToken cancellationToken)
    {
        var first = IsTraced ? 1 : 0;
        var second = 1 - first;
        var results = new DohQueryResult[queries.Count];
        results[first] = await QueryOnAsync(connected, connectionNumber, queries[first].Query, queries[first].Type, cancellationToken).ConfigureAwait(false);
        ReportOutcomeWhen(IsTraced, results[1], 1);
        var kept = TakeKeptConnections(1).SingleOrDefault();
        results[second] = await QueryAsync(kept, queries[second].Query, queries[second].Type, cancellationToken).ConfigureAwait(false);
        ReportOutcomeWhen(!IsTraced, results[1], 1);
        ReportOutcome(results[0], 0);
        return results;
    }

    private void ReportOutcomeWhen(bool isDue, DohQueryResult result, int queriesToGo)
    {
        if (isDue)
        {
            ReportOutcome(result, queriesToGo);
        }
    }

    // Sends every query on one HTTP/2 connection. A kept connection that answers none of them was
    // closed in the meantime, so they are asked on a new one.
    private async Task<DohQueryResult[]> QueryOnOneHttp2ConnectionAsync(DohConnection connection, List<(byte[] Query, DnsRecordType Type)> queries, bool isKept, CancellationToken cancellationToken)
    {
        ReportConnectionTaken(isKept);
        return await AskOnHttp2ConnectionAsync(connection, queries, isKept, cancellationToken).ConfigureAwait(false)
            ?? await RunQueriesAfterTheFirstConnectionAsync(queries, cancellationToken).ConfigureAwait(false);
    }

    private void ReportConnectionTaken(bool isKept)
    {
        if (isKept)
        {
            ReportReuse();
        }
        else
        {
            SubTransferEvents.ReportInfo("using HTTP/2");
        }
    }

    // Asks every query on the connection and reports each outcome as its stream ends, then keeps the
    // connection when every response was read; null when a kept connection answered none.
    private async Task<DohQueryResult[]?> AskOnHttp2ConnectionAsync(DohConnection connection, List<(byte[] Query, DnsRecordType Type)> queries, bool isKept, CancellationToken cancellationToken)
    {
        var results = new DohQueryResult[queries.Count];
        var queriesToGo = queries.Count;
        var isEveryResponseRead = true;
        var lease = new ConnectionLease(this, connection);
        await using (lease.ConfigureAwait(false))
        {
            await ExchangeOverHttp2Async(connection, [.. queries.Select(query => query.Query)], Answered, mayAskAgain: isKept, cancellationToken).ConfigureAwait(false);
            lease.IsUsable = queriesToGo == 0 && isEveryResponseRead;
        }

        return queriesToGo == queries.Count ? null : results;

        void Answered(int index, DohResponseReader.Response? response)
        {
            queriesToGo--;
            isEveryResponseRead &= response is not null;
            // curl says the connection is left intact once, as its last stream ends.
            if (queriesToGo == 0 && response is not null)
            {
                ReportLeftIntact(connection.Number);
            }

            results[index] = ResultOf(response, queries[index].Type);
            ReportOutcome(results[index], queriesToGo);
        }
    }

    // Sends one query on the connection just made for it; the connect's failure when there is none.
    private async Task<DohQueryResult> QueryOnAsync(ConnectResult connected, long connectionNumber, byte[] query, DnsRecordType recordType, CancellationToken cancellationToken)
    {
        if (connected.Connection is not { } transport)
        {
            return new DohQueryResult(recordType, connected.ExitCode, null);
        }

        var connection = new DohConnection(transport, connectionNumber, connected.ApplicationProtocol == "h2");
        SubTransferEvents.ReportInfo(connection.IsHttp2 ? "using HTTP/2" : "using HTTP/1.x");
        return ResultOf(await ExchangeOnAsync(connection, query, cancellationToken).ConfigureAwait(false), recordType);
    }

    // Sends one query on the connection, then keeps the connection for later queries when the response
    // leaves it open, else closes it.
    private async ValueTask<DohResponseReader.Response?> ExchangeOnAsync(DohConnection connection, byte[] query, CancellationToken cancellationToken)
    {
        var lease = new ConnectionLease(this, connection);
        await using (lease.ConfigureAwait(false))
        {
            var response = await SendQueryAsync(connection, query, cancellationToken).ConfigureAwait(false);
            if (response is not null)
            {
                ReportLeftIntact(connection.Number);
            }

            lease.IsUsable = response is { KeepsConnectionOpen: true };
            return response;
        }
    }

    // Sends one query on the connection, over HTTP/2 when the server picked h2.
    private async ValueTask<DohResponseReader.Response?> SendQueryAsync(DohConnection connection, byte[] query, CancellationToken cancellationToken)
    {
        if (!connection.IsHttp2)
        {
            return await ExchangeAsync(connection.Transport, BuildRequestHead(query), query, SubTransferEvents, cancellationToken).ConfigureAwait(false);
        }

        DohResponseReader.Response? response = null;
        await ExchangeOverHttp2Async(connection, [query], (_, answer) => response = answer, mayAskAgain: false, cancellationToken).ConfigureAwait(false);
        return response;
    }

    private void ReportLeftIntact(long connectionNumber) =>
        SubTransferEvents.ReportInfo(FormattableString.Invariant($"Connection #{connectionNumber} to host {_dohServer.Host}:{_dohServer.Port} left intact"));

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
