namespace Curl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="ISecurityContextFactory" /> is asked to build a context for (ADR-0142):
/// the mechanism, the service and host that name the acceptor, and the credential, whose
/// parts are all <see langword="null" /> for the default credentials (<c>-u :</c>).
/// </summary>
/// <param name="Mechanism">The mechanism to run.</param>
/// <param name="ServiceName">
/// The acceptor's service, <c>HTTP</c> for HTTP; each implementation joins it to
/// <paramref name="HostName" /> in its own form: <c>HTTP/host</c> for SSPI and the system
/// GSS-API, the principal <c>HTTP/host@REALM</c> for the hand-built Kerberos.
/// </param>
/// <param name="HostName">The acceptor's host name, without IPv6 brackets.</param>
public sealed record SecurityContextRequest(SecurityMechanism Mechanism, string ServiceName, string HostName)
{
    /// <summary>Gets the user name of an explicit credential, or <see langword="null" /> for the default credentials.</summary>
    public string? UserName { get; init; }

    /// <summary>Gets the password of an explicit credential, or <see langword="null" />.</summary>
    public string? Password { get; init; }

    /// <summary>Gets the domain of an explicit credential, or <see langword="null" /> for none.</summary>
    public string? Domain { get; init; }

    /// <summary>Gets whether the acceptor may act on the initiator's behalf: <c>--delegation</c>.</summary>
    public SecurityDelegation Delegation { get; init; }
}
