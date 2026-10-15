namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// <c>CURLMOPT_TIMERFUNCTION</c>: tells the application when to call
/// <see cref="IMultiTransfer.SocketActionAsync"/> with <see cref="CurlMultiSockets.Timeout"/>.
/// <c>CURLMOPT_TIMERDATA</c> is the delegate's closure.
/// </summary>
/// <param name="timeoutMilliseconds"><c>timeout_ms</c>: milliseconds until the timer expires, 0 for at once, or -1 to delete the timer.</param>
/// <returns>0 to go on; -1 fails every transfer of the multi handle.</returns>
public delegate int MultiTimerCallback(long timeoutMilliseconds);
