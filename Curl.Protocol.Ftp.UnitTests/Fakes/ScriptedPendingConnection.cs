using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="IPendingConnection" /> bound to <paramref name="localEndPoint" /> whose accept
/// returns <paramref name="accepted" />, or, when that is <see langword="null" />, waits until
/// its token is cancelled, as a server that never connects back leaves it.
/// </summary>
/// <param name="localEndPoint">The address and port it reports being bound to.</param>
/// <param name="accepted">What the accept returns; <see langword="null" /> for a peer that never connects.</param>
public sealed class ScriptedPendingConnection(IPEndPoint localEndPoint, ConnectResult? accepted) : IPendingConnection
{
    /// <inheritdoc />
    public EndPoint LocalEndPoint => localEndPoint;

    /// <summary>Gets a value indicating whether the listening was stopped.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets a value indicating whether the accept was awaited.</summary>
    public bool WasAccepted { get; private set; }

    /// <inheritdoc />
    public async ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken)
    {
        WasAccepted = true;
        if (accepted is null)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return accepted!;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
