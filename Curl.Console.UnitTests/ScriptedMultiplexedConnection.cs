using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// An in-memory QUIC connection: its request streams answer with scripted bytes, its
/// unidirectional streams take whatever the client writes, and it records how it was closed.
/// </summary>
/// <param name="requestStreams">
/// The streams the bidirectional opens return, in order; the last is returned again for every
/// open after it.
/// </param>
internal sealed class ScriptedMultiplexedConnection(params ScriptedMultiplexedStream[] requestStreams) : IMultiplexedConnection
{
    private readonly Queue<ScriptedMultiplexedStream> pendingRequestStreams = new(requestStreams);

    private long nextUnidirectionalStreamId = 2;

    public EndPoint? RemoteEndPoint { get; init; }

    public EndPoint? LocalEndPoint { get; init; }

    public string ApplicationProtocol => "h3";

    /// <summary>Gets the server's MAX_STREAMS for bidirectional streams the test gives, or <see langword="null" />.</summary>
    public long? BidirectionalStreamLimit { get; init; }

    /// <summary>Gets how many times the connection was closed.</summary>
    public int CloseCount { get; private set; }

    /// <summary>Gets the application error code the connection was closed with, or <see langword="null" />.</summary>
    public long? CloseCode { get; private set; }

    public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IMultiplexedStream>(pendingRequestStreams.Count > 1 ? pendingRequestStreams.Dequeue() : pendingRequestStreams.Peek());

    public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        ScriptedMultiplexedStream stream = new(nextUnidirectionalStreamId, []);
        nextUnidirectionalStreamId += 4;
        return ValueTask.FromResult<IMultiplexedStream>(stream);
    }

    public ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken)
    {
        CloseCode = applicationErrorCode;
        CloseCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
