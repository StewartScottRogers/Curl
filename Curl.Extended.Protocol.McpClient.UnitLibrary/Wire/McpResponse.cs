using System.Text.Json;

namespace Curl.Extended.Protocol.McpClient.Wire;

/// <summary>
/// The server's JSON-RPC response to one request ("Base Protocol &gt; Overview &gt; Responses"):
/// exactly one of <see cref="Result"/> and <see cref="Error"/> is set.
/// </summary>
/// <param name="Id">The id of the request it answers.</param>
/// <param name="Result">The <c>result</c> member, detached from the message, or <see langword="null"/> for an error.</param>
/// <param name="Error">The <c>error</c> member, or <see langword="null"/> for a result.</param>
public sealed record McpResponse(long Id, JsonElement? Result, McpError? Error);
