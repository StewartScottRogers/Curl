using Curl.Api.Callbacks;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;

namespace Libcurl.Multi;

/// <summary>
/// <c>CURLMOPT_SOCKETFUNCTION</c>'s <c>curl_socket_callback</c>: tells the application which socket to
/// wait on, and for what. <c>CURLMOPT_SOCKETDATA</c> (<c>clientp</c>) is the delegate's closure.
/// </summary>
/// <param name="easy"><c>easy</c>: the easy handle, added to the multi handle, the socket belongs to.</param>
/// <param name="socket"><c>s</c>: the socket.</param>
/// <param name="what"><c>what</c>: what to wait for, or <see cref="CurlPoll.Remove"/> to stop waiting on it.</param>
/// <param name="socketState"><c>socketp</c>: the value <see cref="CurlMulti.Assign"/> gave the socket, else <see langword="null"/>.</param>
/// <returns>0 to go on; -1 fails every transfer of the multi handle (<c>CURLM_ABORTED_BY_CALLBACK</c>).</returns>
public delegate int CurlMultiSocketCallback(CurlEasy easy, CurlSocket socket, CurlPoll what, object? socketState);
