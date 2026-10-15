namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// <c>CURLMOPT_PUSHFUNCTION</c>: accepts or refuses an HTTP/2 server push.
/// <c>CURLMOPT_PUSHDATA</c> is the delegate's closure.
/// </summary>
/// <param name="parent"><c>parent</c>: the transfer the server pushed on.</param>
/// <param name="pushed"><c>easy</c>: the new transfer for the pushed stream.</param>
/// <param name="headers"><c>headers</c>: the push promise's headers as <c>name: value</c> lines, in order (<c>curl_pushheader_bynum</c>).</param>
/// <returns>Whether the push is accepted.</returns>
public delegate CurlPushResult MultiPushCallback(IEasyTransfer parent, IEasyTransfer pushed, IReadOnlyList<string> headers);
