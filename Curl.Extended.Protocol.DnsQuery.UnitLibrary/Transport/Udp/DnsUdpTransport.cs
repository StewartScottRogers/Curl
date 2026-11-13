using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.Transport.Udp;

/// <summary>
/// Sends a query as one UDP datagram and waits for the answer (RFC 1035 section 4.2.1): it sends the query up to
/// <see cref="Attempts" /> times, waiting <see cref="AttemptTimeout" /> on the transfer's clock after each, ignores any
/// datagram that is not a response with the query's message ID and question (RFC 5452 section 9.1), and asks the query again over <paramref name="fallback" />
/// when the answer has TC set. No answer after the last attempt ends with exit 28.
/// </summary>
/// <param name="connector">Opens the datagram channel to the server.</param>
/// <param name="fallback">The transport a truncated answer is asked again over: TCP.</param>
internal sealed class DnsUdpTransport(IDatagramConnector connector, IDnsQueryTransport fallback) : IDnsQueryTransport
{
    /// <summary>How many times the query is sent before giving up, as dig's default <c>+tries=3</c>.</summary>
    public const int Attempts = 3;

    /// <summary>How long each attempt waits for the answer, as dig's default <c>+timeout=5</c>.</summary>
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);

    private const int HeaderLength = 12;
    private const int MaximumDatagramLength = 65535;

    /// <inheritdoc />
    public int DefaultPort => DnsQueryRequest.DefaultPort;

    /// <inheritdoc />
    public async ValueTask<byte[]> ExchangeAsync(string host, int port, byte[] query, ITransferContext context)
    {
        DatagramOpenResult open = await connector.OpenAsync(host, port, context.CancellationToken).ConfigureAwait(false);
        if (open.Channel is not { } channel)
        {
            throw new DnsQueryException(open.ExitCode, open.ErrorMessage!);
        }

        byte[] answer;
        await using (channel.ConfigureAwait(false))
        {
            ReportConnectionOpened(context, host, channel, open.ConnectionNumber);
            answer = await ExchangeOverAsync(channel, query, context).ConfigureAwait(false);
        }

        return IsTruncated(answer) ? await fallback.ExchangeAsync(host, port, query, context).ConfigureAwait(false) : answer;
    }

    /// <summary>
    /// Reports the opened UDP channel, which <c>-v</c> writes as the <c>Established connection</c> line every transfer
    /// writes (ADR-1222), naming the server's host, its address and the local address (<c>0.0.0.0:0</c> when the
    /// channel names none).
    /// </summary>
    private static void ReportConnectionOpened(ITransferContext context, string host, IDatagramChannel channel, long connectionNumber)
    {
        if (channel.ServerEndPoint is IPEndPoint server)
        {
            context.Events.ReportConnectionOpened(new ConnectionOpenedEvent
            {
                HostName = host,
                RemoteEndPoint = server,
                LocalEndPoint = channel.LocalEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, 0),
                ConnectionNumber = connectionNumber,
            });
        }
    }

    private static async ValueTask<byte[]> ExchangeOverAsync(IDatagramChannel channel, byte[] query, ITransferContext context)
    {
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            await SendQueryAsync(channel, query, context).ConfigureAwait(false);
            if (await ReceiveAnswerAsync(channel, query, context).ConfigureAwait(false) is { } answer)
            {
                return answer;
            }
        }

        throw new DnsQueryException(CurlExitCode.OperationTimedOut, $"The DNS server sent no answer to {Attempts} queries over UDP");
    }

    /// <summary>Sends the query once; a socket error (a refusal after a datagram was received, say) ends with exit 55.</summary>
    private static async ValueTask SendQueryAsync(IDatagramChannel channel, byte[] query, ITransferContext context)
    {
        try
        {
            await channel.SendAsync(query, channel.ServerEndPoint, context.CancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception) when (exception.GetType() == typeof(SocketException))
        {
            throw new DnsQueryException(CurlExitCode.SendError, $"Failed sending the query over UDP: {exception.Message}");
        }
    }

    /// <summary>Waits one attempt's time for the answer, and gives <see langword="null" /> when none came.</summary>
    private static async ValueTask<byte[]?> ReceiveAnswerAsync(IDatagramChannel channel, byte[] query, ITransferContext context)
    {
        using var timeout = new CancellationTokenSource(AttemptTimeout, context.TimeProvider);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeout.Token);
        byte[] buffer = new byte[MaximumDatagramLength];
        try
        {
            while (true)
            {
                DatagramReceived received = await channel.ReceiveAsync(buffer, waiting.Token).ConfigureAwait(false);
                if (IsFromServer(channel.ServerEndPoint, received.RemoteEndPoint) && IsAnswerTo(buffer.AsSpan(0, received.Length), query))
                {
                    return buffer[..received.Length];
                }
            }
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException exception) when (exception.GetType() == typeof(SocketException))
        {
            throw new DnsQueryException(CurlExitCode.RecvError, $"Failed receiving the answer over UDP: {exception.Message}");
        }
    }

    /// <summary>
    /// Tells whether a datagram is a response (QR set) carrying the query's message ID (RFC 1035 section 4.1.1) and,
    /// when it has a question section, the query's question (RFC 5452 section 9.1, ADR-1225).
    /// </summary>
    private static bool IsAnswerTo(ReadOnlySpan<byte> datagram, byte[] query) =>
        datagram.Length >= HeaderLength
        && datagram[..2].SequenceEqual(query.AsSpan(0, 2))
        && (BinaryPrimitives.ReadUInt16BigEndian(datagram[2..]) & DnsHeader.ResponseFlag) != 0
        && (BinaryPrimitives.ReadUInt16BigEndian(datagram[4..]) == 0 || StartsWithQuestionOf(datagram[HeaderLength..], query));

    /// <summary>
    /// Tells whether a response's question section starts with the query's question: the same QNAME, compared without
    /// regard to ASCII case, and the same QTYPE and QCLASS (RFC 5452 section 9.1). The query's own QNAME is uncompressed.
    /// </summary>
    private static bool StartsWithQuestionOf(ReadOnlySpan<byte> questions, byte[] query)
    {
        ReadOnlySpan<byte> asked = query.AsSpan(HeaderLength);
        int nameLength = NameLength(asked);
        int questionLength = nameLength + 4;
        return questions.Length >= questionLength
            && Ascii.EqualsIgnoreCase(questions[..nameLength], asked[..nameLength])
            && questions[nameLength..questionLength].SequenceEqual(asked[nameLength..questionLength]);
    }

    /// <summary>Gives the length of an uncompressed domain name, its terminating root label included.</summary>
    private static int NameLength(ReadOnlySpan<byte> name)
    {
        int offset = 0;
        while (name[offset] != 0)
        {
            offset += name[offset] + 1;
        }

        return offset + 1;
    }

    /// <summary>
    /// Tells whether a datagram came from the server queried: the same port and the same address, an IPv4-mapped IPv6
    /// address counting as its IPv4 form (RFC 5452 section 9.1). A server endpoint that is not an IP endpoint cannot be
    /// compared and accepts any source.
    /// </summary>
    private static bool IsFromServer(EndPoint server, EndPoint source) =>
        server is not IPEndPoint expected
        || (source is IPEndPoint actual && actual.Port == expected.Port && Unmapped(actual.Address).Equals(Unmapped(expected.Address)));

    private static IPAddress Unmapped(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static bool IsTruncated(byte[] answer) =>
        (BinaryPrimitives.ReadUInt16BigEndian(answer.AsSpan(2)) & DnsHeader.TruncatedFlag) != 0;
}
