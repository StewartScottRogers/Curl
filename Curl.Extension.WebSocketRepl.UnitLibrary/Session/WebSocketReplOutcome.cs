namespace Curl.Extension.WebSocketRepl.Session;

/// <summary>How an interactive WebSocket session ended.</summary>
/// <param name="Ending">Why it ended.</param>
/// <param name="ViolationMessage">
/// The frame decoder's message for a <see cref="WebSocketReplEnding.ProtocolViolation" />, otherwise <see langword="null" />.
/// </param>
public sealed record WebSocketReplOutcome(WebSocketReplEnding Ending, string? ViolationMessage);
