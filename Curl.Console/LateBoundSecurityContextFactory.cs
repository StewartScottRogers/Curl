using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Makes each context with the factory <see cref="Bind" /> was given, so the CONNECT tunnel's
/// proxy authenticator can be built before the connectors that ADR-0142's router needs for its
/// KDC exchanges, and still use that router (BL-604): the tunnel's options go into the
/// connector, and the router is made from the connector afterwards.
/// </summary>
internal sealed class LateBoundSecurityContextFactory : ISecurityContextFactory
{
    private ISecurityContextFactory? bound;

    /// <summary>Gives the factory every later <see cref="Create" /> call goes to.</summary>
    /// <param name="factory">The factory that makes the contexts.</param>
    public void Bind(ISecurityContextFactory factory) => bound = factory;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">No factory has been bound yet.</exception>
    public ISecurityContext Create(SecurityContextRequest request) =>
        (bound ?? throw new InvalidOperationException("No security context factory has been bound yet.")).Create(request);
}
