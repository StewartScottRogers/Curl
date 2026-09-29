using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// A context for a mechanism the hand-built route does not run yet: every step answers
/// <see cref="SecurityContextStatus.NoMechanism" />, so the caller sends nothing. Hand-built
/// NTLM replaces it (BL-526).
/// </summary>
internal sealed class UnavailableSecurityContext : ISecurityContext
{
    /// <inheritdoc />
    public bool IsCompleted => false;

    /// <inheritdoc />
    public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new SecurityContextStep(SecurityContextStatus.NoMechanism, []));

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
