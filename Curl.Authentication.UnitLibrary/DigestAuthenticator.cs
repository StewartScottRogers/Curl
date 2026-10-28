using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Answers Digest challenges (RFC 7616) with the <c>Authorization</c> value curl 8.21.0
/// builds in its own Digest code, the one the OpenSSL build uses (ADR-0025), or laid out as its
/// Schannel build's SSPI lays it out (<c>matchesSspiBuild</c>): MD5, SHA-256
/// and SHA-512-256, their <c>-sess</c> variants, <c>qop=auth</c> and <c>auth-int</c>, and
/// <c>userhash</c>. Matching the Schannel build, the challenge is read as SSPI reads it
/// (<see cref="DigestSspiChallenge" />) and one SSPI refuses, such as a SHA-256 or SHA-512-256
/// one, fails with exit 94 (ADR-0873, BL-3606).
/// </summary>
/// <param name="credentialEncoding">
/// The encoding the user name and password are hashed and sent in; see
/// <see cref="CredentialEncoding.ForPlatform" />.
/// </param>
/// <param name="createClientNonce">
/// Creates the <c>cnonce</c> for each answer; <see cref="DigestClientNonce.ForBuild(bool)" />
/// in production, a fixed value in tests.
/// </param>
/// <remarks>
/// It answers when Digest is allowed, a credential exists and the first Digest challenge
/// among the header values is one curl accepts; otherwise it returns
/// <see langword="null" />. It never answers before a challenge, as curl does not. Which
/// scheme to answer when several are offered and allowed is decided by
/// <see cref="RankedHttpAuthenticator" />. Keeping no
/// state (ADR-0014), every answer to a challenge is the first for its nonce: <c>nc=00000001</c>.
/// An answer sent again on a request that keeps it (<see cref="RepeatAuthorization" />) is
/// read back from the value as sent and counted on, as curl counts its kept nonce (BL-869).
/// <c>auth-int</c> hashes an empty body, as curl does whatever the body. The algorithm and qop
/// each first answer uses are written to the diagnostic log at <c>verbose</c> (BL-923).
/// </remarks>
/// <param name="diagnosticLog">Where the algorithm and qop chosen are logged; <see langword="null" /> logs nothing.</param>
/// <param name="matchesSspiBuild">
/// <see langword="true" /> writes the value as curl 8.21.0's Schannel build does through SSPI
/// (BL-2022): no blank after each comma, <c>algorithm</c> before <c>response</c>, <c>qop</c>
/// quoted after it and <c>opaque</c> last. <see langword="false" />, the default, writes curl's
/// own form, the OpenSSL build's: <c>, </c> between parameters in RFC 7616's order.
/// </param>
public sealed class DigestAuthenticator(Encoding credentialEncoding, Func<string> createClientNonce, IDiagnosticLog? diagnosticLog = null, bool matchesSspiBuild = false) : IHttpAuthenticator
{
    private readonly AuthDiagnosticLog log = new(diagnosticLog);

    private const string SchemeName = "Digest";

    private const uint FirstNonceCount = 1;

    private const string LoginDeniedMessage = "Login denied";

    /// <inheritdoc />
    /// <exception cref="HttpAuthenticationFailedException">
    /// <c>matchesSspiBuild</c> is set and SSPI refuses the challenge (<see cref="DigestSspiChallenge" />)
    /// or a user name with a control character: exit 94; or the user name is empty: exit 67,
    /// "Login denied", as curl's Schannel build (ADR-0873, BL-3606).
    /// </exception>
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(challenges);

        if ((request.AllowedSchemes & HttpAuthSchemes.Digest) == 0 || request.Credential is not { } credential)
        {
            return null;
        }

        return matchesSspiBuild
            ? CreateSspiAnswer(challenges, credential, request)
            : DigestChallenge.ReadFirst(challenges) is { } challenge
                ? CreateFirstAnswer(challenge, credential.UserName, challenge.Realm, credential, request)
                : null;
    }

    // The Schannel build's answer: the user name's domain, when it has one, is the realm.
    private string? CreateSspiAnswer(IReadOnlyList<string> challenges, NetworkCredential credential, HttpAuthRequest request)
    {
        if (!DigestChallenge.TryReadFirst(challenges, DigestSspiChallenge.Read, out DigestChallenge? challenge))
        {
            return null;
        }

        return challenge is null
            ? throw Refuse(CurlExitCode.AuthError, NtlmHttpAuthenticator.AuthErrorMessage)
            : CreateSspiAnswer(challenge, credential, request);
    }

    // SSPI refuses an empty user name (exit 67) and one with a control character (exit 94).
    private string CreateSspiAnswer(DigestChallenge challenge, NetworkCredential credential, HttpAuthRequest request)
    {
        (string user, string? domain) = SplitSspiUser(credential.UserName);
        if (credential.UserName.Length == 0)
        {
            throw Refuse(CurlExitCode.LoginDenied, LoginDeniedMessage);
        }

        return user.AsSpan().IndexOfAnyInRange('\0', '\u001f') >= 0
            ? throw Refuse(CurlExitCode.AuthError, NtlmHttpAuthenticator.AuthErrorMessage)
            : CreateFirstAnswer(challenge, user, domain is null ? challenge.Realm : ToByteString(domain), credential, request);
    }

    private HttpAuthenticationFailedException Refuse(CurlExitCode exitCode, string message)
    {
        log.Failed(SchemeName, exitCode, message);
        return new HttpAuthenticationFailedException(exitCode, message);
    }

    // Curl_create_sspi_identity: the domain is what comes before the first backslash, or
    // else the first slash; an empty one is no domain.
    private static (string User, string? Domain) SplitSspiUser(string userName)
    {
        int separator = userName.IndexOf('\\');
        separator = separator < 0 ? userName.IndexOf('/') : separator;
        return (userName[(separator + 1)..], separator > 0 ? userName[..separator] : null);
    }

    private string CreateFirstAnswer(DigestChallenge challenge, string userName, string? realm, NetworkCredential credential, HttpAuthRequest request)
    {
        log.DigestParametersChosen(challenge.AlgorithmName, challenge.Qop);
        return SchemeName + " " + CreateResponse(challenge, userName, realm, credential.Password, request, createClientNonce(), FirstNonceCount);
    }

    /// <summary>
    /// Sends a Digest answer this authenticator made once more with its nonce counted on: the
    /// same <c>cnonce</c>, the next <c>nc</c> and the hash for that count, as curl 8.21.0 does
    /// when a request keeps the answer while it answers the other party's challenge (BL-869).
    /// </summary>
    /// <param name="request">The request being authorised; its method and target are hashed again.</param>
    /// <param name="sentAuthorization">The header value the last request sent.</param>
    /// <returns>
    /// The answer counted on; or <paramref name="sentAuthorization" /> as sent when there is no
    /// credential, or it is not a Digest answer with a <c>qop</c>, a <c>cnonce</c> and an
    /// <c>nc</c>, which curl has nothing to count.
    /// </returns>
    public string RepeatAuthorization(HttpAuthRequest request, string sentAuthorization)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sentAuthorization);

        if (request.Credential is not { } credential || ReadSentAnswer(sentAuthorization) is not { } sent)
        {
            return sentAuthorization;
        }

        string userName = matchesSspiBuild ? SplitSspiUser(credential.UserName).User : credential.UserName;
        return SchemeName + " " + CreateResponse(sent.Challenge, userName, sent.Challenge.Realm, credential.Password, request, sent.ClientNonce, sent.NonceCount + 1);
    }

    // What counting a sent answer on needs: its challenge's parameters, cnonce and nc.
    private static (DigestChallenge Challenge, string ClientNonce, uint NonceCount)? ReadSentAnswer(string sentAuthorization)
    {
        if (!sentAuthorization.StartsWith(SchemeName + " ", StringComparison.Ordinal))
        {
            return null;
        }

        IReadOnlyList<KeyValuePair<string, string>> pairs = DigestChallengeParameters.ReadPairs(sentAuthorization, SchemeName.Length)!;
        return DigestChallengeParameters.Read(sentAuthorization, SchemeName.Length) is { Qop: not null } challenge
            && ValueOf(pairs, "cnonce") is { } clientNonce
            && uint.TryParse(ValueOf(pairs, "nc"), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint nonceCount)
                ? (challenge, clientNonce, nonceCount)
                : null;
    }

    private static string? ValueOf(IReadOnlyList<KeyValuePair<string, string>> pairs, string key) =>
        pairs.Where(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value).LastOrDefault();

    // The realm is the challenge's, or the SSPI user name's domain; no realm hashes as empty.
    private string CreateResponse(DigestChallenge challenge, string userName, string? challengeRealm, string password, HttpAuthRequest request, string clientNonce, uint nonceCountValue)
    {
        DigestAlgorithm algorithm = challenge.Algorithm;
        string realm = challengeRealm ?? string.Empty;
        string user = ToByteString(userName);
        string nonceCount = nonceCountValue.ToString("x8", CultureInfo.InvariantCulture);

        string ha1 = algorithm.HashToHex(user + ":" + realm + ":" + ToByteString(password));
        ha1 = algorithm.IsSession ? algorithm.HashToHex(ha1 + ":" + challenge.Nonce + ":" + clientNonce) : ha1;
        string a2 = request.Method + ":" + request.RequestTarget;
        a2 += challenge.Qop == "auth-int" ? ":" + algorithm.HashToHex(string.Empty) : string.Empty;
        string ha2 = algorithm.HashToHex(a2);
        string response = algorithm.HashToHex(challenge.Qop is { } qop
            ? string.Join(':', ha1, challenge.Nonce, nonceCount, clientNonce, qop, ha2)
            : string.Join(':', ha1, challenge.Nonce, ha2));
        return FormatValue(challenge, request.RequestTarget, challenge.UserHash ? algorithm.HashToHex(user + ":" + realm) : user, realm, clientNonce, nonceCount, response);
    }

    private string FormatValue(DigestChallenge challenge, string uri, string username, string realm, string clientNonce, string nonceCount, string response) =>
        matchesSspiBuild
            ? FormatSspiValue(challenge, uri, username, realm, clientNonce, nonceCount, response)
            : FormatOwnValue(challenge, uri, username, realm, clientNonce, nonceCount, response);

    // The order SSPI writes, measured from curl 8.21.0's Schannel build (BL-2022): no blank
    // after a comma, algorithm before the response, qop quoted after it, then opaque and
    // charset. Only the user name and realm are escaped; the nonce, uri and opaque are sent
    // as they are (BL-3606).
    private static string FormatSspiValue(DigestChallenge challenge, string uri, string username, string realm, string clientNonce, string nonceCount, string response)
    {
        StringBuilder value = new();
        value.Append("username=\"").Append(DigestQuoting.QuoteAsSspi(username));
        value.Append("\",realm=\"").Append(DigestQuoting.QuoteAsSspi(realm));
        value.Append("\",nonce=\"").Append(challenge.Nonce);
        value.Append("\",uri=\"").Append(uri).Append("\",");
        value.Append(challenge.Qop is null ? string.Empty : $"cnonce=\"{clientNonce}\",nc={nonceCount},");
        value.Append(challenge.AlgorithmName is null ? string.Empty : "algorithm=" + challenge.AlgorithmName + ",");
        value.Append("response=\"").Append(response).Append('"');
        value.Append(challenge.Qop is null ? string.Empty : $",qop=\"{challenge.Qop}\"");
        value.Append(challenge.Opaque is null ? string.Empty : ",opaque=\"" + challenge.Opaque + "\"");
        value.Append(challenge.SendsUtf8Charset ? ",charset=utf-8" : string.Empty);
        return value.ToString();
    }

    // The order curl's own Digest code writes (vauth/digest.c), the OpenSSL build's.
    private static string FormatOwnValue(DigestChallenge challenge, string uri, string username, string realm, string clientNonce, string nonceCount, string response)
    {
        StringBuilder value = new();
        value.Append("username=\"").Append(DigestQuoting.Quote(username));
        value.Append("\", realm=\"").Append(DigestQuoting.Quote(realm));
        value.Append("\", nonce=\"").Append(DigestQuoting.Quote(challenge.Nonce));
        value.Append("\", uri=\"").Append(DigestQuoting.Quote(uri)).Append("\", ");
        value.Append(challenge.Qop is null ? string.Empty : $"cnonce=\"{clientNonce}\", nc={nonceCount}, qop={challenge.Qop}, ");
        value.Append("response=\"").Append(response).Append('"');
        value.Append(challenge.Opaque is null ? string.Empty : ", opaque=\"" + DigestQuoting.Quote(challenge.Opaque) + "\"");
        value.Append(challenge.AlgorithmName is null ? string.Empty : ", algorithm=" + challenge.AlgorithmName);
        value.Append(challenge.UserHash ? ", userhash=true" : string.Empty);
        return value.ToString();
    }

    // The credential's bytes in the platform encoding, one character per byte.
    private string ToByteString(string text) => Encoding.Latin1.GetString(credentialEncoding.GetBytes(text));
}
