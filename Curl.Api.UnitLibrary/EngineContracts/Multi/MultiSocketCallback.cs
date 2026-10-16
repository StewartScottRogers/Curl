using Curl.Api.Callbacks;

namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// <c>CURLMOPT_SOCKETFUNCTION</c>: tells the application which socket to wait on and for what.
/// <c>CURLMOPT_SOCKETDATA</c> is the delegate's closure.
/// </summary>
/// <param name="transfer"><c>easy</c>: the transfer the socket belongs to.</param>
/// <param name="socket"><c>s</c>: the socket.</param>
/// <param name="what"><c>what</c>: what to wait for, or <see cref="CurlPoll.Remove"/>.</param>
/// <param name="socketState"><c>socketp</c>: the value <see cref="IMultiTransfer.Assign"/> gave the socket, else <see langword="null"/>.</param>
/// <returns>0 to go on; -1 fails every transfer of the multi handle (<c>CURLM_ABORTED_BY_CALLBACK</c>).</returns>
public delegate int MultiSocketCallback(IEasyTransfer transfer, CurlSocket socket, CurlPoll what, object? socketState);
