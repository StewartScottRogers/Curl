using Curl.Api.Callbacks;

namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// One socket to wait on: <c>struct curl_waitfd</c>, as <c>curl_multi_wait</c> and
/// <c>curl_multi_poll</c> take extra sockets and <c>curl_multi_waitfds</c> reports libcurl's own.
/// </summary>
/// <param name="Socket"><c>fd</c>: the socket.</param>
/// <param name="Events"><c>events</c>: the events to wait for.</param>
/// <param name="ReturnedEvents"><c>revents</c>: the events that happened, set by the wait.</param>
public readonly record struct CurlWaitSocket(CurlSocket Socket, CurlWaitEvents Events, CurlWaitEvents ReturnedEvents);
