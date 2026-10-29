using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Curl.Networking;

namespace Curl.Engine;

internal sealed partial class ManagedMultiTransfer
{
    /// <summary>
    /// <c>curl_multi_perform</c>: starts every transfer added since the last call and queues a
    /// <see cref="CurlMultiMessageType.Done" /> message for each one that has finished since, without
    /// waiting for any. An exception a transfer's perform threw comes out of this call.
    /// </summary>
    /// <param name="cancellationToken">Stops the call before it starts anything.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" /> and the transfers not yet finished, or
    /// <see cref="CurlMultiCode.BadHandle" /> and 0 once this handle is disposed. It tells the timer
    /// callback what changed as <see cref="SocketActionAsync" /> does, but never the socket callback, as
    /// curl 8.22.0's perform does not (CGR-148).
    /// </returns>
    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken)
    {
        foreach (TransferSocketWait wait in entries.Select(entry => entry.SocketWait).OfType<TransferSocketWait>())
        {
            wait.Signal(SelectOf(wait.Waiting));
        }

        return MoveTransfersOnAsync(socketAction: false, timerFired: false, cancellationToken);
    }

    /// <summary>
    /// <c>curl_multi_socket_action</c>: moves every transfer on as <see cref="PerformAsync" /> does - the
    /// managed engine's transfers run on their own, so a socket's events add nothing to what is checked -
    /// then tells <see cref="MultiOptions.Socket" /> of each transfer that started (its socket and
    /// <see cref="CurlPoll.In" />) or finished (<see cref="CurlPoll.Remove" />, with the value
    /// <see cref="Assign" /> gave), and <see cref="MultiOptions.Timer" /> of the next timeout when it changed,
    /// or always after <see cref="CurlMultiSockets.Timeout" />, whose timer is spent. Once either callback
    /// returns -1 the handle is dead, as libcurl's: every later socket action answers
    /// <see cref="CurlMultiCode.Ok" /> and the unfinished transfers without moving any on or calling either
    /// callback, and the next <see cref="PerformAsync" /> ends every unfinished transfer with
    /// <see cref="CurlCode.AbortedByCallback" /> (CGR-148).
    /// </summary>
    /// <param name="socket">The socket with the event, or <see cref="CurlMultiSockets.Timeout" /> for the timer.</param>
    /// <param name="events">The events on the socket; not read.</param>
    /// <param name="cancellationToken">Stops the call before it starts anything.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" /> and the transfers not yet finished;
    /// <see cref="CurlMultiCode.AbortedByCallback" /> when a callback returned -1, and
    /// <see cref="CurlMultiCode.BadHandle" /> and 0 once this handle is disposed.
    /// </returns>
    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken)
    {
        foreach (MultiEntry entry in entries.FindAll(candidate => candidate.SocketWait is { } wait && IsActionFor(wait, socket)))
        {
            entry.SocketWait!.Signal(events == CurlSocketSelect.None ? SelectOf(entry.SocketWait.Waiting) : events);
        }

        return MoveTransfersOnAsync(socketAction: true, socket == CurlMultiSockets.Timeout, cancellationToken);
    }

    /// <summary>
    /// <c>curl_multi_assign</c>: the value the socket callback receives with <paramref name="socket" />
    /// from now on.
    /// </summary>
    /// <param name="socket">A socket the socket callback was told of and not yet told to remove.</param>
    /// <param name="socketState">The value, or <see langword="null" /> to clear it.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" />; <see cref="CurlMultiCode.BadSocket" /> for any other socket and
    /// <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed.
    /// </returns>
    public CurlMultiCode Assign(CurlSocket socket, object? socketState)
    {
        if (disposed)
        {
            return CurlMultiCode.BadHandle;
        }

        MultiEntry? entry = entries.Find(candidate => candidate.Socket == socket);
        if (entry is null)
        {
            return CurlMultiCode.BadSocket;
        }

        entry.SocketState = socketState;
        return CurlMultiCode.Ok;
    }

    /// <summary><c>curl_multi_info_read</c>: takes the oldest queued message.</summary>
    /// <param name="message">The message, or <see langword="null" /> when none waits.</param>
    /// <param name="remainingMessages">The messages still queued after this one.</param>
    /// <returns>
    /// <see langword="true" /> when a message was taken; <see langword="false" /> from inside one of the handle's
    /// own transfers' callbacks, where libcurl's call answers no message (CGR-146).
    /// </returns>
    public bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages)
    {
        if (messages.Count == 0 || IsInTransferCallback)
        {
            message = null;
            remainingMessages = 0;
            return false;
        }

        message = messages[0];
        messages.RemoveAt(0);
        remainingMessages = messages.Count;
        return true;
    }

}
