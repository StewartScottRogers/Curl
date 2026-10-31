namespace Curl.Extension.Protocol.McpClient.Wire;

/// <summary>A JSON-RPC error object ("Base Protocol &gt; Overview &gt; Responses").</summary>
/// <param name="Code">The integer error code.</param>
/// <param name="Message">The short description of the error.</param>
public sealed record McpError(int Code, string Message);
