namespace Curl.Protocol.Abstractions;

/// <summary>
/// The initiator's side of one authentication exchange in NTLM, SPNEGO or Kerberos, made by
/// <see cref="ISecurityContextFactory.Create" /> (ADR-0142, ADR-0176). It works in raw token
/// bytes; base64 and the header or command that carries them are each caller's.
/// </summary>
/// <remarks>
/// A step is asynchronous because the hand-built Kerberos route asks a KDC for a service
/// ticket on the first one. Unit tests fake it with a scripted class holding the steps to
/// return, never a mocking library.
/// </remarks>
public interface ISecurityContext : IDisposable
{
    /// <summary>Gets whether the context is established.</summary>
    bool IsCompleted { get; }

    /// <summary>Makes the next token to send, from the peer's last token.</summary>
    /// <param name="incomingToken">The peer's token, already base64-decoded; empty on the first step.</param>
    /// <param name="cancellationToken">Cancels the step, and any KDC exchange it makes.</param>
    /// <returns>The step's status and the token to send.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled; every other failure is a status.</exception>
    ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken);
}
