using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// An in-memory QUIC connection: its one request stream answers with scripted bytes, its
/// unidirectional streams take whatever the client writes, and it records how it was closed.
/// </summary>
/// <param name="requestStream">The stream the first bidirectional open returns.</param>
internal sealed class ScriptedMultiplexedConnection(ScriptedMultiplexedStream requestStream) : IMultiplexedConnection
{
    private long nextUnidirectionalStreamId = 2;

    public EndPoint? RemoteEndPoint { get; init; }

    public EndPoint? LocalEndPoint { get; init; }

    public string ApplicationProtocol => "h3";

    /// <summary>Gets the application error code the connection was closed with, or <see langword="null" />.</summary>
    public long? CloseCode { get; private set; }

    public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IMultiplexedStream>(requestStream);

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
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
