using Curl.Api.Codes;

namespace Curl.Api.EngineContracts.WebSocket;

/// <summary>What one <c>curl_ws_send</c> call returned.</summary>
/// <param name="Result">The call's result; <see cref="CurlCode.Again"/> when nothing could be sent yet.</param>
/// <param name="BytesSent"><c>*sent</c>: how many bytes of the buffer were sent.</param>
public readonly record struct CurlWebSocketSendResult(CurlCode Result, int BytesSent);
