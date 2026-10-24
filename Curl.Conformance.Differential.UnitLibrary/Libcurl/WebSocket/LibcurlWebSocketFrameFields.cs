namespace Curl.Conformance.Differential.Libcurl.WebSocket;

/// <summary>The fields of a <c>struct curl_ws_frame</c> one library answered.</summary>
/// <param name="Age">The struct's <c>age</c>.</param>
/// <param name="Flags">Its <c>CURLWS_*</c> flags.</param>
/// <param name="Offset">Its <c>offset</c>.</param>
/// <param name="BytesLeft">Its <c>bytesleft</c>.</param>
/// <param name="Length">Its <c>len</c>.</param>
public sealed record LibcurlWebSocketFrameFields(int Age, int Flags, long Offset, long BytesLeft, long Length);
