using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Http3;

/// <summary>
/// An <see cref="InMemoryMultiplexedConnection" /> that also hands over the bidirectional
/// streams the peer opens, through <see cref="IPeerBidirectionalStreamAccepting" />, and
/// counts how often it was asked for one.
/// </summary>
internal sealed class InMemoryPeerBidirectionalConnection : IMultiplexedConnection, IPeerBidirectionalStreamAccepting
{
    private readonly Channel<InMemoryMultiplexedStream> peerBidirectionalStreams = Channel.CreateUnbounded<InMemoryMultiplexedStream>();
    private long nextPeerStreamId = 1;
    private int bidirectionalAccepts;

    public InMemoryMultiplexedConnection Inner { get; } = new();

    public int BidirectionalAccepts => Volatile.Read(ref bidirectionalAccepts);

    public EndPoint? RemoteEndPoint => Inner.RemoteEndPoint;

    public EndPoint? LocalEndPoint => Inner.LocalEndPoint;

    public string ApplicationProtocol => Inner.ApplicationProtocol;

    public InMemoryMultiplexedStream AddPeerBidirectionalStream(params byte[] bytes)
    {
        InMemoryMultiplexedStream stream = new InMemoryMultiplexedStream(nextPeerStreamId).Receive(bytes);
        nextPeerStreamId += 4;
        peerBidirectionalStreams.Writer.TryWrite(stream);
        return stream;
    }

    public void FailAcceptingBidirectional(Exception error) => peerBidirectionalStreams.Writer.TryComplete(error);

    public async ValueTask<IMultiplexedStream> AcceptBidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref bidirectionalAccepts);
        // WaitToReadAsync throws the error FailAcceptingBidirectional completed the channel with, unwrapped.
        InMemoryMultiplexedStream? stream;
        while (!peerBidirectionalStreams.Reader.TryRead(out stream))
        {
            await peerBidirectionalStreams.Reader.WaitToReadAsync(cancellationToken);
        }

        return stream;
    }

    public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) => Inner.OpenBidirectionalStreamAsync(cancellationToken);

    public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken) => Inner.OpenUnidirectionalStreamAsync(cancellationToken);

    public ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) => Inner.AcceptUnidirectionalStreamAsync(cancellationToken);

    public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken) => Inner.CloseAsync(applicationErrorCode, cancellationToken);

    public ValueTask DisposeAsync() => Inner.DisposeAsync();
}
