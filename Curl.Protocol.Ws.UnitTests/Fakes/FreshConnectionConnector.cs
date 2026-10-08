using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws.Fakes;

/// <summary>
/// An <see cref="IConnector" /> that builds a new <see cref="ScriptedConnection" /> for every
/// connect, so one handler can run many transfers at once, each against its own server.
/// </summary>
/// <param name="reads">What each new connection's server sends, one read at a time.</param>
public sealed class FreshConnectionConnector(params byte[][] reads) : IConnector
{
    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectResult.Connected(new ScriptedConnection(reads)));
}
