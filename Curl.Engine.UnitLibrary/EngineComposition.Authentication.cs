using System.Security.Cryptography;
using System.Text;
using Curl.Authentication;
using Curl.Kerberos;
using Curl.Networking;
using Curl.Ntlm;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>The system's cryptographically random bytes, which Digest and DIGEST-MD5 draw their client nonce from in production.</summary>
    private static readonly Func<int, byte[]> SystemRandomBytes = RandomNumberGenerator.GetBytes;

    /// <summary>
    /// Creates the HTTP authenticator: a <see cref="RankedHttpAuthenticator" /> that answers the
    /// scheme curl 8.21.0 picks among those <c>--basic</c>, <c>--digest</c>, <c>--ntlm</c>, <c>--negotiate</c> and <c>--anyauth</c>
    /// allow, with a <see cref="BasicAndBearerAuthenticator" /> for Basic, Bearer
    /// (<c>--oauth2-bearer</c>) and the first request, and a <see cref="DigestAuthenticator" />
    /// drawing each client nonce from the matched build's <see cref="DigestClientNonce.ForBuild(bool)" />, both encoding
    /// credentials in the platform's encoding (<see cref="CredentialEncoding.ForPlatform" />),
    /// and a <see cref="NegotiateHttpAuthenticator" /> and an <see cref="NtlmHttpAuthenticator" /> over <paramref name="securityContexts" />
    /// for <c>--negotiate</c> and <c>--ntlm</c> (ADR-0176, ADR-0181), a refused NTLM Type 2 message failing the transfer
    /// on Windows, as curl's SSPI build fails it, Negotiate naming the service and delegating as
    /// <paramref name="negotiateOptions" /> says (ADR-0188).
    /// </summary>
    /// <param name="securityContexts">Makes Negotiate's and NTLM's contexts: <see cref="CreateSecurityContextFactory" />'s in production.</param>
    /// <param name="negotiateOptions">The service names and delegation level; <see cref="NegotiateOptions.Default" /> when <see langword="null" />.</param>
    /// <returns>The authenticator.</returns>
    /// <param name="diagnosticLog">The run's diagnostic log, which the authenticators and security contexts write their choices to (BL-923); <see langword="null" /> for none.</param>
    /// <param name="digestRandomBytes">Where Digest draws its client nonce's bytes from (ADR-0751, BL-2834); <see langword="null" /> for <see cref="RandomNumberGenerator" />.</param>
    internal static RankedHttpAuthenticator CreateHttpAuthenticator(ISecurityContextFactory securityContexts, NegotiateOptions? negotiateOptions = null, IDiagnosticLog? diagnosticLog = null, Func<int, byte[]>? digestRandomBytes = null)
    {
        Encoding credentialEncoding = CredentialEncoding.ForPlatform(OperatingSystem.IsWindows());
        bool digestMatchesSspiBuild = MatchesSspiBuild(securityContexts);

        return new RankedHttpAuthenticator(
            new BasicAndBearerAuthenticator(credentialEncoding),
            new DigestAuthenticator(credentialEncoding, DigestClientNonce.ForBuild(digestMatchesSspiBuild, digestRandomBytes ?? SystemRandomBytes), diagnosticLog, matchesSspiBuild: digestMatchesSspiBuild),
            new NegotiateHttpAuthenticator(securityContexts, negotiateOptions, diagnosticLog: diagnosticLog),
            new NtlmHttpAuthenticator(securityContexts, matchesSspiBuild: OperatingSystem.IsWindows(), diagnosticLog),
            diagnosticLog);
    }

    /// <summary>
    /// Whether Digest sends its header in the Schannel build's SSPI form: on Windows, unless the contexts
    /// are the hand-built ones that curl's non-SSPI build is matched with (the upstream conformance runner's).
    /// </summary>
    /// <param name="securityContexts">The contexts the authenticator is made over.</param>
    /// <returns><see langword="true" /> for the SSPI form.</returns>
    private static bool MatchesSspiBuild(ISecurityContextFactory securityContexts) =>
        OperatingSystem.IsWindows() && securityContexts is not HandBuiltNtlmSecurityContextFactory and not LateBoundSecurityContextFactory { UsesHandBuiltNtlm: true };

    /// <summary>
    /// Creates ADR-0142's router for NTLM, Negotiate and Kerberos contexts: SSPI on Windows,
    /// elsewhere the system GSS-API with the hand-built SPNEGO and Kerberos behind it, whose KDC
    /// exchanges go through a <see cref="KerberosKdcSocketTransport" /> over
    /// <paramref name="datagramConnector" /> and <paramref name="connector" /> (waiting
    /// <see cref="KdcReplyTimeout" /> for a UDP reply), whose <c>https://</c> KDCs are reached through a
    /// <see cref="KerberosKdcProxyHttpsTransport" /> over <paramref name="connector" /> with TLS, and whose SRV lookups go through a
    /// <see cref="DnsServerResolver" /> asking the system's DNS servers (ADR-0176).
    /// </summary>
    /// <param name="connector">Opens TCP connections to a KDC.</param>
    /// <param name="datagramConnector">Opens UDP channels to a KDC.</param>
    /// <returns>The router.</returns>
    /// <param name="diagnosticLog">The run's diagnostic log, which the authenticators and security contexts write their choices to (BL-923); <see langword="null" /> for none.</param>
    internal static RoutingSecurityContextFactory CreateSecurityContextFactory(IConnector connector, IDatagramConnector datagramConnector, IDiagnosticLog? diagnosticLog = null) =>
        new(
            OperatingSystem.IsWindows(),
            new SystemSecurityContextFactory(),
            CreateHandBuiltSecurityContextFactory(connector, datagramConnector, diagnosticLog),
            diagnosticLog);

    /// <summary>
    /// Creates the contexts the dialing runner (<c>Curl.Console</c>'s <c>CurlComposition.CreateRunner</c>)
    /// uses: <see cref="CreateSecurityContextFactory(IConnector, IDatagramConnector, IDiagnosticLog?)" />'s
    /// router, with NTLM sent to the hand-built context on every platform when
    /// <paramref name="usesHandBuiltNtlm" /> is set (<see cref="HandBuiltNtlmSecurityContextFactory" />, BL-1858).
    /// </summary>
    /// <param name="connector">Opens TCP connections to a KDC.</param>
    /// <param name="datagramConnector">Opens UDP channels to a KDC.</param>
    /// <param name="diagnosticLog">The run's diagnostic log.</param>
    /// <param name="usesHandBuiltNtlm">Whether NTLM is answered by the hand-built context, as curl's non-SSPI build answers it.</param>
    /// <returns>The factory.</returns>
    internal static ISecurityContextFactory CreateDialingSecurityContextFactory(IConnector connector, IDatagramConnector datagramConnector, IDiagnosticLog? diagnosticLog, bool usesHandBuiltNtlm) =>
        usesHandBuiltNtlm
            ? new HandBuiltNtlmSecurityContextFactory(CreateSecurityContextFactory(connector, datagramConnector, diagnosticLog), CreateHandBuiltSecurityContextFactory(connector, datagramConnector, diagnosticLog))
            : CreateSecurityContextFactory(connector, datagramConnector, diagnosticLog);

    /// <summary>
    /// Creates ADR-0142's hand-built factory: curl's own NTLM, and SPNEGO and Kerberos over the
    /// hand-built Kerberos whose KDC exchanges are described on
    /// <see cref="CreateSecurityContextFactory(IConnector, IDatagramConnector, IDiagnosticLog?)" />.
    /// </summary>
    /// <param name="connector">Opens TCP connections to a KDC.</param>
    /// <param name="datagramConnector">Opens UDP channels to a KDC.</param>
    /// <param name="diagnosticLog">The run's diagnostic log.</param>
    /// <returns>The factory.</returns>
    private static HandBuiltSecurityContextFactory CreateHandBuiltSecurityContextFactory(IConnector connector, IDatagramConnector datagramConnector, IDiagnosticLog? diagnosticLog)
    {
        KerberosKdcSocketTransport kdcTransport = new(datagramConnector, connector, KdcReplyTimeout, TimeProvider.System);
        DnsServerResolver srvResolver = new(new DnsServerResolverOptions(null, null, null, null), TimeProvider.System);
        HandBuiltKerberosSources sources = new(
            new KerberosDiskFileReader(),
            new KerberosDiskFileWriter(),
            HandBuiltKerberosSources.ReadProcessEnvironmentVariable,
            ProcessUserId.Read,
            new KerberosDnsSrvLookup(srvResolver.ResolveServiceAsync),
            kdcTransport,
            new KerberosKdcProxyHttpsTransport(connector, KdcProxyExchangeTimeout, TimeProvider.System),
            TimeProvider.System);
        return new HandBuiltSecurityContextFactory(sources.CreateTicketSource(), TimeProvider.System, new SystemKerberosRandomSource(), new SystemNtlmRandomSource(), diagnosticLog);
    }

    /// <summary>
    /// How long a UDP request to a KDC waits for its reply before the next KDC is tried: MIT's
    /// first per-KDC wait (<c>krb5_sendto_kdc</c>), one second.
    /// </summary>
    internal static TimeSpan KdcReplyTimeout { get; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long one HTTPS exchange with an MS-KKDCP proxy, connection and TLS included, may take
    /// before the next KDC is tried (BL-882): ten seconds, as MIT's <c>krb5_sendto_kdc</c> gives
    /// one KDC's stream connection.
    /// </summary>
    internal static TimeSpan KdcProxyExchangeTimeout { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates the SASL authenticator the SMTP, POP3 and IMAP handlers share (ADR-0121): a
    /// <see cref="SaslAuthenticator" /> encoding credentials in the platform's encoding
    /// (<see cref="CredentialEncoding.ForPlatform" />), as the HTTP authenticator does, and answering
    /// GSSAPI and NTLM on the contexts <paramref name="securityContexts" /> makes (ADR-0184, BL-852),
    /// asking GSSAPI's for the <paramref name="gssapiDelegation" /> level (BL-874).
    /// </summary>
    /// <param name="securityContexts">Makes GSSAPI's and NTLM's contexts: <see cref="CreateSecurityContextFactory" />'s in production.</param>
    /// <param name="gssapiDelegation">The <c>--delegation</c> level; <see cref="SecurityDelegation.None" /> when not given.</param>
    /// <returns>The authenticator.</returns>
    /// <param name="diagnosticLog">The run's diagnostic log, which the authenticators and security contexts write their choices to (BL-923); <see langword="null" /> for none.</param>
    /// <param name="digestRandomBytes">Where DIGEST-MD5 draws its client nonce's bytes from (ADR-0751, BL-2834); <see langword="null" /> for <see cref="RandomNumberGenerator" />. DIGEST-MD5 answers in curl's own form, not SSPI's, over the hand-built contexts, as for HTTP Digest.</param>
    internal static ISaslAuthenticator CreateSaslAuthenticator(ISecurityContextFactory securityContexts, SecurityDelegation gssapiDelegation = SecurityDelegation.None, IDiagnosticLog? diagnosticLog = null, Func<int, byte[]>? digestRandomBytes = null) =>
        new SaslAuthenticator(CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()), DigestClientNonce.HexFrom(digestRandomBytes ?? SystemRandomBytes), MatchesSspiBuild(securityContexts), securityContexts, diagnosticLog) { GssapiDelegation = gssapiDelegation };
}
