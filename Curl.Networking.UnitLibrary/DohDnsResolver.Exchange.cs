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
            var body = await ExchangeAsync(connection, BuildRequestHead(query), query, events, cancellationToken).ConfigureAwait(false);
            if (body is null)
            {
                return new DohQueryResult(recordType, CurlExitCode.RecvError, null);
            }

            events.ReportInfo(FormattableString.Invariant($"Connection #{connectionNumber} to host {_dohServer.Host}:{_dohServer.Port} left intact"));
            return new DohQueryResult(recordType, CurlExitCode.Ok, DecodeAnswer(body, recordType));
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
