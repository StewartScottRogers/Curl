namespace Curl.Protocol.Ftp;

/// <summary>
/// How curl 8.21.0's <c>-v</c> names the control connection when a transfer ends: its number,
/// and the URL's host and the port it was made to (BL-931).
/// </summary>
/// <param name="Number">The connection's number, the <c>0</c> of <c>Connection #0</c>.</param>
/// <param name="Host">The URL's host, as given.</param>
/// <param name="Port">The port the control connection was made to.</param>
internal sealed record FtpControlConnectionName(long Number, string Host, int Port);
