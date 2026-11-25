using System.Buffers.Binary;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.Transport.Tcp;

/// <summary>
/// Sends a query over TCP and reads its answer, each message prefixed by its length as two octets in network order
/// (RFC 1035 section 4.2.2). The transfer's <c>--max-time</c> bounds the wait, through its cancellation.
/// </summary>
/// <param name="connector">Connects to the server.</param>
internal sealed class DnsTcpTransport(IConnector connector) : IDnsQueryTransport
{
    private const int LengthPrefixLength = 2;

    /// <inheritdoc />
    public int DefaultPort => DnsQueryRequest.DefaultPort;

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
            return await ExchangeOverAsync(connection, query, context.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes <paramref name="query" /> with its two-octet length prefix on an open connection and reads one
    /// length-prefixed answer; DNS over TLS frames its messages the same way (RFC 7858 section 3.3).
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="query">The encoded query.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The answer's octets, without the prefix.</returns>
    /// <exception cref="DnsQueryException">
    /// Sending the query failed (exit 55), or reading the answer failed or the connection closed before the whole answer
    /// arrived (exit 56). A failed socket read or write is worded as curl words it (<see cref="CurlSocketErrorText" />).
    /// </exception>
    public static async ValueTask<byte[]> ExchangeOverAsync(IConnection connection, byte[] query, CancellationToken cancellationToken)
    {
        await SendAsync(connection, query, cancellationToken).ConfigureAwait(false);
        byte[] prefix = await ReadExactlyAsync(connection, LengthPrefixLength, cancellationToken).ConfigureAwait(false);
        return await ReadExactlyAsync(connection, BinaryPrimitives.ReadUInt16BigEndian(prefix), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask SendAsync(IConnection connection, byte[] query, CancellationToken cancellationToken)
    {
        byte[] framed = new byte[LengthPrefixLength + query.Length];
        BinaryPrimitives.WriteUInt16BigEndian(framed, (ushort)query.Length);
        query.CopyTo(framed, LengthPrefixLength);
        try
        {
            await connection.WriteAsync(framed, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure)
        {
            throw SendFailureOf(failure);
        }
    }

    /// <summary>
    /// How a query ends when sending it failed: exit 55 with curl's <c>Send failure:</c> words for a socket error, and
    /// otherwise curl_easy_strerror's text for exit 55, never .NET's.
    /// </summary>
    /// <param name="failure">The failed write or flush.</param>
    /// <returns>The failure to throw.</returns>
    public static DnsQueryException SendFailureOf(IOException failure) =>
        new(CurlExitCode.SendError, CurlSocketErrorText.SendFailure(failure) ?? "Failed sending data to the peer");

    /// <summary>
    /// How a query ends when reading its answer failed: exit 56, keeping the words a TLS connection ended without
    /// close_notify already carries, with curl's <c>Recv failure:</c> words for a socket error, and otherwise
    /// curl_easy_strerror's text for exit 56, never .NET's.
    /// </summary>
    /// <param name="failure">The failed read.</param>
    /// <returns>The failure to throw.</returns>
    public static DnsQueryException ReceiveFailureOf(IOException failure) =>
        new(CurlExitCode.RecvError, failure is MissingCloseNotifyException
            ? failure.Message
            : CurlSocketErrorText.ReceiveFailure(failure) ?? "Failure when receiving data from the peer");

    private static async ValueTask<byte[]> ReadExactlyAsync(IConnection connection, int length, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[length];
        for (int filled = 0; filled < length;)
        {
            int read = await ReadAsync(connection, buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new DnsQueryException(CurlExitCode.RecvError, "The DNS server closed the connection before its whole answer arrived");
            }

            filled += read;
        }

        return buffer;
    }

    private static async ValueTask<int> ReadAsync(IConnection connection, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure)
        {
            throw ReceiveFailureOf(failure);
        }
    }
}
