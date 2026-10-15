namespace Curl.Api.EngineContracts.WebSocket;

/// <summary>
/// What libcurl knows of the WebSocket frame being received: <c>struct curl_ws_frame</c>, which
/// <c>curl_ws_recv</c> hands back and <c>curl_ws_meta</c> returns. The default is every field zero.
/// </summary>
/// <param name="Age"><c>age</c>: the struct's version; zero in curl 8.22.0.</param>
/// <param name="Flags"><c>flags</c>: the frame's <c>CURLWS_*</c> bits.</param>
/// <param name="Offset"><c>offset</c>: where in the frame this piece of data starts.</param>
/// <param name="BytesLeft"><c>bytesleft</c>: how many bytes of the frame are still to come.</param>
/// <param name="Length"><c>len</c>: the size of the piece of data just received.</param>
public readonly record struct CurlWebSocketFrame(int Age, CurlWebSocketFlags Flags, long Offset, long BytesLeft, long Length);
