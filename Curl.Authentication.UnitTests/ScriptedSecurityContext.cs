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

    /// <summary>Wraps by prefixing <c>E</c> when asked to encrypt and <c>S</c> otherwise, so a test sees the call arrive.</summary>
    public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => [(byte)(encrypt ? 'E' : 'S'), .. message];

    /// <summary>Gets or sets whether <see cref="Unwrap" /> fails for every message, as a failed decryption does.</summary>
    public bool UnwrapFails { get; set; }

    /// <summary>Unwraps by dropping the first byte; an empty message does not unwrap, nor does any while <see cref="UnwrapFails" />.</summary>
    public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) => wrappedMessage.IsEmpty || UnwrapFails ? null : wrappedMessage[1..].ToArray();

    public void Dispose() => IsDisposed = true;
}
