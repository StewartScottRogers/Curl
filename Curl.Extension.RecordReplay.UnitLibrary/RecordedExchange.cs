namespace Curl.Extension.RecordReplay;

/// <summary>One recorded HTTP exchange (ADR-1129 section 3): the request it answers and the response curl received.</summary>
/// <param name="Method">The request method, such as <c>GET</c>.</param>
/// <param name="Url">The URL as the transfer was given it.</param>
/// <param name="StatusCode">The final response's status code.</param>
/// <param name="Head">The response header bytes as curl's handler wrote them for <c>-i</c>, status lines included.</param>
/// <param name="Body">The response body bytes.</param>
public sealed record RecordedExchange(string Method, string Url, int StatusCode, byte[] Head, byte[] Body);
