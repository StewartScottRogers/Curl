using System.Net;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebSocketExtendedConnect.UnitTests.Http3;

/// <summary>
/// An HTTP/3 connection in memory: the server's unidirectional streams are queued by the test, and the first
/// bidirectional stream the client opens is <see cref="RequestStream" />, so its response can be queued in advance.
/// </summary>
internal sealed class InMemoryMultiplexedConnection : IMultiplexedConnection
{
    private readonly Channel<InMemoryMultiplexedStream> peerStreams = Channel.CreateUnbounded<InMemoryMultiplexedStream>();
    private long nextLocalUnidirectionalStreamId = 2;

    public EndPoint? RemoteEndPoint => null;

    public EndPoint? LocalEndPoint => null;

    public string ApplicationProtocol => "h3";

    public InMemoryMultiplexedStream RequestStream { get; } = new(0);

    public bool IsDisposed { get; private set; }

    public void AddPeerStream(params byte[] bytes) => peerStreams.Writer.TryWrite(new InMemoryMultiplexedStream(3).Receive(bytes));

    public void FailAccepting(Exception error) => peerStreams.Writer.TryComplete(error);

    public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IMultiplexedStream>(RequestStream);

    public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        InMemoryMultiplexedStream stream = new(nextLocalUnidirectionalStreamId);
        nextLocalUnidirectionalStreamId += 4;
        return ValueTask.FromResult<IMultiplexedStream>(stream);
    }

    public async ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) =>
        await peerStreams.Reader.WaitToReadAsync(cancellationToken) && peerStreams.Reader.TryRead(out InMemoryMultiplexedStream? stream) ? stream : throw new NotSupportedException();

    public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
