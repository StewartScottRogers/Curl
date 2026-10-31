using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebSocketRepl.Transfer;

/// <summary>A listener the WebSocket REPL never asks for: listening fails the test.</summary>
internal sealed class UnusedListener : IConnectionListener
{
    /// <inheritdoc />
    public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) =>
        throw new AssertFailedException("The WebSocket REPL listened.");
}
