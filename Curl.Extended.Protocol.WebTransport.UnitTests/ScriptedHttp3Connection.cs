using System.Net;
using System.Threading.Channels;
using Curl.Extended.Protocol.WebTransport.Wire;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.WebTransport;

/// <summary>
/// An HTTP/3 connection in memory that plays a WebTransport server: its control stream carries
/// <see cref="ServerSettings" />, the CONNECT stream (stream 0) answers with <see cref="ConnectAnswer" />,
/// and each stream or datagram the client sends goes to <see cref="Answer" />. It numbers client streams
/// as QUIC does (bidirectional 0, 4; unidirectional 2, 6, 10) and keeps what the client sent.
/// </summary>
internal sealed class ScriptedHttp3Connection : IMultiplexedConnection, IDatagramCarryingConnection
{
    private readonly Channel<InMemoryMultiplexedStream> peerStreams = Channel.CreateUnbounded<InMemoryMultiplexedStream>();

    private readonly Channel<ReadOnlyMemory<byte>> peerDatagrams = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();

    private long nextPeerStreamId = 3;

    /// <summary>Gets the SETTINGS the server sends, or <see langword="null" /> to end its streams before any.</summary>
    public IReadOnlyList<Http3Setting>? ServerSettings { get; init; } =
    [
        new(Http3SettingIdentifier.EnableConnectProtocol, 1),
        new(Http3SettingIdentifier.H3Datagram, 1),
        new(WebTransportWire.EnabledSetting, 1),
    ];

    /// <summary>Gets the bytes the CONNECT stream receives; by default a HEADERS frame with <c>:status 200</c>.</summary>
    public byte[] ConnectAnswer { get; init; } = Headers("200");

    /// <summary>Gets what the server does with a finished client stream or a datagram; by default nothing.</summary>
    public Action<ScriptedHttp3Connection, InMemoryMultiplexedStream>? AnswerStream { get; init; }

    /// <summary>Gets the datagrams the server sends once the client sent one.</summary>
    public IReadOnlyList<byte[]> DatagramAnswers { get; init; } = [];

    /// <summary>Gets the most bytes one datagram may carry.</summary>
    public int MaxDatagramPayloadLength { get; init; } = 1100;

    /// <summary>Gets the exception the client's bidirectional open throws after the CONNECT stream, or <see langword="null" />.</summary>
    public Exception? DataStreamOpenFailure { get; init; }

    public List<InMemoryMultiplexedStream> BidirectionalStreams { get; } = [];

    public List<InMemoryMultiplexedStream> UnidirectionalStreams { get; } = [];

    public List<byte[]> SentDatagrams { get; } = [];

    public long? ClosedWith { get; private set; }

    public bool IsDisposed { get; private set; }

    public EndPoint? RemoteEndPoint => null;

    public EndPoint? LocalEndPoint => null;

    public string ApplicationProtocol => "h3";

    /// <summary>Encodes a HEADERS frame carrying <c>:status</c> <paramref name="status" />.</summary>
    public static byte[] Headers(string status) =>
        new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(0, [new HeaderField(":status", status)])).ToBytes();

    /// <summary>Opens a server unidirectional stream that will receive <paramref name="bytes" /> and end.</summary>
    public InMemoryMultiplexedStream AddPeerStream(params byte[] bytes) => AddPeerStream(bytes, end: true);

    private InMemoryMultiplexedStream AddPeerStream(byte[] bytes, bool end)

    {
        InMemoryMultiplexedStream stream = new InMemoryMultiplexedStream(nextPeerStreamId).Receive(bytes);
        nextPeerStreamId += 4;
        if (end)
        {
            stream.EndReceiving();
        }

        peerStreams.Writer.TryWrite(stream);
        return stream;
    }

    public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        InMemoryMultiplexedStream stream = new(4 * BidirectionalStreams.Count);
        if (BidirectionalStreams.Count == 0)
        {
            StartServer(stream);
        }
        else if (DataStreamOpenFailure is { } failure)
        {
            throw failure;
        }

        BidirectionalStreams.Add(stream);
        return ValueTask.FromResult<IMultiplexedStream>(new AnsweringStream(this, stream));
    }

    public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        InMemoryMultiplexedStream stream = new(2 + (4 * UnidirectionalStreams.Count));
        UnidirectionalStreams.Add(stream);
        return ValueTask.FromResult<IMultiplexedStream>(UnidirectionalStreams.Count > 3 ? new AnsweringStream(this, stream) : stream);
    }

    public async ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) =>
        await peerStreams.Reader.WaitToReadAsync(cancellationToken) && peerStreams.Reader.TryRead(out InMemoryMultiplexedStream? stream) ? stream : throw new NotSupportedException();

    public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        SentDatagrams.Add(datagram.ToArray());
        foreach (byte[] answer in DatagramAnswers)
        {
            peerDatagrams.Writer.TryWrite(answer);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveDatagramAsync(CancellationToken cancellationToken) =>
        await peerDatagrams.Reader.ReadAsync(cancellationToken);

    public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken)
    {
        ClosedWith = applicationErrorCode;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    private void StartServer(InMemoryMultiplexedStream connect)
    {
        if (ServerSettings is null)
        {
            peerStreams.Writer.TryComplete();
            return;
        }

        AddPeerStream([0x00, .. new Http3SettingsFrame(ServerSettings).ToBytes()], end: false);
        connect.Receive(ConnectAnswer);
    }

    // A client data stream: once the client ends it, the script answers.
    private sealed class AnsweringStream(ScriptedHttp3Connection connection, InMemoryMultiplexedStream inner) : IMultiplexedStream
    {
        public long StreamId => inner.StreamId;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, bool endStream, CancellationToken cancellationToken)
        {
            await inner.WriteAsync(buffer, endStream, cancellationToken);
            if (endStream && inner.StreamId != 0)
            {
                connection.AnswerStream?.Invoke(connection, inner);
            }
        }

        public void Abort(long applicationErrorCode) => inner.Abort(applicationErrorCode);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
