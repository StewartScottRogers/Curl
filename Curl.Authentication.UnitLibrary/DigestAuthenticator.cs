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
/// state (ADR-0014), every answer is the first for its nonce: <c>nc=00000001</c>.
/// <c>auth-int</c> hashes an empty body, as curl does whatever the body.
/// </remarks>
public sealed class DigestAuthenticator(Encoding credentialEncoding, Func<string> createClientNonce) : IHttpAuthenticator
{
    private const string NonceCount = "00000001";

    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(challenges);

        return (request.AllowedSchemes & HttpAuthSchemes.Digest) != 0
            && request.Credential is { } credential
            && DigestChallenge.ReadFirst(challenges) is { } challenge
                ? "Digest " + CreateResponse(challenge, credential, request)
                : null;
    }

    private string CreateResponse(DigestChallenge challenge, NetworkCredential credential, HttpAuthRequest request)
    {
        DigestAlgorithm algorithm = challenge.Algorithm;
        string user = ToByteString(credential.UserName);
        string realm = challenge.Realm ?? string.Empty;
        string clientNonce = createClientNonce();

        string ha1 = algorithm.HashToHex(user + ":" + realm + ":" + ToByteString(credential.Password));
        ha1 = algorithm.IsSession ? algorithm.HashToHex(ha1 + ":" + challenge.Nonce + ":" + clientNonce) : ha1;
        string a2 = request.Method + ":" + request.RequestTarget;
        a2 += challenge.Qop == "auth-int" ? ":" + algorithm.HashToHex(string.Empty) : string.Empty;
        string ha2 = algorithm.HashToHex(a2);
        string response = algorithm.HashToHex(challenge.Qop is { } qop
            ? string.Join(':', ha1, challenge.Nonce, NonceCount, clientNonce, qop, ha2)
            : string.Join(':', ha1, challenge.Nonce, ha2));
        return FormatValue(challenge, request.RequestTarget, challenge.UserHash ? algorithm.HashToHex(user + ":" + realm) : user, realm, clientNonce, response);
    }

    private static string FormatValue(DigestChallenge challenge, string uri, string username, string realm, string clientNonce, string response)
    {
        StringBuilder value = new();
        value.Append("username=\"").Append(DigestQuoting.Quote(username));
        value.Append("\", realm=\"").Append(DigestQuoting.Quote(realm));
        value.Append("\", nonce=\"").Append(DigestQuoting.Quote(challenge.Nonce));
        value.Append("\", uri=\"").Append(DigestQuoting.Quote(uri)).Append("\", ");
        value.Append(challenge.Qop is null ? string.Empty : $"cnonce=\"{clientNonce}\", nc={NonceCount}, qop={challenge.Qop}, ");
        value.Append("response=\"").Append(response).Append('"');
        value.Append(challenge.Opaque is null ? string.Empty : ", opaque=\"" + DigestQuoting.Quote(challenge.Opaque) + "\"");
        value.Append(challenge.AlgorithmName is null ? string.Empty : ", algorithm=" + challenge.AlgorithmName);
        value.Append(challenge.UserHash ? ", userhash=true" : string.Empty);
        return value.ToString();
    }

    // The credential's bytes in the platform encoding, one character per byte.
    private string ToByteString(string text) => Encoding.Latin1.GetString(credentialEncoding.GetBytes(text));
}
