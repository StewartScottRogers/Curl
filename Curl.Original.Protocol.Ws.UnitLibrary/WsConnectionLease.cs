using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ws;

/// <summary>
/// Closes a transfer's connection when the transfer is done with it, unless the upgrade handed it
/// back to the caller open (<see cref="WsProtocolHandler.ConnectAndUpgradeOnlyAsync" />, BL-2471).
/// </summary>
/// <param name="connection">The connection to close.</param>
internal sealed class WsConnectionLease(IConnection connection) : IAsyncDisposable
{
    /// <summary>Gets or sets a value indicating whether the caller now owns the connection, so it is left open.</summary>
    internal bool IsHandedBack { get; set; }

    /// <summary>Closes the connection unless it was handed back.</summary>
    /// <returns>A task that completes once the connection is closed, or at once when it was handed back.</returns>
    public ValueTask DisposeAsync() => IsHandedBack ? ValueTask.CompletedTask : connection.DisposeAsync();
}
