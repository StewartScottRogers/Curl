namespace Curl.Extended.Protocol.McpClient.Wire;

/// <summary>
/// One JSON-RPC request <see cref="McpRequestWriter"/> wrote: its id, which the server's response
/// carries back ("Base Protocol &gt; Overview &gt; Responses"), and its UTF-8 bytes.
/// </summary>
/// <param name="Id">The request's id.</param>
/// <param name="Bytes">The request as one UTF-8 JSON object.</param>
public sealed record McpOutgoingRequest(long Id, byte[] Bytes);
