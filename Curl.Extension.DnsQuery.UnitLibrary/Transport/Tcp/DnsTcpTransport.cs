using System.Buffers.Binary;
using Curl.Extension.DnsQuery.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.Transport.Tcp;

/// <summary>
/// Sends a query over TCP and reads its answer, each message prefixed by its length as two octets in network order
/// (RFC 1035 section 4.2.2). The transfer's <c>--max-time</c> bounds the wait, through its cancellation.
/// </summary>
/// <param name="connector">Connects to the server.</param>
internal sealed class DnsTcpTransport(IConnector connector) : IDnsQueryTransport
{
    private const int LengthPrefixLength = 2;

    /// <inheritdoc />
    public async ValueTask<byte[]> ExchangeAsync(string host, int port, byte[] query, ITransferContext context)
    {
        ConnectResult connect = await connector.ConnectAsync(new ConnectTarget(host, port, UseTls: false), context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            throw new DnsQueryException(connect.ExitCode, connect.ErrorMessage!);
        }

        await using (connection.ConfigureAwait(false))
        {
            byte[] framed = new byte[LengthPrefixLength + query.Length];
            BinaryPrimitives.WriteUInt16BigEndian(framed, (ushort)query.Length);
            query.CopyTo(framed, LengthPrefixLength);
            await connection.WriteAsync(framed, context.CancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(context.CancellationToken).ConfigureAwait(false);
            byte[] prefix = await ReadExactlyAsync(connection, LengthPrefixLength, context.CancellationToken).ConfigureAwait(false);
            return await ReadExactlyAsync(connection, BinaryPrimitives.ReadUInt16BigEndian(prefix), context.CancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<byte[]> ReadExactlyAsync(IConnection connection, int length, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[length];
        for (int filled = 0; filled < length;)
        {
            int read = await connection.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new DnsQueryException(CurlExitCode.RecvError, "The DNS server closed the connection before its whole answer arrived");
            }

            filled += read;
        }

        return buffer;
    }
}
