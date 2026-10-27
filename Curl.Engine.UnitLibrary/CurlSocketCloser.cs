using Curl.Api.Callbacks;

namespace Curl.Engine;

/// <summary>
/// Calls <c>CURLOPT_CLOSESOCKETFUNCTION</c> for one socket at most once, however many times the connection over
/// it (or the plain connection ending TLS gave) is disposed; with no callback set, closing calls nothing (ADR-0610).
/// </summary>
/// <param name="socket">The socket the callbacks saw.</param>
/// <param name="close">The close-socket callback, or <see langword="null" /> when none is set.</param>
/// <param name="isTheConnectionsOwnSocket">
/// Whether <paramref name="socket" /> is the operating system socket the engine opened for the connection, which the
/// callback must see before the connection closes it (CGR-129, ADR-0840).
/// </param>
internal sealed class CurlSocketCloser(CurlSocket socket, CloseSocketCallback? close, bool isTheConnectionsOwnSocket = false)
{
    private int closed;

    /// <summary>Gets the socket the callbacks saw.</summary>
    internal CurlSocket Socket => socket;

    /// <summary>
    /// Gets a value indicating whether <see cref="Socket" /> is the engine's own operating system socket for the
    /// connection, so the close-socket callback runs before the connection is closed rather than after.
    /// </summary>
    internal bool IsTheConnectionsOwnSocket => isTheConnectionsOwnSocket;

    /// <summary>Calls the close-socket callback with <see cref="Socket" /> the first time; later calls do nothing.</summary>
    internal void Close()
    {
        if (Interlocked.Exchange(ref closed, 1) == 0)
        {
            close?.Invoke(socket);
        }
    }
}
