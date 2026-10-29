using System.Buffers.Binary;
using System.Net;
using Curl.Extension.EncryptedDns.Wire;
using Curl.Protocol.Abstractions;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Extension.EncryptedDns.DnsOverQuic;

/// <summary>
/// Looks host names up over DNS over QUIC (RFC 9250): opens a QUIC connection to the server, port 853 by default,
/// offering the ALPN token <c>doq</c> (section 4.1.1), then sends the AAAA and the A query each on its own client
/// bidirectional stream with DNS Message ID 0 and the two-octet length prefix, ending the stream with FIN after the
/// query (sections 4.2 and 4.2.1). An answer whose Message ID is not 0 or that lacks its length prefix closes the
/// connection with DOQ_PROTOCOL_ERROR; otherwise it closes with DOQ_NO_ERROR (sections 4.3 and 4.5).
/// </summary>
/// <param name="connector">Opens the UDP datagram channel to the server.</param>
/// <param name="verifier">Checks the certificate chain the server presents.</param>
/// <param name="server">The server to ask.</param>
/// <param name="timeProvider">Times the QUIC handshake, loss recovery and idle timeout.</param>
public sealed class DnsOverQuicResolver(IDatagramConnector connector, IServerCertificateVerifier verifier, DnsOverQuicServer server, TimeProvider timeProvider) : IDnsResolver
{
    /// <summary>The ALPN token RFC 9250 section 4.1.1 assigns to DNS over QUIC.</summary>
    public const string ApplicationProtocol = "doq";

    /// <summary>DOQ_NO_ERROR (RFC 9250 section 4.3): no error, the connection or stream closes normally.</summary>
    public const long NoError = 0x0;

    /// <summary>DOQ_PROTOCOL_ERROR (RFC 9250 section 4.3): the peer broke the DoQ protocol.</summary>
    public const long ProtocolError = 0x2;

    /// <summary>The Message ID every DoQ query carries (RFC 9250 section 4.2.1).</summary>
    internal const ushort QueryId = 0;

    private const int LengthPrefixLength = 2;

    private const int LargestAnswerLength = LengthPrefixLength + ushort.MaxValue;

    private static readonly DnsRecordType[] QueryTypes = [DnsRecordType.Aaaa, DnsRecordType.A];

    /// <summary>
    /// Resolves <paramref name="host" />: an address literal as itself, any other name through the server.
    /// </summary>
    /// <param name="host">The host name or literal address to resolve.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>
    /// The AAAA answer's addresses, then the A answer's; none when the name cannot be encoded, the channel or
    /// handshake fails, the connection fails, or an answer breaks the protocol.
    /// </returns>
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? literal))
        {
            return [literal];
        }

        byte[][]? queries = EncodeQueries(host);
        return queries is null ? [] : await AskServerAsync(queries, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns the QUIC client settings for <paramref name="server" />: OpenSSL's hello offering only <c>doq</c>.</summary>
    /// <param name="server">The server to ask.</param>
    /// <returns>The settings the QUIC handshake runs with.</returns>
    internal static QuicClientSettings CreateQuicSettings(DnsOverQuicServer server) => new()
    {
        Tls = QuicClientSettings.CreateOpenSslTlsSettings(server.AuthenticationName) with { ApplicationProtocols = [ApplicationProtocol] },
    };

    // One query per record type, each with its length prefix, AAAA first; null for a name RFC 1035 cannot carry.
    private static byte[][]? EncodeQueries(string host)
    {
        try
        {
            return [.. QueryTypes.Select(type => WithLengthPrefix(DnsQueryEncoder.Encode(QueryId, host, type)))];
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static byte[] WithLengthPrefix(byte[] message)
    {
        byte[] prefixed = new byte[LengthPrefixLength + message.Length];
        BinaryPrimitives.WriteUInt16BigEndian(prefixed, (ushort)message.Length);
        message.CopyTo(prefixed, LengthPrefixLength);
        return prefixed;
    }

    private async ValueTask<IReadOnlyList<IPAddress>> AskServerAsync(byte[][] queries, CancellationToken cancellationToken)
    {
        DatagramOpenResult opened = await connector.OpenAsync(server.Host, server.Port, cancellationToken).ConfigureAwait(false);
        if (opened.Channel is not { } channel)
        {
            return [];
        }

        QuicClientConnectionState state = new(CreateQuicSettings(server), SystemTlsRandomSource.Instance, verifier, timeProvider);
        if (await new QuicClientConnector(timeProvider).RunHandshakeAsync(state, channel, null, cancellationToken).ConfigureAwait(false) is not null)
        {
            state.Dispose();
            await channel.DisposeAsync().ConfigureAwait(false);
            return [];
        }

        await using QuicConnection connection = new(state, channel, timeProvider);
        try
        {
            return await ExchangeAsync(connection, queries, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return [];
        }
    }

    // Sends every query on its own stream before reading any answer, then reads the answers in query order.
    private static async ValueTask<IReadOnlyList<IPAddress>> ExchangeAsync(QuicConnection connection, byte[][] queries, CancellationToken cancellationToken)
    {
        List<IMultiplexedStream> streams = [];
        foreach (byte[] query in queries)
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(cancellationToken).ConfigureAwait(false);
            streams.Add(stream);
            await stream.WriteAsync(query, endStream: true, cancellationToken).ConfigureAwait(false);
        }

        List<IPAddress> addresses = [];
        for (int index = 0; index < streams.Count; index++)
        {
            byte[]? message = await ReadAnswerAsync(streams[index], cancellationToken).ConfigureAwait(false);
            if (message is null)
            {
                await connection.CloseAsync(ProtocolError, cancellationToken).ConfigureAwait(false);
                return [];
            }

            addresses.AddRange(DnsAnswerDecoder.Decode(message, QueryId, QueryTypes[index]).Addresses);
        }

        await connection.CloseAsync(NoError, cancellationToken).ConfigureAwait(false);
        return addresses;
    }

    // The answer's DNS message, or null when the stream does not hold exactly one length-prefixed message with
    // Message ID 0 (RFC 9250 sections 4.2, 4.2.1 and 4.3.3).
    // The buffer holds one octet more than the largest framed answer, so an answer that overflows it fails the length
    // check; a read into the full buffer returns no bytes and ends the loop.
    private static async ValueTask<byte[]?> ReadAnswerAsync(IMultiplexedStream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[LargestAnswerLength + 1];
        int filled = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false)) > 0)
        {
            filled += read;
        }

        bool framed = filled >= LengthPrefixLength + LengthPrefixLength
            && BinaryPrimitives.ReadUInt16BigEndian(buffer) == filled - LengthPrefixLength
            && BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(LengthPrefixLength)) == QueryId;
        return framed ? buffer[LengthPrefixLength..filled] : null;
    }
}
