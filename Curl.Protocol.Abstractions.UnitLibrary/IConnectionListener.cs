namespace Curl.Protocol.Abstractions;

/// <summary>
/// Opens a listening port a peer connects back to: the seam FTP's active mode (<c>-P</c>)
/// acquires its data connection through, so a handler never constructs a
/// <see cref="System.Net.Sockets.Socket" /> itself (ADR-0102).
/// </summary>
/// <remarks>
/// A handler takes a listener in its constructor, as it takes an <see cref="IConnector" />,
/// and asks it for a port once it knows the address to announce. The handler owns and
/// disposes the <see cref="IPendingConnection" /> it is given, which stops the listening.
/// </remarks>
public interface IConnectionListener
{
    /// <summary>
    /// Binds a listening port on <see cref="ListenTarget.Address" /> within
    /// <paramref name="target" />'s port range.
    /// </summary>
    /// <param name="target">The address to bind and the ports that may be used.</param>
    /// <param name="cancellationToken">Cancels the bind.</param>
    /// <returns>
    /// <see cref="ListenResult.Listening(IPendingConnection)" /> with the bound port, or
    /// <see cref="ListenResult.Failed(CurlExitCode, string)" /> carrying curl's exit code and
    /// the message curl prints when no port in the range can be bound.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled. This is the only exception an
    /// implementation may let escape; every bind failure is returned as a failed result
    /// instead.
    /// </exception>
    ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken);
}
