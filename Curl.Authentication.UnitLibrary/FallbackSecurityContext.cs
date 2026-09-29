using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// ADR-0142's "G, else S+K": runs the first step on the system context, and only when that
/// answers <see cref="SecurityContextStatus.NoMechanism" /> disposes it and runs the whole
/// exchange on the fallback instead. Every later step stays on whichever answered the first.
/// </summary>
/// <param name="primary">The system GSS-API context.</param>
/// <param name="createFallback">Makes the hand-built context.</param>
internal sealed class FallbackSecurityContext(ISecurityContext primary, Func<ISecurityContext> createFallback) : ISecurityContext
{
    private ISecurityContext current = primary;
    private bool firstStepTaken;

    /// <inheritdoc />
    public bool IsCompleted => current.IsCompleted;

    /// <inheritdoc />
    public async ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken)
    {
        bool mayFallBack = !firstStepTaken;
        firstStepTaken = true;
        SecurityContextStep step = await current.NextTokenAsync(incomingToken, cancellationToken).ConfigureAwait(false);
        if (!mayFallBack || step.Status != SecurityContextStatus.NoMechanism)
        {
            return step;
        }

        current.Dispose();
        current = createFallback();
        return await current.NextTokenAsync(incomingToken, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => current.Dispose();
}
