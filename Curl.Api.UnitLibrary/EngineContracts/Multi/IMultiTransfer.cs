using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;

namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// One multi handle of an <see cref="ICurlEngine"/> (ADR-0506): many easy transfers driven at once,
/// by <see cref="PerformAsync"/> and a wait, or by the socket-action API and its callbacks.
/// <see cref="IAsyncDisposable.DisposeAsync"/> is <c>curl_multi_cleanup</c>. <c>curl_multi_fdset</c>
/// has no member: an <c>fd_set</c> means nothing to .NET, so <c>Libcurl</c> answers it from
/// <see cref="GetWaitSockets"/> (phase 3 decides how).
/// </summary>
public interface IMultiTransfer : IAsyncDisposable
{
    /// <summary>
    /// <c>curl_multi_setopt</c>: every option of the handle. Setting it replaces them all; one option
    /// changes with a <see langword="with"/> expression on the current value.
    /// </summary>
    MultiOptions Options { get; set; }

    /// <summary><c>curl_multi_add_handle</c>: add a transfer, which starts on the next perform.</summary>
    /// <param name="transfer">The transfer to add.</param>
    /// <returns>The call's result; <see cref="CurlMultiCode.AddedAlready"/> when it is in a multi handle already.</returns>
    CurlMultiCode Add(IEasyTransfer transfer);

    /// <summary><c>curl_multi_remove_handle</c>: remove a transfer, stopping it if it still runs.</summary>
    /// <param name="transfer">The transfer to remove.</param>
    /// <returns>The call's result.</returns>
    CurlMultiCode Remove(IEasyTransfer transfer);

    /// <summary><c>curl_multi_perform</c>: move every transfer on as far as it can go without waiting.</summary>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The call's result and <c>running_handles</c>, the transfers still running.</returns>
    ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken);

    /// <summary><c>curl_multi_wait</c>: wait until a transfer's socket or an extra socket has an event, or the timeout.</summary>
    /// <param name="extraSockets"><c>extra_fds</c>: more sockets to wait on; the wait sets each one's <see cref="CurlWaitSocket.ReturnedEvents"/>.</param>
    /// <param name="timeout"><c>timeout_ms</c>: the longest wait.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The call's result and <c>numfds</c>, the sockets with an event.</returns>
    ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary><c>curl_multi_poll</c>: as <see cref="WaitAsync"/>, but it waits the whole timeout with nothing to wait on, and <see cref="Wakeup"/> ends it.</summary>
    /// <param name="extraSockets"><c>extra_fds</c>: more sockets to wait on; the wait sets each one's <see cref="CurlWaitSocket.ReturnedEvents"/>.</param>
    /// <param name="timeout"><c>timeout_ms</c>: the longest wait.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The call's result and <c>numfds</c>, the sockets with an event.</returns>
    ValueTask<(CurlMultiCode Code, int ReadySockets)> PollAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary><c>curl_multi_wakeup</c>: end a <see cref="PollAsync"/> now, from any thread.</summary>
    /// <returns>The call's result.</returns>
    CurlMultiCode Wakeup();

    /// <summary><c>curl_multi_info_read</c>: take the next message, if there is one.</summary>
    /// <param name="message">The message, or <see langword="null"/> when none waits.</param>
    /// <param name="remainingMessages"><c>msgs_in_queue</c>: the messages still waiting after this one.</param>
    /// <returns><see langword="true"/> when a message was taken.</returns>
    bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages);

    /// <summary><c>curl_multi_socket_action</c>: act on an event on one socket, or on an expired timer.</summary>
    /// <param name="socket"><c>s</c>: the socket with the event, or <see cref="CurlMultiSockets.Timeout"/> for the timer.</param>
    /// <param name="events"><c>ev_bitmask</c>: the events on the socket.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The call's result and <c>running_handles</c>, the transfers still running.</returns>
    ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken);

    /// <summary><c>curl_multi_assign</c>: the value the socket callback receives for a socket.</summary>
    /// <param name="socket"><c>sockfd</c>: the socket.</param>
    /// <param name="socketState"><c>sockp</c>: the value, or <see langword="null"/> to clear it.</param>
    /// <returns>The call's result; <see cref="CurlMultiCode.BadSocket"/> for a socket the handle does not know.</returns>
    CurlMultiCode Assign(CurlSocket socket, object? socketState);

    /// <summary><c>curl_multi_timeout</c>: how long to wait before the next perform or timer action.</summary>
    /// <returns>The call's result and the milliseconds to wait, or -1 when no timeout is set.</returns>
    (CurlMultiCode Code, long TimeoutMilliseconds) GetTimeout();

    /// <summary><c>curl_multi_get_handles</c>: every transfer added and not yet removed.</summary>
    /// <returns>The transfers, in no set order.</returns>
    IReadOnlyList<IEasyTransfer> GetHandles();

    /// <summary><c>curl_multi_waitfds</c>: the sockets and events the handle waits on.</summary>
    /// <param name="sockets"><c>ufds</c>: filled with up to its length of sockets.</param>
    /// <param name="socketCount"><c>fd_count</c>: how many sockets the handle waits on, even past <paramref name="sockets"/>' length.</param>
    /// <returns>The call's result; <see cref="CurlMultiCode.OutOfMemory"/> when <paramref name="sockets"/> is too short.</returns>
    CurlMultiCode GetWaitSockets(Span<CurlWaitSocket> sockets, out int socketCount);

    /// <summary><c>curl_multi_get_offt</c>: one of the handle's transfer counts.</summary>
    /// <param name="info"><c>info</c>: the count to report.</param>
    /// <returns>The call's result and the count.</returns>
    (CurlMultiCode Code, long Value) GetInfo(CurlMultiInfo info);

    /// <summary><c>curl_multi_notify_enable</c>: send a notification to <see cref="MultiOptions.Notify"/>.</summary>
    /// <param name="notification">The notification to switch on.</param>
    /// <returns>The call's result.</returns>
    CurlMultiCode EnableNotification(CurlMultiNotification notification);

    /// <summary><c>curl_multi_notify_disable</c>: stop sending a notification.</summary>
    /// <param name="notification">The notification to switch off.</param>
    /// <returns>The call's result.</returns>
    CurlMultiCode DisableNotification(CurlMultiNotification notification);
}
