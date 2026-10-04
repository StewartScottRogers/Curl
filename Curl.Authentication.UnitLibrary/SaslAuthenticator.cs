using System.Globalization;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Chooses a SASL mechanism as curl 8.21.0 does and answers PLAIN, LOGIN, EXTERNAL, XOAUTH2,
/// OAUTHBEARER, CRAM-MD5 and DIGEST-MD5 with the bytes curl sends, and GSSAPI and NTLM through
/// security contexts (ADR-0121, ADR-0123, ADR-0139, ADR-0184).
/// </summary>
/// <param name="credentialEncoding">
/// The encoding the user name, password, authorization identity and token are sent in; see
/// <see cref="CredentialEncoding.ForPlatform" />.
/// </param>
/// <param name="createClientNonce">
/// Creates DIGEST-MD5's client nonce; <see cref="DigestClientNonce.CreateRandomHex" /> in
/// production, a fixed value in tests.
/// </param>
/// <param name="answerDigestMd5AsSspi">
/// <see langword="true" /> to answer DIGEST-MD5 as curl's Schannel build on Windows does,
/// through SSPI; <see langword="false" /> to answer as curl's own code in the OpenSSL build
/// does (ADR-0139).
/// </param>
/// <param name="securityContexts">
/// Makes the Kerberos and NTLM contexts GSSAPI and NTLM run on, ADR-0142's router in
/// production; <see langword="null" /> treats both mechanisms as not offered.
/// </param>
/// <remarks>
/// <para>
/// The preference order is EXTERNAL, GSSAPI, DIGEST-MD5, CRAM-MD5, NTLM, OAUTHBEARER,
/// XOAUTH2, PLAIN, LOGIN.
/// </para>
/// <para>
/// GSSAPI and NTLM run on a context for the SASL service on the server's host
/// (<c>smtp/host</c>). NTLM's initial response is the Type 1 message and its one answer the
/// Type 3 message. GSSAPI's is the raw Kerberos token (RFC 4752, not SPNEGO); its answers are
/// the context's tokens until it is established, then the server's wrapped security-layer
/// offer is answered with no layer, a zero size and the authorization identity, wrapped
/// without encryption, as curl's <c>Curl_auth_create_gssapi_security_message</c> does.
/// </para>
/// <para>
/// Every mechanism here has an initial response, LOGIN's being the user name, as curl sends
/// with <c>--sasl-ir</c>. Without it the handler sends the initial response in answer to the
/// server's first challenge, and <see cref="ISaslExchange.RespondAsync" /> answers the challenges
/// after that: LOGIN's password, and OAUTHBEARER's single <c>0x01</c> byte acknowledging an
/// error continuation. CRAM-MD5 and DIGEST-MD5 have no initial response and compute their
/// answers from the server's challenge; DIGEST-MD5 answers the server's <c>rspauth</c>
/// with an empty response, as curl does.
/// </para>
/// The mechanism chosen from the server's list, each exchange begun, and a DIGEST-MD5 answer
/// that fails the transfer are written to the diagnostic log, never a credential (BL-923).
/// </remarks>
/// <param name="diagnosticLog">Where the mechanism choices are logged; <see langword="null" /> logs nothing.</param>
public sealed class SaslAuthenticator(
    Encoding credentialEncoding,
    Func<string> createClientNonce,
    bool answerDigestMd5AsSspi,
    ISecurityContextFactory? securityContexts,
    IDiagnosticLog? diagnosticLog = null) : ISaslAuthenticator
{
    private readonly AuthDiagnosticLog log = new(diagnosticLog);

    /// <summary>
    /// Initializes an authenticator that answers DIGEST-MD5 as the platform's curl does, with
    /// a random client nonce, and treats GSSAPI and NTLM as not offered.
    /// </summary>
    /// <param name="credentialEncoding">
    /// The encoding the user name, password, authorization identity and token are sent in;
    /// see <see cref="CredentialEncoding.ForPlatform" />.
    /// </param>
    public SaslAuthenticator(Encoding credentialEncoding)
        : this(credentialEncoding, DigestClientNonce.CreateRandomHex, OperatingSystem.IsWindows(), securityContexts: null)
    {
    }

    /// <summary>
    /// Initializes an authenticator that answers DIGEST-MD5 as the platform's curl does, with
    /// a random client nonce, and GSSAPI and NTLM on the contexts <paramref name="securityContexts" /> makes.
    /// </summary>
    /// <param name="credentialEncoding">
    /// The encoding the user name, password, authorization identity and token are sent in;
    /// see <see cref="CredentialEncoding.ForPlatform" />.
    /// </param>
    /// <param name="securityContexts">Makes the Kerberos and NTLM contexts; ADR-0142's router in production.</param>
    /// <param name="diagnosticLog">Where the mechanism choices are logged; <see langword="null" /> logs nothing.</param>
    public SaslAuthenticator(Encoding credentialEncoding, ISecurityContextFactory securityContexts, IDiagnosticLog? diagnosticLog = null)
        : this(credentialEncoding, DigestClientNonce.CreateRandomHex, OperatingSystem.IsWindows(), securityContexts, diagnosticLog)
    {
    }

    /// <summary>The byte OAUTHBEARER and XOAUTH2 separate their fields with (RFC 7628 section 3.1).</summary>
    private const char FieldSeparator = '\u0001';

    /// <summary>The message curl prints for exit 94, <see cref="CurlExitCode.AuthError" />.</summary>
    private const string AuthErrorMessage = "An authentication function returned an error";

    // Each built mechanism's messages: the initial response first, then the answers to the
    // challenges after it. OAUTHBEARER acknowledges an error continuation with 0x01, as curl
    // does; the others answer only what curl answers.
    private static readonly Dictionary<string, Func<SaslRequest, string[]>> MessagesByMechanism =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [SaslMechanismRanking.Plain] = request =>
                [(request.AuthorizationIdentity ?? string.Empty) + "\0" + UserOf(request) + "\0" + PasswordOf(request)],
            [SaslMechanismRanking.Login] = request => [UserOf(request), PasswordOf(request)],
            [SaslMechanismRanking.External] = request => [UserOf(request)],
            [SaslMechanismRanking.XOAuth2] = request =>
                [$"user={UserOf(request)}{FieldSeparator}auth=Bearer {TokenOf(request)}{FieldSeparator}{FieldSeparator}"],
            [SaslMechanismRanking.OAuthBearer] = request =>
                [OAuthBearerMessage(UserOf(request), request.Host, request.Port, TokenOf(request)), FieldSeparator.ToString()],
        };

    /// <summary>
    /// Gets the <c>--delegation</c> level GSSAPI's Kerberos context is asked for, as curl's
    /// GSS-API build passes <c>CURLOPT_GSSAPI_DELEGATION</c> to <c>gss_init_sec_context</c> for
    /// SASL too (BL-874); <see cref="SecurityDelegation.None" /> when not set. NTLM's context
    /// never delegates.
    /// </summary>
    public SecurityDelegation GssapiDelegation { get; init; }

    /// <summary>
    /// Gets whether a GSSAPI security-layer offer curl cannot answer is worded as curl's
    /// Windows (SSPI) build words it rather than as its GSS-API build does (BL-1336); the
    /// platform's build by default, so both wordings are testable everywhere.
    /// </summary>
    public bool WordsGssapiFailuresAsSspi { get; init; } = OperatingSystem.IsWindows();

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms)
    {
        string? picked = SaslMechanismRanking.PickFirst(request, offeredMechanisms, securityContexts is not null);
        log.SaslMechanismPicked(offeredMechanisms, picked);
        return picked;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// <paramref name="mechanism" /> is not one this authenticator builds, or is GSSAPI or NTLM
    /// and it has no security context factory.
    /// </exception>
    public ISaslExchange Begin(string mechanism, SaslRequest request)
    {
        string name = mechanism.ToUpperInvariant();
        log.Round("SASL " + name + " exchange begun");
        if (MessagesByMechanism.TryGetValue(name, out Func<SaslRequest, string[]>? buildMessages))
        {
            byte[][] messages = [.. buildMessages(request).Select(credentialEncoding.GetBytes)];
            return new ScriptedSaslExchange(name, messages[0], messages[1..]);
        }

        return securityContexts is not null && name is SaslMechanismRanking.Gssapi or SaslMechanismRanking.Ntlm
            ? BeginOnSecurityContext(name, request, securityContexts)
            : BeginAnsweringChallenges(name, mechanism, request);
    }

    // CRAM-MD5 and DIGEST-MD5, computed from the server's challenges.
    private ChallengeSaslExchange BeginAnsweringChallenges(string name, string mechanism, SaslRequest request)
    {
        return name switch
        {
            SaslMechanismRanking.CramMd5 => new ChallengeSaslExchange(name, challenge => CramMd5Answer(request, challenge)),
            SaslMechanismRanking.DigestMd5 => new ChallengeSaslExchange(name, challenge => DigestMd5Answer(request, challenge), _ => []),
            _ => throw new ArgumentException($"The SASL mechanism '{mechanism}' is not built.", nameof(mechanism)),
        };
    }

    /// <summary>
    /// Builds the OAUTHBEARER client message (RFC 7628 section 3.1) as curl 8.21.0 does.
    /// </summary>
    /// <param name="user">The user name, sent as the GS2 authorization identity <c>a=</c>.</param>
    /// <param name="host">The server's host name.</param>
    /// <param name="port">
    /// The server's port, <see cref="SaslRequest.Port" />, sent as <c>port=</c>; <c>0</c> leaves
    /// the field out, as curl does for port 0.
    /// </param>
    /// <param name="token">The bearer token.</param>
    /// <returns>The message, before encoding.</returns>
    internal static string OAuthBearerMessage(string user, string host, int port, string token)
    {
        string portField = port != 0
            ? "port=" + port.ToString(CultureInfo.InvariantCulture) + FieldSeparator
            : string.Empty;
        return $"n,a={user},{FieldSeparator}host={host}{FieldSeparator}{portField}auth=Bearer {token}{FieldSeparator}{FieldSeparator}";
    }

    // GSSAPI runs on the raw Kerberos mechanism with signing keys for its security-layer
    // message and the --delegation level; NTLM on an NTLM context with neither.
    private SecurityContextSaslExchange BeginOnSecurityContext(string name, SaslRequest request, ISecurityContextFactory contexts) =>
        name == SaslMechanismRanking.Gssapi
            ? new SecurityContextSaslExchange(
                name,
                contexts.Create(SecurityContextSaslExchange.ContextRequestFor(SecurityMechanism.Kerberos, request) with { MessageProtection = ProtectionLevel.Sign, Delegation = GssapiDelegation }),
                credentialEncoding.GetBytes(request.AuthorizationIdentity ?? string.Empty),
                WordsGssapiFailuresAsSspi)
            : new SecurityContextSaslExchange(name, contexts.Create(SecurityContextSaslExchange.ContextRequestFor(SecurityMechanism.Ntlm, request)), securityLayerAuthorizationIdentity: null, WordsGssapiFailuresAsSspi);

    // RFC 2195: the user name, a space, and the HMAC-MD5 of the challenge keyed with the
    // password, in lower-case hexadecimal.
    private byte[] CramMd5Answer(SaslRequest request, byte[] challenge) =>
        credentialEncoding.GetBytes(
            UserOf(request) + " " + Convert.ToHexStringLower(HMACMD5.HashData(credentialEncoding.GetBytes(PasswordOf(request)), challenge)));

    // The principal is service/host, as curl's Curl_auth_build_spn makes it. A challenge SSPI
    // rejects fails the transfer with exit 94 rather than cancelling it (BL-781).
    private byte[]? DigestMd5Answer(SaslRequest request, byte[] challenge)
    {
        string digestUri = request.ServiceName + "/" + request.Host;
        return answerDigestMd5AsSspi
            ? SaslDigestMd5.AnswerAsSspi(challenge, credentialEncoding, UserOf(request), PasswordOf(request), digestUri, createClientNonce())
                ?? throw DigestMd5Failed()
            : SaslDigestMd5.AnswerAsCurl(challenge, credentialEncoding, UserOf(request), PasswordOf(request), digestUri, createClientNonce());
    }

    private SaslAuthenticationFailedException DigestMd5Failed()
    {
        log.Failed(SaslMechanismRanking.DigestMd5, CurlExitCode.AuthError, AuthErrorMessage);
        return new SaslAuthenticationFailedException(CurlExitCode.AuthError, AuthErrorMessage);
    }

    // curl sends an empty user name, password or token for one that was not given.
    private static string UserOf(SaslRequest request) => request.Credential?.UserName ?? string.Empty;

    private static string PasswordOf(SaslRequest request) => request.Credential is { } credential ? credential.Password : string.Empty;

    private static string TokenOf(SaslRequest request) => request.BearerToken ?? string.Empty;
}
