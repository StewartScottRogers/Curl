namespace Curl.Extension.WebSocketRepl.Handshake;

/// <summary>How an opening handshake ended.</summary>
/// <param name="Failure">Why the client failed the connection, or <see langword="null" /> when the handshake succeeded.</param>
/// <param name="AlreadyReceived">
/// The bytes the server sent after its response head, already frames of the session; empty on failure.
/// </param>
public sealed record WebSocketHandshakeResult(string? Failure, ReadOnlyMemory<byte> AlreadyReceived);
