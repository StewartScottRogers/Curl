using System.Buffers.Binary;
using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Http3Server;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Extended.DnsQuery.Encrypted;

/// <summary>
/// A reference DNS over QUIC server written from RFC 9250 (BL-4683) on one <see cref="Http3ServerConnection" />,
/// whose handshake accepts only the ALPN protocol <see cref="ApplicationProtocol" />. It reads each client stream to
/// its FIN, records the length-prefixed query as a <see cref="DoqReceivedQuery" />, and either answers on the same
/// stream with the first reply <paramref name="respond" /> gives, length-prefixed and followed by FIN, or, when
/// <paramref name="closeCode" /> is set, closes the connection with that application error code instead. A reply's
/// <see cref="DnsReferenceReply.Id" /> scripts a non-zero Message ID.
/// </summary>
/// <param name="tlsSettings">The certificate and key the QUIC handshake presents; its ALPN protocol is replaced with <c>doq</c>.</param>
/// <param name="respond">Gives the replies to each query; the first is sent.</param>
/// <param name="random">The randomness of the handshake and the connection id.</param>
/// <param name="closeCode">The application error code to close the connection with on the first complete query, or null to answer.</param>
public sealed class DoqReferenceServer(Http3ServerTlsSettings tlsSettings, DnsReferenceResponder respond, ITlsRandomSource random, ulong? closeCode = null) : IDatagramReferenceServer, IDisposable
{
    /// <summary>The ALPN protocol of DNS over QUIC (RFC 9250 section 4.1).</summary>
    public const string ApplicationProtocol = "doq";

    /// <summary>The UDP port of DNS over QUIC (RFC 9250 section 4.1.1).</summary>
    public const int Port = 853;

    private const int LengthPrefixLength = 2;

    private readonly Http3ServerConnection connection = new(tlsSettings with { ApplicationProtocol = ApplicationProtocol }, random);

    private readonly Lock gate = new();

    private readonly List<(Http3ServerStream Stream, List<byte> Bytes)> openStreams = [];

    private readonly List<DoqReceivedQuery> received = [];

    /// <summary>Gets the queries the client sent, in order.</summary>
    public IReadOnlyList<DoqReceivedQuery> Received
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    /// <summary>Gets a value indicating whether the QUIC handshake completed.</summary>
    public bool IsHandshakeComplete => connection.IsHandshakeComplete;

    /// <summary>Gets the CONNECTION_CLOSE the client sent, or null when it sent none.</summary>
    public QuicConnectionCloseFrame? ClientClose => connection.PeerClose;

    /// <inheritdoc />
    public IReadOnlyList<ReadOnlyMemory<byte>> Answer(ReadOnlyMemory<byte> datagram)
    {
        lock (gate)
        {
            List<byte[]> toSend = [.. connection.Receive(datagram)];
            if (connection.IsHandshakeComplete && !connection.IsClosed)
            {
                toSend.AddRange(Serve());
            }

            return [.. toSend.Select(bytes => new ReadOnlyMemory<byte>(bytes))];
        }
    }

    /// <inheritdoc />
    public void Dispose() => connection.Dispose();

    private static byte[] Framed(byte[] message)
    {
        byte[] framed = new byte[LengthPrefixLength + message.Length];
        BinaryPrimitives.WriteUInt16BigEndian(framed, (ushort)message.Length);
        message.CopyTo(framed, LengthPrefixLength);
        return framed;
    }

    private IReadOnlyList<byte[]> Serve()
    {
        while (connection.AcceptStream() is { } stream)
        {
            openStreams.Add((stream, []));
        }

        foreach ((Http3ServerStream stream, List<byte> bytes) in openStreams.ToList())
        {
            bytes.AddRange(stream.Read());
            if (!stream.IsReadComplete)
            {
                continue;
            }

            openStreams.RemoveAll(open => open.Stream == stream);
            if (AnswerQuery(stream.Id, [.. bytes]) is { } close)
            {
                return close;
            }
        }

        return connection.TakeDatagramsToSend();
    }

    // Records the query and answers it on its stream; returns the datagrams of the close instead when one is scripted.
    private IReadOnlyList<byte[]>? AnswerQuery(ulong streamId, byte[] streamBytes)
    {
        DnsReferenceQuery query = DnsReferenceQuery.Decode(streamBytes.AsSpan(LengthPrefixLength));
        received.Add(new DoqReceivedQuery(streamId, streamBytes, query));
        if (closeCode is { } code)
        {
            return connection.Close(code);
        }

        connection.Write(streamId, Framed(DnsReferenceMessageWriter.WriteReply(query, respond(query, received.Count - 1)[0])), endStream: true);
        return null;
    }
}
