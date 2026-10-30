using System.Buffers.Binary;
using Curl.Extension.DnsQuery.Client;
using Curl.Protocol.Abstractions;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Extension.DnsQuery.Transport.Quic;

/// <summary>
/// Sends a query over DNS over QUIC (RFC 9250): opens a QUIC connection to the server, port 853 by default, offering
/// only the ALPN token <c>doq</c> (section 4.1.1) and checking the certificate through curl's TLS provider, then sends
/// the query on one client bidirectional stream with DNS Message ID 0 and the two-octet length prefix, ending the
/// stream with FIN (sections 4.2 and 4.2.1). The answer is handed back with the query's own message ID, so the caller
/// matches it as for any transport. An answer whose Message ID is not 0 or that is not exactly one length-prefixed
/// message closes the connection with DOQ_PROTOCOL_ERROR and is exit 8; otherwise the connection closes with
/// DOQ_NO_ERROR (sections 4.3 and 4.5).
/// </summary>
/// <param name="connector">Opens the UDP datagram channel to the server.</param>
/// <param name="tlsProvider">Judges the certificate chain the server presents.</param>
internal sealed class DnsQuicTransport(IDatagramConnector connector, ITlsProvider tlsProvider) : IDnsQueryTransport
{
    /// <summary>The ALPN token RFC 9250 section 4.1.1 assigns to DNS over QUIC.</summary>
    public const string ApplicationProtocol = "doq";

    /// <summary>The port RFC 9250 section 4.1.1 assigns to DNS over QUIC.</summary>
    public const int Port = 853;

    /// <summary>DOQ_NO_ERROR (RFC 9250 section 4.3): the connection closes normally.</summary>
    public const long NoError = 0x0;

    /// <summary>DOQ_PROTOCOL_ERROR (RFC 9250 section 4.3): the server broke the DoQ protocol.</summary>
    public const long ProtocolError = 0x2;

    private const int LengthPrefixLength = 2;

    private const int MessageIdLength = 2;

    private const int LargestAnswerLength = LengthPrefixLength + ushort.MaxValue;

    /// <inheritdoc />
    public int DefaultPort => Port;

    /// <summary>Returns the QUIC client settings for <paramref name="host" />: OpenSSL's hello offering only <c>doq</c>.</summary>
    /// <param name="host">The server's host, sent as the TLS server name.</param>
    /// <returns>The settings the QUIC handshake runs with.</returns>
    public static QuicClientSettings CreateQuicSettings(string host) => new()
    {
        Tls = QuicClientSettings.CreateOpenSslTlsSettings(host) with { ApplicationProtocols = [ApplicationProtocol] },
    };

    /// <inheritdoc />
    public async ValueTask<byte[]> ExchangeAsync(string host, int port, byte[] query, ITransferContext context)
    {
        DatagramOpenResult open = await connector.OpenAsync(host, port, context.CancellationToken).ConfigureAwait(false);
        if (open.Channel is not { } channel)
        {
            throw new DnsQueryException(open.ExitCode, open.ErrorMessage!);
        }

        QuicClientConnectionState state = new(CreateQuicSettings(host), SystemTlsRandomSource.Instance, new DnsQuicCertificateVerifier(tlsProvider, host), context.TimeProvider);
        if (await new QuicClientConnector(context.TimeProvider).RunHandshakeAsync(state, channel, null, context.CancellationToken).ConfigureAwait(false) is { } failure)
        {
            state.Dispose();
            await channel.DisposeAsync().ConfigureAwait(false);
            throw new DnsQueryException(failure.ExitCode, failure.Message);
        }

        QuicConnection connection = new(state, channel, context.TimeProvider);
        await using (connection.ConfigureAwait(false))
        {
            byte[] answer = await AskAsync(connection, query, context.CancellationToken).ConfigureAwait(false);
            query.AsSpan(0, MessageIdLength).CopyTo(answer);
            return answer;
        }
    }

    private static async ValueTask<byte[]> AskAsync(QuicConnection connection, byte[] query, CancellationToken cancellationToken)
    {
        byte[]? answer;
        try
        {
            IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(Framed(query), endStream: true, cancellationToken).ConfigureAwait(false);
            answer = await ReadAnswerAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new DnsQueryException(CurlExitCode.RecvError, $"The DNS-over-QUIC server did not answer: {exception.Message}");
        }

        await connection.CloseAsync(answer is null ? ProtocolError : NoError, cancellationToken).ConfigureAwait(false);
        return answer ?? throw new DnsQueryException(CurlExitCode.WeirdServerReply, "The DNS-over-QUIC server's answer was not one length-prefixed message with Message ID 0");
    }

    // The query with Message ID 0 (RFC 9250 section 4.2.1) behind its two-octet length prefix (section 4.2).
    private static byte[] Framed(byte[] query)
    {
        byte[] framed = new byte[LengthPrefixLength + query.Length];
        BinaryPrimitives.WriteUInt16BigEndian(framed, (ushort)query.Length);
        query.AsSpan(MessageIdLength).CopyTo(framed.AsSpan(LengthPrefixLength + MessageIdLength));
        return framed;
    }

    // The answer's DNS message, or null when the stream does not hold exactly one length-prefixed message with
    // Message ID 0 (RFC 9250 sections 4.2, 4.2.1 and 4.3.3). The buffer holds one octet more than the largest framed
    // answer, so an answer that overflows it fails the length check; a read into the full buffer returns no octets.
    private static async ValueTask<byte[]?> ReadAnswerAsync(IMultiplexedStream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[LargestAnswerLength + 1];
        int filled = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false)) > 0)
        {
            filled += read;
        }

        bool framed = filled >= LengthPrefixLength + MessageIdLength
            && BinaryPrimitives.ReadUInt16BigEndian(buffer) == filled - LengthPrefixLength
            && BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(LengthPrefixLength)) == 0;
        return framed ? buffer[LengthPrefixLength..filled] : null;
    }
}
