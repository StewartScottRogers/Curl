namespace Curl.Extension.SignalR.HubProtocol;

/// <summary>One message of the SignalR hub protocol after the handshake (HubProtocol.md, "Message Types").</summary>
internal abstract record HubMessage;

/// <summary>
/// Type 1: call <paramref name="Target"/> with the arguments, each kept as its JSON text; no
/// <paramref name="InvocationId"/> when no result is wanted (HubProtocol.md, "Invocation").
/// </summary>
internal sealed record InvocationMessage(string? InvocationId, string Target, IReadOnlyList<string> ArgumentsJson, IReadOnlyList<string> StreamIds) : HubMessage;

/// <summary>Type 2: one item, as its JSON text, of the stream an invocation returns (HubProtocol.md, "StreamItem").</summary>
internal sealed record StreamItemMessage(string InvocationId, string ItemJson) : HubMessage;

/// <summary>
/// Type 3: an invocation ended, with its result's JSON text, an error, or neither (HubProtocol.md, "Completion").
/// </summary>
internal sealed record CompletionMessage(string InvocationId, string? ResultJson, string? Error) : HubMessage;

/// <summary>Type 4: call <paramref name="Target"/> for a stream of items (HubProtocol.md, "StreamInvocation").</summary>
internal sealed record StreamInvocationMessage(string InvocationId, string Target, IReadOnlyList<string> ArgumentsJson, IReadOnlyList<string> StreamIds) : HubMessage;

/// <summary>Type 5: stop the stream of one invocation (HubProtocol.md, "CancelInvocation").</summary>
internal sealed record CancelInvocationMessage(string InvocationId) : HubMessage;

/// <summary>Type 6: keep the connection alive (HubProtocol.md, "Ping").</summary>
internal sealed record PingMessage : HubMessage;

/// <summary>Type 7: the server closes the connection, with an error or none, and says whether a client may reconnect (HubProtocol.md, "Close").</summary>
internal sealed record CloseMessage(string? Error, bool AllowReconnect) : HubMessage;

/// <summary>A message of a type HubProtocol.md does not define, which a client ignores.</summary>
internal sealed record UnknownHubMessage(int Type) : HubMessage;
