using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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

    /// <summary>The directives SSPI rejects a challenge for repeating.</summary>
    private static readonly string[] SspiSingleKeys = ["nonce", "algorithm", "charset"];

    // Interpreted, not source generated: the generated matcher would fall under the quality gates.
    // Blanks, a key up to "=", a quoted or unquoted value and any comma after it; each match
    // starts where the one before ended, which must be the start or just after a comma.
    private static readonly Regex CurlDirective = new(
        """\G(?<=\A|,)[ \t]*(?<key>[^=]*)=(?:"(?<quoted>(?:\\.|[^"\\])*)"|(?<plain>(?!")[^,]*)),?""",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    // One directive as SSPI reads it, with the end of the text or the commas after it.
    private static readonly Regex SspiDirective = new(
        """\G[ \t]*(?<key>[^=,]+)=[ \t]*(?:"(?<quoted>(?:\\.|[^"\\])*)"[ \t]*|(?<plain>(?![ \t"])[^,]*)|)(?:\z|,[ \t,]*(?!\z))""",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    // A backslash and the character it escapes in a quoted value.
    private static readonly Regex QuotedPair = new("""\\(.)""", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>
    /// Answers as curl's own DIGEST-MD5 code does (<c>Curl_auth_create_digest_md5_message</c>).
    /// </summary>
    /// <param name="challenge">The decoded challenge.</param>
    /// <param name="credentialEncoding">The encoding the user name and password are sent in.</param>
    /// <param name="user">The user name, hashed as given.</param>
    /// <param name="password">The password.</param>
    /// <param name="digestUri">The <c>service/host</c> principal name.</param>
    /// <param name="clientNonce">The client nonce.</param>
    /// <returns>
    /// The response; <see langword="null" /> when the challenge has no <c>nonce</c>, no
    /// <c>qop</c> offering <c>auth</c>, or an <c>algorithm</c> other than <c>md5-sess</c>
    /// exactly, which curl cancels and fails with exit 67.
    /// </returns>
    /// <remarks>
    /// The challenge is read as <c>key=value</c> directives separated by a comma, blanks
    /// skipped only before a key (ADR-1347). Each value is the first under exactly that key
    /// before any text that is not such a directive; a quoted one runs to its closing quote,
    /// a backslash taking the character after it, and an unquoted one to a comma. A value
    /// that does not fit curl's buffer counts as absent: <c>nonce</c> over 63 characters,
    /// <c>realm</c> over 127, <c>algorithm</c> and <c>qop</c> over 63. A missing
    /// <c>realm</c> is sent empty. The user name and realm are hashed as they are and sent
    /// with each double quote and backslash escaped (upstream case 907).
    /// </remarks>
    internal static byte[]? AnswerAsCurl(byte[] challenge, Encoding credentialEncoding, string user, string password, string digestUri, string clientNonce)
    {
        string text = ChallengeText(challenge);
        string? nonce = CurlValue(text, "nonce", 64);
        string realm = CurlValue(text, "realm", 128) ?? string.Empty;
        string? algorithm = CurlValue(text, "algorithm", 64);
        string? qopOptions = CurlValue(text, "qop", 64);
        if (nonce is null || algorithm != "md5-sess" || !OffersAuth(qopOptions))
        {
            return null;
        }

        string response = ResponseHex(credentialEncoding, user, realm, password, nonce, clientNonce, digestUri);
        return Concatenate(
            credentialEncoding,
            "username=\"",
            Quoted(user),
            $"\",realm=\"{Quoted(realm)}\",nonce=\"{nonce}\",cnonce=\"{clientNonce}\",nc=\"{NonceCount}\",digest-uri=\"{digestUri}\",response={response},qop=auth");
    }

    /// <summary>
    /// Answers as Windows SSPI does for curl's Schannel build.
    /// </summary>
    /// <param name="challenge">The decoded challenge.</param>
    /// <param name="credentialEncoding">
    /// The encoding the user name and password are sent in when the challenge does not
    /// give <c>charset</c> as <c>utf-8</c>; with it, they are sent in UTF-8.
    /// </param>
    /// <param name="userWithDomain">
    /// The user name as given to curl. Up to the first backslash, or else the first slash, it
    /// is the domain, sent as the realm; the challenge's own realm is not used.
    /// </param>
    /// <param name="password">The password.</param>
    /// <param name="digestUri">The <c>service/host</c> principal name.</param>
    /// <param name="clientNonce">The client nonce.</param>
    /// <returns>
    /// The response, which ends <c>,charset=utf-8</c> when the challenge gives that charset;
    /// <see langword="null" /> when the challenge is not a list of <c>key=value</c>
    /// directives, repeats <c>nonce</c>, <c>algorithm</c> or <c>charset</c>, has no
    /// <c>nonce</c> or an empty one, an <c>algorithm</c> other than <c>md5-sess</c> in any
    /// case, or a <c>qop</c> without <c>auth</c>. SSPI fails those with exit 94, not 67,
    /// which <see cref="SaslAuthenticator" /> throws as a
    /// <see cref="Curl.Protocol.Abstractions.SaslAuthenticationFailedException" /> (BL-781).
    /// </returns>
    internal static byte[]? AnswerAsSspi(byte[] challenge, Encoding credentialEncoding, string userWithDomain, string password, string digestUri, string clientNonce)
    {
        if (ReadAsSspi(ChallengeText(challenge)) is not var (nonce, isUtf8))
        {
            return null;
        }

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

    // The challenge's nonce and whether it gives charset utf-8, or null when SSPI rejects it.
    // Keys are read in any case; the last qop counts.
    private static (string Nonce, bool IsUtf8)? ReadAsSspi(string text)
    {
        if (SspiDirectives(text) is not { } directives || SspiSingleKeys.Any(key => directives[key].Count() > 1))
        {
            return null;
        }

        string nonce = directives["nonce"].SingleOrDefault(string.Empty);
        bool isMd5Session = string.Equals(directives["algorithm"].SingleOrDefault(), "md5-sess", StringComparison.OrdinalIgnoreCase);
        bool isUtf8 = string.Equals(directives["charset"].SingleOrDefault(), "utf-8", StringComparison.OrdinalIgnoreCase);
        return nonce.Length > 0 && isMd5Session && SspiOffersAuth(directives["qop"].LastOrDefault()) ? (nonce, isUtf8) : null;
    }

    // Whether SSPI reads the qop list as offering auth: no list or an empty one does, and
    // blanks around its tokens do not count.
    private static bool SspiOffersAuth(string? qopOptions) =>
        string.IsNullOrEmpty(qopOptions) || OffersAuth(qopOptions.Replace(" ", string.Empty).Replace("\t", string.Empty));

    // The challenge's directive values by key, or null when any of the text is not a
    // directive: each is key=value, blanks allowed around both, the value quoted or running
    // to a comma; a comma separates them, further commas allowed, none before the first or
    // after the last.
    private static ILookup<string, string>? SspiDirectives(string text)
    {
        IReadOnlyList<Match> directives = SspiDirective.Matches(text);
        return directives.Sum(directive => directive.Length) == text.Length
            ? directives.ToLookup(
                directive => directive.Groups["key"].Value.Trim(' ', '\t'),
                directive => DirectiveValue(directive).TrimEnd(' ', '\t'),
                StringComparer.OrdinalIgnoreCase)
            : null;
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

    // curl's auth_digest_get_key_value: the value of the first directive under exactly key;
    // null when there is none before the first text that is not a directive, or when the
    // value has capacity characters or more.
    private static string? CurlValue(string text, string key, int capacity)
    {
        Match? found = CurlDirective.Matches(text).FirstOrDefault(directive => directive.Groups["key"].Value == key);
        return found is not null && DirectiveValue(found) is var value && value.Length < capacity ? value : null;
    }

    // A directive's value: a quoted one with each backslash dropped and the character after
    // it taken as it is, an unquoted one as it stands.
    private static string DirectiveValue(Match directive) =>
        directive.Groups["quoted"] is { Success: true } quoted
            ? QuotedPair.Replace(quoted.Value, "$1")
            : directive.Groups["plain"].Value;

    // A value as curl's own build writes it between quotes: each double quote and backslash
    // escaped with a backslash.
    private static string Quoted(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

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
