using System.Net.Security;

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

    /// <summary>
    /// Gets the message protection the context must negotiate for <see cref="ISecurityContext.Wrap" />
    /// and <see cref="ISecurityContext.Unwrap" /> (ADR-0183). <see cref="ProtectionLevel.None" />,
    /// the default and what HTTP asks, leaves the tokens as curl sends them; SSPI's NTLM then
    /// negotiates no signing key, so its wrapped messages do not verify.
    /// </summary>
    public ProtectionLevel MessageProtection { get; init; }

    /// <summary>
    /// Gets the DER of the TLS server certificate the exchange runs over, from which the
    /// hand-built Kerberos makes RFC 5929's <c>tls-server-end-point</c> channel bindings as curl
    /// with MIT sends them (BL-915); empty, the default, over a connection without TLS, which
    /// sends no bindings.
    /// </summary>
    public ReadOnlyMemory<byte> ServerCertificate { get; init; }
}
