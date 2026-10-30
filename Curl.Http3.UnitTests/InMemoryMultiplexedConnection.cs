using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Http3;

/// <summary>
/// An <see cref="IMultiplexedConnection" /> in memory: it numbers the streams it opens as a
/// QUIC client does (bidirectional 0, 4, 8; unidirectional 2, 6, 10), hands out the peer
/// streams the test adds, and records how it was closed and whether it was disposed.
/// </summary>
internal sealed class InMemoryMultiplexedConnection : IMultiplexedConnection
{
    private readonly Channel<InMemoryMultiplexedStream> peerStreams = Channel.CreateUnbounded<InMemoryMultiplexedStream>();
    private long nextPeerStreamId = 3;

    public EndPoint? RemoteEndPoint => null;

    public EndPoint? LocalEndPoint => null;

    public string ApplicationProtocol => "h3";

    public long? BidirectionalStreamLimit { get; init; }

    public List<InMemoryMultiplexedStream> BidirectionalStreams { get; } = [];

    public List<InMemoryMultiplexedStream> UnidirectionalStreams { get; } = [];

    /// <summary>Gets or sets the exception the next bidirectional open throws, or <see langword="null" />.</summary>
    public Exception? BidirectionalOpenFailure { get; set; }

    /// <summary>Gets or sets the exception closing throws, or <see langword="null" />.</summary>
    public Exception? CloseFailure { get; set; }

    public long? ClosedWith { get; private set; }

    public bool IsDisposed { get; private set; }

    /// <summary>Adds a peer unidirectional stream that will receive <paramref name="bytes" />.</summary>
    public InMemoryMultiplexedStream AddPeerStream(params byte[] bytes)
    {
        InMemoryMultiplexedStream stream = new InMemoryMultiplexedStream(nextPeerStreamId).Receive(bytes);
        nextPeerStreamId += 4;
        peerStreams.Writer.TryWrite(stream);
        return stream;
    }

    /// <summary>Makes accepting the next peer stream fail with <paramref name="error" />.</summary>
    public void FailAccepting(Exception error) => peerStreams.Writer.TryComplete(error);

    public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        if (BidirectionalOpenFailure is { } failure)
        {
            throw failure;
        }

        return Open(BidirectionalStreams, 0);
    }

    public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken) => Open(UnidirectionalStreams, 2);

    public async ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) =>
        peerStreams.Reader.TryRead(out InMemoryMultiplexedStream? ready) ? ready : await peerStreams.Reader.WaitToReadAsync(cancellationToken) && peerStreams.Reader.TryRead(out InMemoryMultiplexedStream? stream) ? stream : throw new NotSupportedException();

    public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken)
    {
        ClosedWith = applicationErrorCode;
        return CloseFailure is { } failure ? ValueTask.FromException(failure) : ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    private static ValueTask<IMultiplexedStream> Open(List<InMemoryMultiplexedStream> streams, long firstStreamId)
    {
        InMemoryMultiplexedStream stream = new(firstStreamId + (4 * streams.Count));
        streams.Add(stream);
        return ValueTask.FromResult<IMultiplexedStream>(stream);
    }
}
