namespace Curl.Protocol.Abstractions;

/// <summary>The outcome of one <see cref="ISecurityContext.NextTokenAsync" /> step: its status and the token to send.</summary>
/// <param name="Status">What the step came to.</param>
/// <param name="Token">The token to send to the peer; empty when there is none, and always empty for a failure.</param>
public readonly record struct SecurityContextStep(SecurityContextStatus Status, byte[] Token);
