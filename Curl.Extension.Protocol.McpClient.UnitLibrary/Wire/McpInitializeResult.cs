namespace Curl.Extension.Protocol.McpClient.Wire;

/// <summary>The part of an InitializeResult a client acts on ("Lifecycle &gt; Version Negotiation").</summary>
/// <param name="ProtocolVersion">The protocol version the server chose.</param>
public sealed record McpInitializeResult(string ProtocolVersion);
