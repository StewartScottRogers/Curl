using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;

namespace Libcurl.Multi;

/// <summary>
/// The wait area's part of <see cref="CurlMulti"/> (ADR-0584): <c>curl_multi_wait</c>, <c>_poll</c>,
/// <c>_wakeup</c>, <c>_fdset</c> and <c>_timeout</c>, each over the engine's <see cref="IMultiTransfer"/>.
/// </summary>
public sealed partial class CurlMulti
{
    /// <summary>
    /// <c>curl_multi_wait</c>: waits until a transfer's socket or one of <paramref name="extraSockets"/>
    /// has an event, or <paramref name="timeoutMilliseconds"/> pass; with nothing to wait on it returns at once.
    /// </summary>
    /// <param name="extraSockets"><c>extra_fds</c>: more sockets to wait on; the wait sets each one's <see cref="CurlWaitSocket.ReturnedEvents"/>.</param>
    /// <param name="timeoutMilliseconds"><c>timeout_ms</c>: the longest wait.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.BadFunctionArgument"/> and 0 for a negative timeout, without calling the
    /// engine, as libcurl's <c>multi_wait</c> does; else the engine's result and <c>numfds</c>, the sockets with an event.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitAsync(Memory<CurlWaitSocket> extraSockets, int timeoutMilliseconds, CancellationToken cancellationToken = default)
    {
        var live = LiveTransfer();
        return timeoutMilliseconds < 0
            ? ValueTask.FromResult((CurlMultiCode.BadFunctionArgument, 0))
            : live.WaitAsync(extraSockets, TimeSpan.FromMilliseconds(timeoutMilliseconds), cancellationToken);
    }

    /// <summary>
    /// <c>curl_multi_poll</c>: as <see cref="WaitAsync"/>, but with nothing to wait on it waits the whole
    /// timeout, and <see cref="Wakeup"/> ends it early.
    /// </summary>
    /// <param name="extraSockets"><c>extra_fds</c>: more sockets to wait on; the wait sets each one's <see cref="CurlWaitSocket.ReturnedEvents"/>.</param>
    /// <param name="timeoutMilliseconds"><c>timeout_ms</c>: the longest wait.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.BadFunctionArgument"/> and 0 for a negative timeout, without calling the
    /// engine; else the engine's result and <c>numfds</c>, the sockets with an event.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public ValueTask<(CurlMultiCode Code, int ReadySockets)> PollAsync(Memory<CurlWaitSocket> extraSockets, int timeoutMilliseconds, CancellationToken cancellationToken = default)
    {
        var live = LiveTransfer();
        return timeoutMilliseconds < 0
            ? ValueTask.FromResult((CurlMultiCode.BadFunctionArgument, 0))
            : live.PollAsync(extraSockets, TimeSpan.FromMilliseconds(timeoutMilliseconds), cancellationToken);
    }

    /// <summary><c>curl_multi_wakeup</c>: ends a <see cref="PollAsync"/> now; callable from any thread.</summary>
    /// <returns>The engine's result.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMultiCode Wakeup() => LiveTransfer().Wakeup();

    /// <summary><c>curl_multi_timeout</c>: how long to wait before the next perform.</summary>
    /// <returns>The engine's result and the milliseconds to wait, -1 when no timeout is set.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public (CurlMultiCode Code, long TimeoutMilliseconds) Timeout() => LiveTransfer().GetTimeout();

    /// <summary>
    /// <c>curl_multi_fdset</c>: adds every socket the handle waits on to the sets for its events, from the
    /// engine's <c>curl_multi_waitfds</c> list. A set is any <see cref="ISet{T}"/> of sockets, the
    /// <c>fd_set</c> of .NET; like <c>FD_SET</c>, sockets are added and none is removed.
    /// </summary>
    /// <param name="readSockets"><c>read_fd_set</c>: gets each socket waiting for <see cref="CurlWaitEvents.PollIn"/>; <see langword="null"/> skips it.</param>
    /// <param name="writeSockets"><c>write_fd_set</c>: gets each socket waiting for <see cref="CurlWaitEvents.PollOut"/>; <see langword="null"/> skips it.</param>
    /// <param name="exceptionSockets"><c>exc_fd_set</c>: gets each socket waiting for <see cref="CurlWaitEvents.PollPriority"/>, which libcurl's own sockets never do; <see langword="null"/> skips it.</param>
    /// <param name="maxSocket"><c>max_fd</c>: the highest socket added, or -1 when there is none.</param>
    /// <returns>The engine's result; on any but <see cref="CurlMultiCode.Ok"/> no set is changed and <paramref name="maxSocket"/> is -1.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMultiCode FdSet(ISet<CurlSocket>? readSockets, ISet<CurlSocket>? writeSockets, ISet<CurlSocket>? exceptionSockets, out nint maxSocket)
    {
        maxSocket = -1;
        var code = ReadWaitSockets(LiveTransfer(), out var sockets);
        if (code != CurlMultiCode.Ok)
        {
            return code;
        }

        foreach (var socket in sockets)
        {
            AddWhenWaitingFor(readSockets, socket, CurlWaitEvents.PollIn);
            AddWhenWaitingFor(writeSockets, socket, CurlWaitEvents.PollOut);
            AddWhenWaitingFor(exceptionSockets, socket, CurlWaitEvents.PollPriority);
            maxSocket = Math.Max(maxSocket, socket.Socket.Handle);
        }

        return CurlMultiCode.Ok;
    }

    private static CurlMultiCode ReadWaitSockets(IMultiTransfer live, out CurlWaitSocket[] sockets)
    {
        sockets = [];
        var code = live.GetWaitSockets(sockets, out var count);
        if (code != CurlMultiCode.OutOfMemory)
        {
            return code;
        }

        sockets = new CurlWaitSocket[count];
        code = live.GetWaitSockets(sockets, out count);
        sockets = sockets[..Math.Min(count, sockets.Length)];
        return code;
    }

    private static void AddWhenWaitingFor(ISet<CurlSocket>? set, CurlWaitSocket socket, CurlWaitEvents events)
    {
        if (set is not null && (socket.Events & events) != 0)
        {
            set.Add(socket.Socket);
        }
    }
}
