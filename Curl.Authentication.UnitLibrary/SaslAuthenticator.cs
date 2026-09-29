using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Chooses a SASL mechanism as curl 8.21.0 does and answers PLAIN, LOGIN, EXTERNAL, XOAUTH2,
/// OAUTHBEARER, CRAM-MD5 and DIGEST-MD5 with the bytes curl sends (ADR-0121, ADR-0123,
/// ADR-0139).
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
/// <remarks>
/// <para>
/// The preference order is EXTERNAL, GSSAPI, DIGEST-MD5, CRAM-MD5, NTLM, OAUTHBEARER,
/// XOAUTH2, PLAIN, LOGIN. GSSAPI and NTLM are not built yet (BL-538) and are treated as not
/// offered.
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
/// </remarks>
public sealed class SaslAuthenticator(Encoding credentialEncoding, Func<string> createClientNonce, bool answerDigestMd5AsSspi) : ISaslAuthenticator
{
    /// <summary>
    /// Initializes an authenticator that answers DIGEST-MD5 as the platform's curl does, with
    /// a random client nonce.
    /// </summary>
    /// <param name="credentialEncoding">
    /// The encoding the user name, password, authorization identity and token are sent in;
    /// see <see cref="CredentialEncoding.ForPlatform" />.
    /// </param>
    public SaslAuthenticator(Encoding credentialEncoding)
        : this(credentialEncoding, DigestClientNonce.CreateRandomHex, OperatingSystem.IsWindows())
    {
    }

    /// <summary>The byte OAUTHBEARER and XOAUTH2 separate their fields with (RFC 7628 section 3.1).</summary>
    private const char FieldSeparator = '\u0001';

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
                [OAuthBearerMessage(UserOf(request), request.Host, port: null, TokenOf(request)), FieldSeparator.ToString()],
        };

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms) =>
        SaslMechanismRanking.PickFirst(request, offeredMechanisms);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// <paramref name="mechanism" /> is not one this authenticator builds.
    /// </exception>
    public ISaslExchange Begin(string mechanism, SaslRequest request)
    {
        string name = mechanism.ToUpperInvariant();
        if (MessagesByMechanism.TryGetValue(name, out Func<SaslRequest, string[]>? buildMessages))
        {
            byte[][] messages = [.. buildMessages(request).Select(credentialEncoding.GetBytes)];
            return new ScriptedSaslExchange(name, messages[0], messages[1..]);
        }

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
    /// The server's port, which curl always sends; <see langword="null" /> leaves the
    /// <c>port=</c> field out, as curl does for port 0, until <see cref="SaslRequest" />
    /// carries the port.
    /// </param>
    /// <param name="token">The bearer token.</param>
    /// <returns>The message, before encoding.</returns>
    internal static string OAuthBearerMessage(string user, string host, int? port, string token)
    {
        string portField = port is { } value
            ? "port=" + value.ToString(CultureInfo.InvariantCulture) + FieldSeparator
            : string.Empty;
        return $"n,a={user},{FieldSeparator}host={host}{FieldSeparator}{portField}auth=Bearer {token}{FieldSeparator}{FieldSeparator}";
    }

    // RFC 2195: the user name, a space, and the HMAC-MD5 of the challenge keyed with the
    // password, in lower-case hexadecimal.
    private byte[] CramMd5Answer(SaslRequest request, byte[] challenge) =>
        credentialEncoding.GetBytes(
            UserOf(request) + " " + Convert.ToHexStringLower(HMACMD5.HashData(credentialEncoding.GetBytes(PasswordOf(request)), challenge)));

    // The principal is service/host, as curl's Curl_auth_build_spn makes it.
    private byte[]? DigestMd5Answer(SaslRequest request, byte[] challenge)
    {
        string digestUri = request.ServiceName + "/" + request.Host;
        return answerDigestMd5AsSspi
            ? SaslDigestMd5.AnswerAsSspi(challenge, credentialEncoding, UserOf(request), PasswordOf(request), digestUri, createClientNonce())
            : SaslDigestMd5.AnswerAsCurl(challenge, credentialEncoding, UserOf(request), PasswordOf(request), digestUri, createClientNonce());
    }

    // curl sends an empty user name, password or token for one that was not given.
    private static string UserOf(SaslRequest request) => request.Credential?.UserName ?? string.Empty;

    private static string PasswordOf(SaslRequest request) => request.Credential is { } credential ? credential.Password : string.Empty;

    private static string TokenOf(SaslRequest request) => request.BearerToken ?? string.Empty;
}
