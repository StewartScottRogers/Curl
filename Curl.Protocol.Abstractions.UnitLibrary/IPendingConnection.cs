using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// A bound, listening port waiting for its peer to connect, as returned by
/// <see cref="IConnectionListener.ListenAsync(ListenTarget, CancellationToken)" /> (ADR-0102).
/// </summary>
/// <remarks>
/// Disposing it stops the listening. A connection already accepted is owned by the caller
/// and is not closed by the dispose.
/// </remarks>
public interface IPendingConnection : IAsyncDisposable
{
    /// <summary>
    /// Gets the address and port the listener is bound to: what FTP announces in its
    /// <c>EPRT</c> or <c>PORT</c> command.
    /// </summary>
    EndPoint LocalEndPoint { get; }

    /// <summary>
    /// Waits for the peer to connect and returns the connection.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>
    /// <see cref="ConnectResult.Connected(IConnection)" /> with the accepted connection, or
    /// <see cref="ConnectResult.Failed(CurlExitCode, string)" /> carrying curl's exit code
    /// and message when the accept fails.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled. This is the only exception an
    /// implementation may let escape.
    /// </exception>
    ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken);
}
