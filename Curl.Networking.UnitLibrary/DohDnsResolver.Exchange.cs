using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// One query's connection, POST and response, and how each of them can fail.
public sealed partial class DohDnsResolver
{
    private async Task<DohQueryResult> QueryAsync(byte[] query, DnsRecordType recordType, ITransferEvents events, CancellationToken cancellationToken)
    {
        var connectionNumber = Interlocked.Increment(ref _connectionCount);
        var connected = await _connector.ConnectAsync(_dohServer with { Events = events }, cancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return new DohQueryResult(recordType, connected.ExitCode, null);
        }

        await using (connection.ConfigureAwait(false))
        {
            events.ReportInfo("using HTTP/1.x");
            var response = await ExchangeAsync(connection, BuildRequestHead(query), query, events, cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                return new DohQueryResult(recordType, CurlExitCode.RecvError, null);
            }

            events.ReportInfo(FormattableString.Invariant($"Connection #{connectionNumber} to host {_dohServer.Host}:{_dohServer.Port} left intact"));
            return new DohQueryResult(recordType, CurlExitCode.Ok, DecodeAnswer(response.Body, recordType)) { StatusCode = response.StatusCode };
        }
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
