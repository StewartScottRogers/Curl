using System.Security.Cryptography;
using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="SaslDigestMd5" /> to the DIGEST-MD5 responses curl sent to
/// <c>Record-CurlExchange.ps1 -Smtp</c> on 2026-09-28 (ADR-0139, BL-537 Notes): curl 8.21.0
/// (mingw, Schannel, SSPI) on Windows and curl 8.18.0 (OpenSSL) under WSL. Each test feeds
/// back the client nonce curl chose, so the response hash must match curl's.
/// </summary>
[TestClass]
public sealed class SaslDigestMd5Tests
{
    private const string Challenge = "realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess,charset=utf-8";

    private const string ChallengeWithoutCharset = "realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess";

    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    [TestMethod]
    [DataRow(ChallengeWithoutCharset, "33c9355ee13cd9ce118ac5332dfff45e",
        "username=\"user\",realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",cnonce=\"33c9355ee13cd9ce118ac5332dfff45e\",nc=\"00000001\",digest-uri=\"smtp/172.26.96.1\",response=3bd95e0256eb7fec7923e829f85c9339,qop=auth",
        DisplayName = "No charset")]
    [DataRow("nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess", "bec776c211ac7e5322fccc444cf441d0",
        "username=\"user\",realm=\"\",nonce=\"OA6MG9tEQGm2hh\",cnonce=\"bec776c211ac7e5322fccc444cf441d0\",nc=\"00000001\",digest-uri=\"smtp/172.26.96.1\",response=8809745c4edc2b44067e53e4eb50f8e7,qop=auth",
        DisplayName = "No realm: sent empty")]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth-int,AUTH\",algorithm=md5-sess,charset=utf-8", "c28c7f2930b42faad761e33ad9538bf9",
        "username=\"user\",realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",cnonce=\"c28c7f2930b42faad761e33ad9538bf9\",nc=\"00000001\",digest-uri=\"smtp/172.26.96.1\",response=61378d18101f465e47c567ebb7530023,qop=auth",
        DisplayName = "qop=\"auth-int,AUTH\": auth")]
    public void AnswerAsCurl_MatchesCurlOpenSsl(string challenge, string clientNonce, string expected)
    {
        byte[]? answer = SaslDigestMd5.AnswerAsCurl(Encoding.ASCII.GetBytes(challenge), Encoding.UTF8, "user", "pencil", "smtp/172.26.96.1", clientNonce);

        Assert.AreEqual(expected, Encoding.Latin1.GetString(answer!));
    }

    [TestMethod]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",algorithm=md5-sess", DisplayName = "No qop: *")]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\"", DisplayName = "No algorithm: *")]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=MD5-SESS", DisplayName = "algorithm=MD5-SESS: * (compared exactly)")]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth-int\",algorithm=md5-sess", DisplayName = "qop=\"auth-int\": *")]
    [DataRow("realm=\"localhost\",qop=\"auth\",algorithm=md5-sess", DisplayName = "No nonce: *")]
    [DataRow("", DisplayName = "Empty challenge: *")]
    public void AnswerAsCurl_ChallengeCurlCancels_AnswersNull(string challenge)
    {
        Assert.IsNull(SaslDigestMd5.AnswerAsCurl(Encoding.ASCII.GetBytes(challenge), Encoding.UTF8, "user", "pencil", "smtp/h", "c"));
    }

    [TestMethod]
    public void AnswerAsCurl_ValuesCutAtCurlsBufferSizes()
    {
        string challenge = $"realm=\"{new string('r', 200)}\",nonce=\"{new string('n', 100)}\",qop=\"auth\",algorithm=md5-sess";

        string answer = Encoding.Latin1.GetString(SaslDigestMd5.AnswerAsCurl(Encoding.ASCII.GetBytes(challenge), Encoding.UTF8, "u", "p", "smtp/h", "c")!);

        StringAssert.Contains(answer, $",realm=\"{new string('r', 127)}\",nonce=\"{new string('n', 63)}\",");
    }

    [TestMethod]
    public void AnswerAsCurl_ChallengeEndsAtNul()
    {
        byte[] challenge = [.. Encoding.ASCII.GetBytes(ChallengeWithoutCharset), 0, .. Encoding.ASCII.GetBytes(",realm=\"other\"")];

        string answer = Encoding.Latin1.GetString(SaslDigestMd5.AnswerAsCurl(challenge, Encoding.UTF8, "u", "p", "smtp/h", "c")!);

        StringAssert.Contains(answer, ",realm=\"localhost\",");
    }

    [TestMethod]
    [DataRow(Challenge, "user", "81eed5b913007ab96776b8224946a866", "user", "", "1ae34deb057c4c638fc093d4913d8f29", ",charset=utf-8",
        DisplayName = "charset=utf-8: echoed")]
    [DataRow(ChallengeWithoutCharset, "user", "5f058a261c6c55ebd62177b774f89c43", "user", "", "b3618740f08190b08487ec025bb8b213", "",
        DisplayName = "No charset")]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",algorithm=md5-sess", "user", "8f6e49c3a8cb500c737efec872098058", "user", "", "90a11222fe5d89e0fca4af2856a40e89", "",
        DisplayName = "No qop: auth")]
    [DataRow("nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess", "user", "db3cf9d6f2b68025ac0e017824904b80", "user", "", "8e74f08e003f976bb9f6b39b5e606d02", "",
        DisplayName = "No realm")]
    [DataRow(ChallengeWithoutCharset, "dom\\user", "5f615aeb6ee98ad080c2b42ead270b95", "user", "dom", "fcfc5faaab992fe8eb259071613f12df", "",
        DisplayName = "dom\\user: realm dom")]
    [DataRow(ChallengeWithoutCharset, "dom/user", "12da7bd6bd22201dfc3fa69ccc446ce1", "user", "dom", "d8fc17d7cb2a49d6c3206453d2c036c8", "",
        DisplayName = "dom/user: realm dom")]
    [DataRow(ChallengeWithoutCharset, "a/b\\c", "e4327076e61189bfd8230536bc03fbf7", "c", "a/b", "ba72293116ddc2ac418a7c8194b2dfd1", "",
        DisplayName = "a/b\\c: the backslash wins")]
    [DataRow(ChallengeWithoutCharset, "a\\b/c", "37adc6aaa86347068ca9bf0ed9045058", "b/c", "a", "e4059fb3e0f799bd1695fab2346d0fc6", "",
        DisplayName = "a\\b/c: the backslash wins")]
    [DataRow(ChallengeWithoutCharset, "user@dom", "06c5c42296c59737d70133a1824cea64", "user@dom", "", "4584df8aeeae0d2a0d9f1ed16b6bebd5", "",
        DisplayName = "user@dom: not split")]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=MD5-SESS", "user", "146b738a72ad526e5cbc72c0466eb707", "user", "", "ac1ed2e68b44c968126ad5f2db7f341d", "",
        DisplayName = "algorithm=MD5-SESS")]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth-int,AUTH\",algorithm=md5-sess,charset=utf-8", "user", "09f59dfb76c9b74b73c96b1c2fbf6a8e", "user", "", "9d947628680d04e6e81a9cf1594634b0", ",charset=utf-8",
        DisplayName = "qop=\"auth-int,AUTH\": auth")]
    public void AnswerAsSspi_MatchesCurlSchannel(
        string challenge, string userWithDomain, string clientNonce, string user, string realm, string response, string charset)
    {
        byte[]? answer = SaslDigestMd5.AnswerAsSspi(Encoding.ASCII.GetBytes(challenge), Windows1252, userWithDomain, "pencil", "smtp/127.0.0.1", clientNonce);

        Assert.AreEqual(
            $"username=\"{user}\",realm=\"{realm}\",nonce=\"OA6MG9tEQGm2hh\",digest-uri=\"smtp/127.0.0.1\",cnonce=\"{clientNonce}\",nc=00000001,response={response},qop=auth{charset}",
            Encoding.Latin1.GetString(answer!));
    }

    [TestMethod]
    [DataRow(Challenge, "0957e29eb260bffe33a68a21b3dec2df", new byte[] { 0x75, 0x73, 0xC3, 0xA9, 0x72 }, "0f8668bb3b92e4a0df3c4c54ab8174c2",
        DisplayName = "charset=utf-8: UTF-8")]
    [DataRow(ChallengeWithoutCharset, "5d3e2ddb1a8f9cc2ac3261cc4af87934", new byte[] { 0x75, 0x73, 0xE9, 0x72 }, "3a831ace4875f9afe0983ef7fc908e42",
        DisplayName = "No charset: the credential encoding")]
    public void AnswerAsSspi_NonAsciiUser_EncodedAsCurlSchannel(string challenge, string clientNonce, byte[] userBytes, string response)
    {
        byte[] answer = SaslDigestMd5.AnswerAsSspi(Encoding.ASCII.GetBytes(challenge), Windows1252, "usér", "pencil", "smtp/127.0.0.1", clientNonce)!;

        CollectionAssert.AreEqual(userBytes, answer[10..(10 + userBytes.Length)]);
        StringAssert.Contains(Encoding.Latin1.GetString(answer), $",response={response},");
    }

    [TestMethod]
    public void AnswerAsSspi_UserOutsideLatin1UnderUtf8_HashesTheUtf8Bytes()
    {
        // RFC 2831 section 2.1.2.1, not measured: the reference build receives its arguments in
        // the ANSI code page, so it cannot be given a name outside ISO 8859-1.
        byte[] challenge = Encoding.ASCII.GetBytes(Challenge);
        string latin1Answer = Encoding.Latin1.GetString(SaslDigestMd5.AnswerAsSspi(challenge, Windows1252, "é", "p", "smtp/h", "c")!);
        string outsideAnswer = Encoding.Latin1.GetString(SaslDigestMd5.AnswerAsSspi(challenge, Windows1252, "€", "p", "smtp/h", "c")!);

        Assert.AreEqual(
            "username=\"â\u0082¬\",realm=\"\",nonce=\"OA6MG9tEQGm2hh\",digest-uri=\"smtp/h\",cnonce=\"c\",nc=00000001,response=" + ExpectedUtf8Response() + ",qop=auth,charset=utf-8",
            outsideAnswer);
        Assert.AreNotEqual(latin1Answer, outsideAnswer);
    }

    [TestMethod]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\"", DisplayName = "No algorithm: exit 94")]
    [DataRow("realm=\"localhost\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth-int\",algorithm=md5-sess", DisplayName = "qop=\"auth-int\": exit 94")]
    [DataRow("realm=\"localhost\",qop=\"auth\",algorithm=md5-sess", DisplayName = "No nonce: exit 94")]
    public void AnswerAsSspi_ChallengeSspiRejects_AnswersNull(string challenge)
    {
        Assert.IsNull(SaslDigestMd5.AnswerAsSspi(Encoding.ASCII.GetBytes(challenge), Windows1252, "user", "pencil", "smtp/h", "c"));
    }

    // RFC 2831's response for user U+20AC, empty realm, password p, cnonce c, smtp/h, with A1 in UTF-8.
    private static string ExpectedUtf8Response()
    {
        byte[] userRealmPassword = MD5.HashData("€::p"u8.ToArray());
        string ha1 = Convert.ToHexStringLower(MD5.HashData([.. userRealmPassword, .. ":OA6MG9tEQGm2hh:c"u8]));
        string ha2 = Convert.ToHexStringLower(MD5.HashData("AUTHENTICATE:smtp/h"u8));
        return Convert.ToHexStringLower(MD5.HashData(Encoding.ASCII.GetBytes($"{ha1}:OA6MG9tEQGm2hh:00000001:c:auth:{ha2}")));
    }
}
