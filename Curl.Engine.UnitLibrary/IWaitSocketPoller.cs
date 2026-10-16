using Curl.Api.Callbacks;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

/// <summary>
/// Checks, without waiting, which events one of the extra sockets a <c>curl_multi_wait</c> or
/// <c>curl_multi_poll</c> caller passes has now (ADR-0597). <see cref="ManagedMultiTransfer" /> asks it
/// again each poll interval while it waits.
/// </summary>
internal interface IWaitSocketPoller
{
    /// <summary>Gets the events of <paramref name="events" /> that <paramref name="socket" /> has now.</summary>
    /// <param name="socket">The operating system socket.</param>
    /// <param name="events">The events the caller waits for.</param>
    /// <returns>The events that happened, or <see cref="CurlWaitEvents.None" />.</returns>
    CurlWaitEvents Poll(CurlSocket socket, CurlWaitEvents events);
}
