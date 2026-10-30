using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Extension.Dane.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// Sends one DNSSEC query to a recursive resolver and reads its answer: over UDP first, then again over TCP with
/// RFC 1035 section 4.2.2's two-octet length prefix when the UDP answer is truncated (RFC 7766 section 5). Every
/// query sets DO and CD (RFC 4035 section 3.2.2) so the caller validates the answer itself; the answer must carry
/// the query's ID and, over UDP, come from the server's address.
/// </summary>
/// <param name="datagramConnector">Opens the UDP channel to the server.</param>
/// <param name="streamConnector">Opens the TCP connection to the server when a UDP answer is truncated.</param>
/// <param name="serverHost">The resolver's host name or address.</param>
/// <param name="serverPort">The resolver's port, 53 normally.</param>
/// <param name="timeProvider">The clock the timeout runs on.</param>
/// <param name="timeout">How long one query, UDP and TCP together, may take.</param>
public sealed class DnssecQueryTransport(
    IDatagramConnector datagramConnector,
    IConnector streamConnector,
    string serverHost,
    int serverPort,
    TimeProvider timeProvider,
    TimeSpan timeout)
{
    private const int LengthPrefixSize = 2;

    private const int MaximumMessageSize = ushort.MaxValue;

    /// <summary>Asks the server for <paramref name="name" />'s <paramref name="type" /> records.</summary>
    /// <param name="name">The query name.</param>
    /// <param name="type">The query type.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The decoded answer, or null when the server cannot be reached, times out or answers unreadably.</returns>
    public async ValueTask<DnssecResponse?> QueryAsync(string name, DnssecRecordType type, CancellationToken cancellationToken)
    {
        ushort id = (ushort)RandomNumberGenerator.GetInt32(MaximumMessageSize + 1);
        byte[] query = DnssecQueryEncoder.Encode(id, name, type, checkingDisabled: true);
        using CancellationTokenSource timeoutSource = new(timeout, timeProvider);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            DnssecResponse? response = await QueryOverUdpAsync(query, id, linkedSource.Token).ConfigureAwait(false);
            return response is { IsTruncated: true }
                ? await QueryOverTcpAsync(query, id, linkedSource.Token).ConfigureAwait(false)
                : response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async ValueTask<DnssecResponse?> QueryOverUdpAsync(byte[] query, ushort id, CancellationToken cancellationToken)
    {
        DatagramOpenResult opened = await datagramConnector.OpenAsync(serverHost, serverPort, cancellationToken).ConfigureAwait(false);
        if (opened.Channel is not { } channel)
        {
            return null;
        }

        await using (channel.ConfigureAwait(false))
        {
            await channel.SendAsync(query, channel.ServerEndPoint, cancellationToken).ConfigureAwait(false);
            byte[] buffer = new byte[MaximumMessageSize];
            while (true)
            {
                DatagramReceived received = await channel.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (received.RemoteEndPoint.Equals(channel.ServerEndPoint) &&
                    TryDecodeAnswer(buffer.AsSpan(0, received.Length), id, out DnssecResponse? response))
                {
                    return response;
                }
            }
        }
    }

    private async ValueTask<DnssecResponse?> QueryOverTcpAsync(byte[] query, ushort id, CancellationToken cancellationToken)
    {
        ConnectResult connected = await streamConnector
            .ConnectAsync(new ConnectTarget(serverHost, serverPort, UseTls: false), cancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return null;
        }

        await using (connection.ConfigureAwait(false))
        {
            byte[] framed = new byte[LengthPrefixSize + query.Length];
            BinaryPrimitives.WriteUInt16BigEndian(framed, (ushort)query.Length);
            query.CopyTo(framed, LengthPrefixSize);
            await connection.WriteAsync(framed, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            byte[] lengthPrefix = new byte[LengthPrefixSize];
            if (!await TryReadExactlyAsync(connection, lengthPrefix, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            byte[] message = new byte[BinaryPrimitives.ReadUInt16BigEndian(lengthPrefix)];
            return await TryReadExactlyAsync(connection, message, cancellationToken).ConfigureAwait(false) &&
                TryDecodeAnswer(message, id, out DnssecResponse? response)
                ? response
                : null;
        }
    }

    private static async ValueTask<bool> TryReadExactlyAsync(IConnection connection, byte[] buffer, CancellationToken cancellationToken)
    {
        int filled = 0;
        while (filled < buffer.Length)
        {
            int read = await connection.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            filled += read;
        }

        return true;
    }

    private static bool TryDecodeAnswer(ReadOnlySpan<byte> message, ushort id, out DnssecResponse? response) =>
        DnssecResponseDecoder.TryDecode(message, out response) && response.Id == id;
}
