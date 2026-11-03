namespace Curl.Extended.Protocol.McpClient.Wire;

/// <summary>
/// The part of an InitializeResult a client acts on: the protocol version ("Lifecycle &gt; Version Negotiation") and whether
/// the server declared the tools capability ("Lifecycle &gt; Capability Negotiation").
/// </summary>
/// <param name="ProtocolVersion">The protocol version the server chose.</param>
/// <param name="DeclaresTools">Whether <c>capabilities.tools</c> is an object, so the client may send <c>tools/list</c> and <c>tools/call</c>.</param>
public sealed record McpInitializeResult(string ProtocolVersion, bool DeclaresTools);
