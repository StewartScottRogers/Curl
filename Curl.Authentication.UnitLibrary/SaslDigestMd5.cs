using System.Security.Cryptography;
using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Answers a SASL DIGEST-MD5 challenge (RFC 2831) the way curl 8.21.0 does: its own code
/// in the OpenSSL build on Linux and macOS, Windows SSPI in the Schannel build on Windows,
/// which differ in the message they send (ADR-0139).
/// </summary>
internal static class SaslDigestMd5
{
    /// <summary>The only nonce count curl sends: it answers one challenge per exchange.</summary>
    private const string NonceCount = "00000001";

    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>
    /// Answers as curl's own DIGEST-MD5 code does (<c>Curl_auth_create_digest_md5_message</c>).
    /// </summary>
    /// <param name="challenge">The decoded challenge.</param>
    /// <param name="credentialEncoding">The encoding the user name and password are sent in.</param>
    /// <param name="user">The user name, sent as given.</param>
    /// <param name="password">The password.</param>
    /// <param name="digestUri">The <c>service/host</c> principal name.</param>
    /// <param name="clientNonce">The client nonce.</param>
    /// <returns>
    /// The response; <see langword="null" /> when the challenge has no <c>nonce</c>, no
    /// <c>qop</c> offering <c>auth</c>, or an <c>algorithm</c> other than <c>md5-sess</c>
    /// exactly, which curl cancels and fails with exit 67.
    /// </returns>
    /// <remarks>
    /// Each value is found as curl finds it, by the first occurrence of its key anywhere in
    /// the challenge, and cut at curl's buffer size less one: <c>nonce</c> 63 characters,
    /// <c>realm</c> 127, <c>algorithm</c> and <c>qop</c> 63. A missing <c>realm</c> is sent
    /// empty.
    /// </remarks>
    internal static byte[]? AnswerAsCurl(byte[] challenge, Encoding credentialEncoding, string user, string password, string digestUri, string clientNonce)
    {
        string text = ChallengeText(challenge);
        string? nonce = ValueAfter(text, "nonce=\"", '"', 64);
        string realm = ValueAfter(text, "realm=\"", '"', 128) ?? string.Empty;
        string? algorithm = ValueAfter(text, "algorithm=", ',', 64);
        string? qopOptions = ValueAfter(text, "qop=\"", '"', 64);
        if (nonce is null || algorithm != "md5-sess" || !OffersAuth(qopOptions))
        {
            return null;
        }

        string response = ResponseHex(credentialEncoding, user, realm, password, nonce, clientNonce, digestUri);
        return Concatenate(
            credentialEncoding,
            "username=\"",
            user,
            $"\",realm=\"{realm}\",nonce=\"{nonce}\",cnonce=\"{clientNonce}\",nc=\"{NonceCount}\",digest-uri=\"{digestUri}\",response={response},qop=auth");
    }

    /// <summary>
    /// Answers as Windows SSPI does for curl's Schannel build.
    /// </summary>
    /// <param name="challenge">The decoded challenge.</param>
    /// <param name="credentialEncoding">
    /// The encoding the user name and password are sent in when the challenge does not
    /// name <c>charset=utf-8</c>; with it, they are sent in UTF-8.
    /// </param>
    /// <param name="userWithDomain">
    /// The user name as given to curl. Up to the first backslash, or else the first slash, it
    /// is the domain, sent as the realm; the challenge's own realm is not used.
    /// </param>
    /// <param name="password">The password.</param>
    /// <param name="digestUri">The <c>service/host</c> principal name.</param>
    /// <param name="clientNonce">The client nonce.</param>
    /// <returns>
    /// The response, which ends <c>,charset=utf-8</c> when the challenge names it;
    /// <see langword="null" /> when the challenge has no <c>nonce</c>, an <c>algorithm</c>
    /// other than <c>md5-sess</c> in any case, or a <c>qop</c> without <c>auth</c>. SSPI
    /// fails those with exit 94, not 67, which <see cref="SaslAuthenticator" /> throws as a
    /// <see cref="Curl.Protocol.Abstractions.SaslAuthenticationFailedException" /> (BL-781).
    /// </returns>
    internal static byte[]? AnswerAsSspi(byte[] challenge, Encoding credentialEncoding, string userWithDomain, string password, string digestUri, string clientNonce)
    {
        string text = ChallengeText(challenge);
        if (NonceSspiAccepts(text) is not { } nonce)
        {
            return null;
        }

        bool isUtf8 = text.Contains("charset=utf-8", StringComparison.OrdinalIgnoreCase);
        Encoding encoding = isUtf8 ? Encoding.UTF8 : credentialEncoding;
        (string domain, string user) = SplitDomain(userWithDomain);
        string response = ResponseHex(HashEncoding(encoding, user + domain + password), user, domain, password, nonce, clientNonce, digestUri);
        return Concatenate(
            encoding,
            "username=\"",
            user,
            $"\",realm=\"{domain}\",nonce=\"{nonce}\",digest-uri=\"{digestUri}\",cnonce=\"{clientNonce}\",nc={NonceCount},response={response},qop=auth"
                + (isUtf8 ? ",charset=utf-8" : string.Empty));
    }

    // The challenge's nonce, or null when SSPI rejects the challenge: no nonce, an algorithm
    // other than md5-sess in any case, or a qop list without auth (none means auth).
    private static string? NonceSspiAccepts(string text)
    {
        string? nonce = ValueAfter(text, "nonce=\"", '"', int.MaxValue);
        string? algorithm = ValueAfter(text, "algorithm=", ',', int.MaxValue);
        string qopOptions = ValueAfter(text, "qop=\"", '"', int.MaxValue) ?? "auth";
        return string.Equals(algorithm, "md5-sess", StringComparison.OrdinalIgnoreCase) && OffersAuth(qopOptions) ? nonce : null;
    }

    // As curl's Schannel build splits a user name for SSPI: the domain up to the first
    // backslash, or else the first slash; no domain without either.
    private static (string Domain, string User) SplitDomain(string userWithDomain)
    {
        int separator = userWithDomain.IndexOf('\\') is var backslash and >= 0 ? backslash : userWithDomain.IndexOf('/');
        return (separator < 0 ? string.Empty : userWithDomain[..separator], userWithDomain[(separator + 1)..]);
    }

    // RFC 2831 section 2.1.2.1: under charset=utf-8, A1 is hashed in ISO 8859-1 when every
    // character fits it; the name is still sent in UTF-8.
    private static Encoding HashEncoding(Encoding encoding, string credentials) =>
        encoding is UTF8Encoding && credentials.All(character => character <= 'ÿ') ? Latin1 : encoding;

    // The challenge as C text: its bytes one character each, up to the first NUL.
    private static string ChallengeText(byte[] challenge) => Latin1.GetString(challenge).Split('\0')[0];

    // curl's auth_digest_get_key_value: the text after the first occurrence of key, up to
    // endCharacter or the end, at most capacity - 1 characters; null when key is absent.
    private static string? ValueAfter(string text, string key, char endCharacter, int capacity)
    {
        int start = text.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += key.Length;
        int end = text.IndexOf(endCharacter, start);
        int length = (end < 0 ? text.Length : end) - start;
        return text.Substring(start, Math.Min(length, capacity - 1));
    }

    // Whether the comma-separated qop list holds "auth", in any case.
    private static bool OffersAuth(string? qopOptions) =>
        qopOptions is not null && qopOptions.Split(',').Contains("auth", StringComparer.OrdinalIgnoreCase);

    // RFC 2831 section 2.1.2.1 for qop=auth: A1 is MD5(user:realm:password):nonce:cnonce.
    private static string ResponseHex(Encoding encoding, string user, string realm, string password, string nonce, string clientNonce, string digestUri)
    {
        byte[] userRealmPassword = MD5.HashData([.. encoding.GetBytes(user), .. Latin1.GetBytes($":{realm}:"), .. encoding.GetBytes(password)]);
        string ha1 = Convert.ToHexStringLower(MD5.HashData([.. userRealmPassword, .. Latin1.GetBytes($":{nonce}:{clientNonce}")]));
        string ha2 = Convert.ToHexStringLower(MD5.HashData(Latin1.GetBytes("AUTHENTICATE:" + digestUri)));
        return Convert.ToHexStringLower(MD5.HashData(Latin1.GetBytes($"{ha1}:{nonce}:{NonceCount}:{clientNonce}:auth:{ha2}")));
    }

    // The message with the user name in the credential encoding and the rest one byte per character.
    private static byte[] Concatenate(Encoding encoding, string before, string user, string after) =>
        [.. Latin1.GetBytes(before), .. encoding.GetBytes(user), .. Latin1.GetBytes(after)];
}
