using System.Collections.Concurrent;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// The stand-in for upstream's nghttpx in front of <c>sws</c> (ADR-0491): every connection is an
/// in-memory pipe whose server end is an <see cref="Http2ServerSession"/> relaying each HTTP/2
/// stream to <paramref name="backend"/> as HTTP/1.1.
/// </summary>
/// <param name="backend">The HTTP/1.1 server, the sws emulation in the case runner.</param>
internal sealed class Http2ServerConnector(IConnector backend) : IConnector
{
    private readonly ConcurrentQueue<Task> sessions = new();

    /// <summary>
    /// Gets each connection's session so far, in connection order: a task that ends when the
    /// session has sent its GOAWAY and closed.
    /// </summary>
    internal IReadOnlyList<Task> Sessions => [.. sessions];

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        (InMemoryDuplexStream client, InMemoryDuplexStream server) = InMemoryDuplexStream.CreatePair();
        sessions.Enqueue(Task.Run(() => new Http2ServerSession(server, backend, target).RunAsync(CancellationToken.None), CancellationToken.None));
        return ValueTask.FromResult(ConnectResult.Connected(new Http2ServerClientConnection(client)));
    }
}
