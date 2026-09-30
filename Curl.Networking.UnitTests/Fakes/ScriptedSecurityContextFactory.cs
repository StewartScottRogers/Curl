using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ISecurityContextFactory" /> whose one context returns scripted steps, and
/// wraps and unwraps by prefixing <c>W</c> - or refuses to when told - recording every request,
/// incoming token and message it was given.
/// </summary>
/// <param name="steps">The steps the context returns, in order.</param>
public sealed class ScriptedSecurityContextFactory(params SecurityContextStep[] steps) : ISecurityContextFactory
{
    /// <summary>The byte a wrapped message starts with.</summary>
    public const byte WrapMarker = (byte)'W';

    private readonly SecurityContextStep[] _steps = steps;

    /// <summary>Gets every request <see cref="Create" /> was given.</summary>
    public List<SecurityContextRequest> Requests { get; } = [];

    /// <summary>Gets every token the context was given, in order.</summary>
    public List<byte[]> IncomingTokens { get; } = [];

    /// <summary>Gets every message the context wrapped, with its encrypt flag.</summary>
    public List<(byte[] Message, bool Encrypt)> Wrapped { get; } = [];

    /// <summary>Gets whether <see cref="ISecurityContext.Wrap" /> gives <see langword="null" />.</summary>
    public bool RefusesToWrap { get; init; }

    /// <summary>Gets whether the context was disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public ISecurityContext Create(SecurityContextRequest request)
    {
        Requests.Add(request);
        return new Context(this);
    }

    private sealed class Context(ScriptedSecurityContextFactory owner) : ISecurityContext
    {
        private int _next;

        public bool IsCompleted => false;

        public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken)
        {
            owner.IncomingTokens.Add(incomingToken.ToArray());
            return ValueTask.FromResult(owner._steps[_next++]);
        }

        public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt)
        {
            owner.Wrapped.Add((message.ToArray(), encrypt));
            return owner.RefusesToWrap ? null : [WrapMarker, .. message];
        }

        public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) =>
            wrappedMessage is [WrapMarker, .. var message] ? message.ToArray() : null;

        public void Dispose() => owner.IsDisposed = true;
    }
}
