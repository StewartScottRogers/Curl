using System.Security.Cryptography;
using System.Text;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Builds the two BindRequests of the LDAP DIGEST-MD5 SASL bind (RFC 4513 section 5.2.1.3,
/// RFC 2831): the initial one that names the mechanism, and the one that answers the server's
/// challenge with a digest-response. The password is hashed into the response, never sent.
/// </summary>
internal static class LdapDigestMd5Bind
{
    /// <summary>The SASL mechanism's name.</summary>
    internal const string Mechanism = "DIGEST-MD5";

    /// <summary>The only nonce count sent: one challenge is answered per bind.</summary>
    private const string NonceCount = "00000001";

    private const string QopConfidential = "auth-conf";

    /// <summary>
    /// Encodes the initial BindRequest: SASL authentication (tag 0xa3) naming
    /// <see cref="Mechanism" /> with empty credentials, so the server answers with its challenge.
    /// </summary>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <returns>The LDAPMessage.</returns>
    internal static byte[] InitialRequest(LdapBerWriter writer, int messageId) =>
        LdapRequests.SaslBind(writer, messageId, Mechanism, []);

    /// <summary>
    /// Encodes the BindRequest that answers <paramref name="challenge" /> with a digest-response.
    /// </summary>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <param name="challenge">The server's digest-challenge, from its bind response's server credentials.</param>
    /// <param name="user">The user name.</param>
    /// <param name="password">The password, hashed into the response.</param>
    /// <param name="digestUri">The digest-uri, <c>ldap/host</c>.</param>
    /// <param name="clientNonce">The client nonce.</param>
    /// <returns>
    /// The LDAPMessage; <see langword="null" /> when the challenge has no <c>nonce</c> or its
    /// <c>qop</c> offers neither <c>auth-conf</c> with a supported cipher nor, when <paramref name="acceptsQopAuth" />,
    /// <c>auth</c>: WinLDAP refuses to bind without the security layer rather than fall back to <c>qop=auth</c> (ADR-1302).
    /// </returns>
    /// <param name="acceptsQopAuth">Whether <c>qop=auth</c>, with no security layer, may answer a challenge that offers no
    /// <c>auth-conf</c> with a supported cipher: the OpenLDAP dialect's, never WinLDAP's.</param>
    internal static byte[]? AnswerRequest(LdapBerWriter writer, int messageId, ReadOnlySpan<byte> challenge, string user, string password, string digestUri, string clientNonce, bool acceptsQopAuth = false)
    {
        Dictionary<string, string> directives = ParseChallenge(Encoding.Latin1.GetString(challenge));
        if (!directives.TryGetValue("nonce", out string? nonce) || ChooseQop(directives, acceptsQopAuth) is not { } qop)
        {
            return null;
        }

        bool confidential = qop == QopConfidential;
        string realm = directives.GetValueOrDefault("realm", string.Empty);
        bool isUtf8 = IsUtf8(directives);
        Encoding encoding = isUtf8 ? Encoding.UTF8 : Encoding.Latin1;
        string response = ResponseHex(encoding, user, realm, password, nonce, clientNonce, digestUri, confidential);
        string text = $"username=\"{Quoted(user)}\",realm=\"{Quoted(realm)}\",nonce=\"{Quoted(nonce)}\",cnonce=\"{Quoted(clientNonce)}\",nc={NonceCount},qop={qop}"
            + (confidential ? $",cipher={ChooseCipher(directives)}" : string.Empty)
            + $",digest-uri=\"{Quoted(digestUri)}\",response={response}"
            + (isUtf8 ? ",charset=utf-8" : string.Empty);
        return LdapRequests.SaslBind(writer, messageId, Mechanism, encoding.GetBytes(text));
    }

    /// <summary>
    /// Creates the RFC 2831 confidentiality layer when <see cref="AnswerRequest" /> answers
    /// <paramref name="challenge" /> with <c>qop=auth-conf</c>; the session's later PDUs travel through it.
    /// </summary>
    /// <param name="challenge">The server's digest-challenge.</param>
    /// <param name="user">The user name.</param>
    /// <param name="password">The password.</param>
    /// <param name="clientNonce">The client nonce sent in the answer.</param>
    /// <returns>The layer; <see langword="null" /> when no answer was possible.</returns>
    internal static LdapDigestMd5SecurityLayer? SecurityLayerFor(ReadOnlySpan<byte> challenge, string user, string password, string clientNonce)
    {
        Dictionary<string, string> directives = ParseChallenge(Encoding.Latin1.GetString(challenge));
        if (!directives.TryGetValue("nonce", out string? nonce) || ChooseQop(directives, acceptsQopAuth: false) is null)
        {
            return null;
        }

        Encoding encoding = IsUtf8(directives) ? Encoding.UTF8 : Encoding.Latin1;
        return LdapDigestMd5SecurityLayer.Create(HashA1(encoding, user, directives.GetValueOrDefault("realm", string.Empty), password, nonce, clientNonce), maxBuffer: MaxBufferOf(directives), cipher: ChooseCipher(directives)!);
    }

    private static bool IsUtf8(Dictionary<string, string> directives) =>
        directives.GetValueOrDefault("charset", string.Empty).Equals("utf-8", StringComparison.OrdinalIgnoreCase);

    // The challenge's maxbuf when it is a positive number, else the RFC 2831 default.
    private static int MaxBufferOf(Dictionary<string, string> directives) =>
        int.TryParse(directives.GetValueOrDefault("maxbuf", string.Empty), out int maxBuffer) && maxBuffer > 0
            ? maxBuffer
            : LdapDigestMd5SecurityLayer.DefaultMaxBuffer;

    /// <summary>
    /// RFC 2831 section 2.1.2.1: HEX(KD(HEX(H(A1)), nonce:nc:cnonce:qop:HEX(H(A2)))) for
    /// algorithm <c>md5-sess</c>; <c>qop=auth</c>, or <c>auth-conf</c> when <paramref name="confidential" />
    /// (whose A2 ends in the 32 zeros of the unused message hash).
    /// </summary>
    internal static string ResponseHex(Encoding encoding, string user, string realm, string password, string nonce, string clientNonce, string digestUri, bool confidential = false)
    {
        string a1Hex = Convert.ToHexStringLower(HashA1(encoding, user, realm, password, nonce, clientNonce));
        string a2Hex = Convert.ToHexStringLower(MD5.HashData(encoding.GetBytes($"AUTHENTICATE:{digestUri}" + (confidential ? ":00000000000000000000000000000000" : string.Empty))));
        return Convert.ToHexStringLower(MD5.HashData(encoding.GetBytes($"{a1Hex}:{nonce}:{NonceCount}:{clientNonce}:{(confidential ? QopConfidential : "auth")}:{a2Hex}")));
    }

    // H(A1) of algorithm md5-sess: MD5(MD5(user:realm:password):nonce:cnonce).
    private static byte[] HashA1(Encoding encoding, string user, string realm, string password, string nonce, string clientNonce)
    {
        byte[] secret = MD5.HashData(encoding.GetBytes($"{user}:{realm}:{password}"));
        return MD5.HashData([.. secret, .. encoding.GetBytes($":{nonce}:{clientNonce}")]);
    }

    // The first of the supported ciphers the challenge offers (3des, then the rc4 family from the strongest), else null.
    private static string? ChooseCipher(Dictionary<string, string> directives)
    {
        string[] offered = directives.GetValueOrDefault("cipher", string.Empty).Split(',', StringSplitOptions.TrimEntries);
        return SupportedCiphers.FirstOrDefault(cipher => offered.Contains(cipher, StringComparer.OrdinalIgnoreCase));
    }

    private static readonly string[] SupportedCiphers = ["3des", "rc4", "rc4-56", "rc4-40"];

    // The OpenLDAP dialect (acceptsQopAuth) answers qop=auth only, as curl's own vauth digest does (BL-5525); WinLDAP takes auth-conf when offered with a supported cipher, else null (ADR-1302).
    private static string? ChooseQop(Dictionary<string, string> directives, bool acceptsQopAuth)
    {
        string[] qops = directives.GetValueOrDefault("qop", "auth").Split(',', StringSplitOptions.TrimEntries);
        bool offersConf = qops.Contains(QopConfidential, StringComparer.OrdinalIgnoreCase)
            && ChooseCipher(directives) is not null;
        return acceptsQopAuth ? (qops.Contains("auth", StringComparer.OrdinalIgnoreCase) ? "auth" : null) : offersConf ? QopConfidential : null;
    }

    // Escapes each backslash and double quote for a quoted-string.
    private static string Quoted(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    // The challenge's key=value directives, a quoted value ending at its closing quote (a
    // backslash takes the next character as it is), an unquoted one at the next comma. A key
    // seen twice keeps its first value.
    private static Dictionary<string, string> ParseChallenge(string text)
    {
        Dictionary<string, string> directives = new(StringComparer.OrdinalIgnoreCase);
        int position = 0;
        while (text.IndexOf('=', position) is var equals and >= 0)
        {
            string key = text[position..equals].Trim(' ', ',');
            position = equals + 1;
            (string value, position) = position < text.Length && text[position] == '"'
                ? ReadQuoted(text, position + 1)
                : ReadUnquoted(text, position);
            directives.TryAdd(key, value);
        }

        return directives;
    }

    // The quoted value from start and the position after its closing quote (or the end).
    private static (string Value, int Next) ReadQuoted(string text, int start)
    {
        StringBuilder value = new();
        int position = start;
        while (position < text.Length && text[position] != '"')
        {
            position += text[position] == '\\' && position + 1 < text.Length ? 1 : 0;
            value.Append(text[position++]);
        }

        return (value.ToString(), Math.Min(position + 1, text.Length));
    }

    // The unquoted value from start up to the next comma, and the position of that comma.
    private static (string Value, int Next) ReadUnquoted(string text, int start)
    {
        int comma = text.IndexOf(',', start);
        int end = comma < 0 ? text.Length : comma;
        return (text[start..end].Trim(), end);
    }
}
