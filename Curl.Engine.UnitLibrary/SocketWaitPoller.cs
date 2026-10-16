using System.Net.Sockets;
using Curl.Api.Callbacks;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

/// <summary>
/// The production <see cref="IWaitSocketPoller" />: wraps the descriptor in a <see cref="Socket" /> that
/// does not own it and asks <see cref="Socket.Poll(int, SelectMode)" /> with no wait, <c>POLLIN</c> as
/// <see cref="SelectMode.SelectRead" />, <c>POLLOUT</c> as <see cref="SelectMode.SelectWrite" /> and
/// <c>POLLPRI</c> as <see cref="SelectMode.SelectError" />, which holds urgent data (ADR-0597).
/// </summary>
internal sealed class SocketWaitPoller : IWaitSocketPoller
{
    /// <summary>Gets the one instance; it keeps no state.</summary>
    public static SocketWaitPoller Instance { get; } = new();

    /// <inheritdoc />
    public CurlWaitEvents Poll(CurlSocket socket, CurlWaitEvents events)
    {
        using Socket wrapped = new(new SafeSocketHandle(socket.Handle, ownsHandle: false));
        return EventIf(wrapped, events, CurlWaitEvents.PollIn, SelectMode.SelectRead)
            | EventIf(wrapped, events, CurlWaitEvents.PollOut, SelectMode.SelectWrite)
            | EventIf(wrapped, events, CurlWaitEvents.PollPriority, SelectMode.SelectError);
    }

    private static CurlWaitEvents EventIf(Socket socket, CurlWaitEvents events, CurlWaitEvents wanted, SelectMode mode) =>
        events.HasFlag(wanted) && socket.Poll(0, mode) ? wanted : CurlWaitEvents.None;
}
