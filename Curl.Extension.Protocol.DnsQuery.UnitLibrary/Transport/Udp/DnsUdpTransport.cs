using System.Buffers.Binary;
using Curl.Extension.Protocol.DnsQuery.Client;
using Curl.Extension.Protocol.DnsQuery.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.DnsQuery.Transport.Udp;

/// <summary>
/// Sends a query as one UDP datagram and waits for the answer (RFC 1035 section 4.2.1): it sends the query up to
/// <see cref="Attempts" /> times, waiting <see cref="AttemptTimeout" /> on the transfer's clock after each, ignores any
/// datagram that is not a response with the query's message ID, and asks the query again over <paramref name="fallback" />
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
            answer = await ExchangeOverAsync(channel, query, context).ConfigureAwait(false);
        }

        return IsTruncated(answer) ? await fallback.ExchangeAsync(host, port, query, context).ConfigureAwait(false) : answer;
    }

    private static async ValueTask<byte[]> ExchangeOverAsync(IDatagramChannel channel, byte[] query, ITransferContext context)
    {
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            await channel.SendAsync(query, channel.ServerEndPoint, context.CancellationToken).ConfigureAwait(false);
            if (await ReceiveAnswerAsync(channel, query, context).ConfigureAwait(false) is { } answer)
            {
                return answer;
            }
        }

        throw new DnsQueryException(CurlExitCode.OperationTimedOut, $"The DNS server sent no answer to {Attempts} queries over UDP");
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
                if (IsAnswerTo(buffer.AsSpan(0, received.Length), query))
                {
                    return buffer[..received.Length];
                }
            }
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Tells whether a datagram is a response (QR set) carrying the query's message ID (RFC 1035 section 4.1.1).</summary>
    private static bool IsAnswerTo(ReadOnlySpan<byte> datagram, byte[] query) =>
        datagram.Length >= HeaderLength
        && datagram[..2].SequenceEqual(query.AsSpan(0, 2))
        && (BinaryPrimitives.ReadUInt16BigEndian(datagram[2..]) & DnsHeader.ResponseFlag) != 0;

    private static bool IsTruncated(byte[] answer) =>
        (BinaryPrimitives.ReadUInt16BigEndian(answer.AsSpan(2)) & DnsHeader.TruncatedFlag) != 0;
}
