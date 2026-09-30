using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="ISecurityContextFactory" /> whose every context steps through the scripted
/// <paramref name="steps" /> of one shared script, in order, recording each incoming token, so a
/// test can drive a Negotiate exchange of several legs without SSPI, GSS-API or a KDC.
/// </summary>
/// <param name="steps">The steps handed out, one per <see cref="ISecurityContext.NextTokenAsync" /> call.</param>
public sealed class ScriptedTokenSource(params SecurityContextStep[] steps) : ISecurityContextFactory
{
    private readonly Queue<SecurityContextStep> remaining = new(steps);

    /// <summary>Gets how many contexts were made.</summary>
    public int ContextsMade { get; private set; }

    /// <summary>Gets how many contexts were disposed of.</summary>
    public int ContextsDisposed { get; private set; }

    /// <summary>Gets every incoming token the contexts were stepped with, in order.</summary>
    public List<byte[]> IncomingTokens { get; } = [];

    /// <summary>Gets every request a context was made for, in order.</summary>
    public List<SecurityContextRequest> Requests { get; } = [];

    /// <inheritdoc />
    public ISecurityContext Create(SecurityContextRequest request)
    {
        Requests.Add(request);
        ContextsMade++;
        return new Context(this);
    }

    private sealed class Context(ScriptedTokenSource source) : ISecurityContext
    {
        public bool IsCompleted { get; private set; }

        public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken)
        {
            source.IncomingTokens.Add(incomingToken.ToArray());
            SecurityContextStep step = source.remaining.Dequeue();
            IsCompleted = step.Status == SecurityContextStatus.Completed;
            return ValueTask.FromResult(step);
        }

        public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => throw new NotSupportedException();

        public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) => throw new NotSupportedException();

        public void Dispose() => source.ContextsDisposed++;
    }
}
