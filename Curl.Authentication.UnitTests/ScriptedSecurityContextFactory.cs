using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>An <see cref="ISecurityContextFactory" /> that hands out the contexts it holds, in order, and records each request.</summary>
internal sealed class ScriptedSecurityContextFactory(params ISecurityContext[] contexts) : ISecurityContextFactory
{
    private readonly Queue<ISecurityContext> remaining = new(contexts);

    public List<SecurityContextRequest> Requests { get; } = [];

    public ISecurityContext Create(SecurityContextRequest request)
    {
        Requests.Add(request);
        return remaining.Dequeue();
    }
}
