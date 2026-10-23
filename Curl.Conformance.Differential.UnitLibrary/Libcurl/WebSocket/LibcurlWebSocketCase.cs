namespace Curl.Conformance.Differential.Libcurl.WebSocket;

/// <summary>
/// One WebSocket case: what the loopback server sends after its 101 answer, and either the
/// <c>curl_ws_*</c> calls a <c>CURLOPT_CONNECT_ONLY</c> 2 handle makes, or (callback mode, no
/// steps) a perform whose write callback reads <c>curl_ws_meta</c> for each piece.
/// </summary>
/// <param name="Name">The case's name in the report.</param>
/// <param name="ServerFrames">The raw frames the server sends straight after its 101 answer.</param>
/// <param name="ServerHangsUp">Whether the server closes the connection after those frames instead of reading the client's.</param>
/// <param name="ConnectOnly">Whether the handle is <c>CURLOPT_CONNECT_ONLY</c> 2 and makes <paramref name="Steps"/>.</param>
/// <param name="Steps">The calls a connect-only handle makes after its perform.</param>
public sealed record LibcurlWebSocketCase(string Name, byte[] ServerFrames, bool ServerHangsUp, bool ConnectOnly, IReadOnlyList<LibcurlWebSocketStep> Steps);
