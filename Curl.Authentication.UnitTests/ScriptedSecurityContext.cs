using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// An <see cref="ISecurityContext" /> that returns the steps it holds, in order, and records the
/// tokens it was given, as <c>ScriptedSaslExchange</c> does for SASL (ADR-0142).
/// </summary>
internal sealed class ScriptedSecurityContext(params SecurityContextStep[] steps) : ISecurityContext
{
    private readonly Queue<SecurityContextStep> remaining = new(steps);

    public List<byte[]> IncomingTokens { get; } = [];

    public bool IsDisposed { get; private set; }

    public bool IsCompleted { get; set; }

    public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken)
    {
        IncomingTokens.Add(incomingToken.ToArray());
        return ValueTask.FromResult(remaining.Dequeue());
    }

    public void Dispose() => IsDisposed = true;
}
