using Curl.Api.Codes;

namespace Curl.Api.EngineContracts.WebSocket;

/// <summary>What one <c>curl_ws_recv</c> call returned.</summary>
/// <param name="Result">The call's result; <see cref="CurlCode.Again"/> when no data is ready.</param>
/// <param name="BytesReceived"><c>*recv</c>: how many bytes were written into the buffer.</param>
/// <param name="Frame"><c>*metap</c>: the frame the bytes belong to.</param>
public readonly record struct CurlWebSocketReceiveResult(CurlCode Result, int BytesReceived, CurlWebSocketFrame Frame);
