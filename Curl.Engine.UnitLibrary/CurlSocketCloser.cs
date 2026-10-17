using Curl.Api.Callbacks;

namespace Curl.Engine;

/// <summary>
/// Calls <c>CURLOPT_CLOSESOCKETFUNCTION</c> for one socket at most once, however many times the connection over
/// it (or the plain connection ending TLS gave) is disposed; with no callback set, closing calls nothing (ADR-0610).
/// </summary>
/// <param name="socket">The socket the callbacks saw.</param>
/// <param name="close">The close-socket callback, or <see langword="null" /> when none is set.</param>
internal sealed class CurlSocketCloser(CurlSocket socket, CloseSocketCallback? close)
{
    private int closed;

    /// <summary>Gets the socket the callbacks saw.</summary>
    internal CurlSocket Socket => socket;

    /// <summary>Calls the close-socket callback with <see cref="Socket" /> the first time; later calls do nothing.</summary>
    internal void Close()
    {
        if (Interlocked.Exchange(ref closed, 1) == 0)
        {
            close?.Invoke(socket);
        }
    }
}
