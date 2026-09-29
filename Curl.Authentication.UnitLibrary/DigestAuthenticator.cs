using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Answers Digest challenges (RFC 7616) with the <c>Authorization</c> value curl 8.21.0
/// builds in its own Digest code, the one the OpenSSL build uses (ADR-0025): MD5, SHA-256
/// and SHA-512-256, their <c>-sess</c> variants, <c>qop=auth</c> and <c>auth-int</c>, and
/// <c>userhash</c>.
/// </summary>
/// <param name="credentialEncoding">
/// The encoding the user name and password are hashed and sent in; see
/// <see cref="CredentialEncoding.ForPlatform" />.
/// </param>
/// <param name="createClientNonce">
/// Creates the <c>cnonce</c> for each answer; <see cref="DigestClientNonce.CreateRandom" />
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
/// <c>auth-int</c> hashes an empty body, as curl does whatever the body.
/// </remarks>
public sealed class DigestAuthenticator(Encoding credentialEncoding, Func<string> createClientNonce) : IHttpAuthenticator
{
    private const string SchemeName = "Digest";

    private const uint FirstNonceCount = 1;

    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(challenges);

        return (request.AllowedSchemes & HttpAuthSchemes.Digest) != 0
            && request.Credential is { } credential
            && DigestChallenge.ReadFirst(challenges) is { } challenge
                ? SchemeName + " " + CreateResponse(challenge, credential, request, createClientNonce(), FirstNonceCount)
                : null;
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

        return request.Credential is { } credential && ReadSentAnswer(sentAuthorization) is { } sent
            ? SchemeName + " " + CreateResponse(sent.Challenge, credential, request, sent.ClientNonce, sent.NonceCount + 1)
            : sentAuthorization;
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

    private string CreateResponse(DigestChallenge challenge, NetworkCredential credential, HttpAuthRequest request, string clientNonce, uint nonceCountValue)
    {
        DigestAlgorithm algorithm = challenge.Algorithm;
        string user = ToByteString(credential.UserName);
        string realm = challenge.Realm ?? string.Empty;
        string nonceCount = nonceCountValue.ToString("x8", CultureInfo.InvariantCulture);

        string ha1 = algorithm.HashToHex(user + ":" + realm + ":" + ToByteString(credential.Password));
        ha1 = algorithm.IsSession ? algorithm.HashToHex(ha1 + ":" + challenge.Nonce + ":" + clientNonce) : ha1;
        string a2 = request.Method + ":" + request.RequestTarget;
        a2 += challenge.Qop == "auth-int" ? ":" + algorithm.HashToHex(string.Empty) : string.Empty;
        string ha2 = algorithm.HashToHex(a2);
        string response = algorithm.HashToHex(challenge.Qop is { } qop
            ? string.Join(':', ha1, challenge.Nonce, nonceCount, clientNonce, qop, ha2)
            : string.Join(':', ha1, challenge.Nonce, ha2));
        return FormatValue(challenge, request.RequestTarget, challenge.UserHash ? algorithm.HashToHex(user + ":" + realm) : user, realm, clientNonce, nonceCount, response);
    }

    private static string FormatValue(DigestChallenge challenge, string uri, string username, string realm, string clientNonce, string nonceCount, string response)
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
