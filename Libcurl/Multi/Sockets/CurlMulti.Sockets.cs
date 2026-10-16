using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;

namespace Libcurl.Multi;

/// <summary>
/// The socket-action API of a multi handle (ADR-0588): <see cref="SocketActionAsync"/> is
/// <c>curl_multi_socket_action</c>, <see cref="Assign"/> is <c>curl_multi_assign</c>, and
/// <see cref="SetSocketFunction"/> and <see cref="SetTimerFunction"/> set <c>CURLMOPT_SOCKETFUNCTION</c>
/// and <c>CURLMOPT_TIMERFUNCTION</c> for <c>curl_multi_setopt</c>.
/// </summary>
public sealed partial class CurlMulti
{
    /// <summary>
    /// <c>curl_multi_socket_action</c>: acts on an event on one socket, or, given
    /// <see cref="CurlMultiSockets.Timeout"/>, on the expired timer.
    /// </summary>
    /// <param name="socket"><c>s</c>: the socket with the event, or <see cref="CurlMultiSockets.Timeout"/>.</param>
    /// <param name="events"><c>ev_bitmask</c>: the events on the socket; <see cref="CurlSocketSelect.None"/> lets the engine work them out.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The engine's result and <c>running_handles</c>, the transfers still running.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public ValueTask<(CurlMultiCode Code, int RunningHandles)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken = default) =>
        LiveTransfer().SocketActionAsync(socket, events, cancellationToken);

    /// <summary><c>curl_multi_assign</c>: sets the value the socket callback receives as <c>socketp</c> for <paramref name="socket"/>.</summary>
    /// <param name="socket"><c>sockfd</c>: the socket.</param>
    /// <param name="socketState"><c>sockp</c>: the value, or <see langword="null"/> to clear it.</param>
    /// <returns>The engine's result; <see cref="CurlMultiCode.BadSocket"/> for a socket the handle does not know.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMultiCode Assign(CurlSocket socket, object? socketState) =>
        LiveTransfer().Assign(socket, socketState);

    /// <summary>
    /// <c>CURLMOPT_SOCKETFUNCTION</c>: sets the engine's socket callback to one that calls
    /// <paramref name="callback"/> with the <see cref="Easy.CurlEasy"/> added for the engine's transfer, or
    /// clears it for <see langword="null"/>.
    /// </summary>
    /// <param name="callback">The application's socket callback, or <see langword="null"/>.</param>
    /// <returns><see cref="CurlMultiCode.Ok"/>.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    internal CurlMultiCode SetSocketFunction(CurlMultiSocketCallback? callback)
    {
        var live = LiveTransfer();
        live.Options = live.Options with { Socket = callback is null ? null : SocketCallbackOver(callback) };
        return CurlMultiCode.Ok;
    }

    /// <summary>
    /// <c>CURLMOPT_TIMERFUNCTION</c>: sets the engine's timer callback to one that calls
    /// <paramref name="callback"/> with this multi handle, or clears it for <see langword="null"/>.
    /// </summary>
    /// <param name="callback">The application's timer callback, or <see langword="null"/>.</param>
    /// <returns><see cref="CurlMultiCode.Ok"/>.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    internal CurlMultiCode SetTimerFunction(CurlMultiTimerCallback? callback)
    {
        var live = LiveTransfer();
        live.Options = live.Options with { Timer = callback is null ? null : TimerCallbackOver(callback) };
        return CurlMultiCode.Ok;
    }

    private MultiSocketCallback SocketCallbackOver(CurlMultiSocketCallback callback) =>
        (IEasyTransfer easyTransfer, CurlSocket socket, CurlPoll what, object? socketState) =>
            callback(handles[easyTransfer], socket, what, socketState);

    private MultiTimerCallback TimerCallbackOver(CurlMultiTimerCallback callback) =>
        timeoutMilliseconds => callback(this, timeoutMilliseconds);
}
