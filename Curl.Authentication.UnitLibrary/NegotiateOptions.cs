using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// What the command line says about a Negotiate exchange (ADR-0188): the service of the
/// server's principal (<c>--service-name</c>), the service of the proxy's principal
/// (<c>--proxy-service-name</c>), each <c>HTTP</c> when not given, and whether the acceptor may
/// act on the user's behalf (<c>--delegation</c>).
/// </summary>
/// <param name="ServiceName">The <c>--service-name</c> value, or <see langword="null" /> for <c>HTTP</c>.</param>
/// <param name="ProxyServiceName">The <c>--proxy-service-name</c> value, or <see langword="null" /> for <c>HTTP</c>.</param>
/// <param name="Delegation">The <c>--delegation</c> level.</param>
public sealed record NegotiateOptions(string? ServiceName, string? ProxyServiceName, SecurityDelegation Delegation)
{
    /// <summary>Gets the options with no <c>--service-name</c>, no <c>--proxy-service-name</c> and no delegation.</summary>
    public static NegotiateOptions Default { get; } = new(null, null, SecurityDelegation.None);
}
