namespace Curl.Protocol.Ws;

/// <summary>The reply to an upgrade request: its status code, its head and what followed the head.</summary>
/// <param name="StatusCode">The status code; <c>101</c> accepts the upgrade.</param>
/// <param name="Head">The head as received, status line to blank line, for <c>-D</c> and <c>-v</c>.</param>
/// <param name="Remaining">The bytes received after the head: the start of the first frames.</param>
internal sealed record WsUpgradeResponse(int StatusCode, byte[] Head, byte[] Remaining);
