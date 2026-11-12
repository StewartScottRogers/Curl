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
    /// <c>qop</c> does not offer <c>auth</c>, which this answer cannot satisfy.
    /// </returns>
    internal static byte[]? AnswerRequest(LdapBerWriter writer, int messageId, ReadOnlySpan<byte> challenge, string user, string password, string digestUri, string clientNonce)
    {
        Dictionary<string, string> directives = ParseChallenge(Encoding.Latin1.GetString(challenge));
        if (!directives.TryGetValue("nonce", out string? nonce)
            || !directives.GetValueOrDefault("qop", "auth").Split(',').Any(option => option.Trim().Equals("auth", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        string realm = directives.GetValueOrDefault("realm", string.Empty);
        bool isUtf8 = directives.GetValueOrDefault("charset", string.Empty).Equals("utf-8", StringComparison.OrdinalIgnoreCase);
        Encoding encoding = isUtf8 ? Encoding.UTF8 : Encoding.Latin1;
        string response = ResponseHex(encoding, user, realm, password, nonce, clientNonce, digestUri);
        string text = $"username=\"{Quoted(user)}\",realm=\"{Quoted(realm)}\",nonce=\"{Quoted(nonce)}\",cnonce=\"{Quoted(clientNonce)}\",nc={NonceCount},qop=auth,digest-uri=\"{Quoted(digestUri)}\",response={response}"
            + (isUtf8 ? ",charset=utf-8" : string.Empty);
        return LdapRequests.SaslBind(writer, messageId, Mechanism, encoding.GetBytes(text));
    }

    /// <summary>
    /// RFC 2831 section 2.1.2.1: HEX(KD(HEX(H(A1)), nonce:nc:cnonce:qop:HEX(H(A2)))) for
    /// algorithm <c>md5-sess</c>, <c>qop=auth</c>.
    /// </summary>
    internal static string ResponseHex(Encoding encoding, string user, string realm, string password, string nonce, string clientNonce, string digestUri)
    {
        byte[] secret = MD5.HashData(encoding.GetBytes($"{user}:{realm}:{password}"));
        byte[] a1 = [.. secret, .. encoding.GetBytes($":{nonce}:{clientNonce}")];
        string a1Hex = Convert.ToHexStringLower(MD5.HashData(a1));
        string a2Hex = Convert.ToHexStringLower(MD5.HashData(encoding.GetBytes($"AUTHENTICATE:{digestUri}")));
        return Convert.ToHexStringLower(MD5.HashData(encoding.GetBytes($"{a1Hex}:{nonce}:{NonceCount}:{clientNonce}:auth:{a2Hex}")));
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
