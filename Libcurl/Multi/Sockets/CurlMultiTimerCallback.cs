namespace Libcurl.Multi;

/// <summary>
/// <c>CURLMOPT_TIMERFUNCTION</c>'s <c>curl_multi_timer_callback</c>: tells the application when to call
/// <see cref="CurlMulti.SocketActionAsync"/> with <see cref="Curl.Api.EngineContracts.Multi.CurlMultiSockets.Timeout"/>.
/// <c>CURLMOPT_TIMERDATA</c> (<c>clientp</c>) is the delegate's closure.
/// </summary>
/// <param name="multi"><c>multi</c>: the multi handle whose timer changed.</param>
/// <param name="timeoutMilliseconds"><c>timeout_ms</c>: milliseconds until the timer expires, 0 for at once, or -1 to delete the timer.</param>
/// <returns>0 to go on; -1 fails every transfer of the multi handle.</returns>
public delegate int CurlMultiTimerCallback(CurlMulti multi, long timeoutMilliseconds);
