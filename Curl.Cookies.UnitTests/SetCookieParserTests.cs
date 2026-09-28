using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Pins <see cref="SetCookieParser"/> to curl 8.21.0 (<c>x86_64-w64-mingw32</c>, Schannel, libpsl), measured on
/// 2026-09-26. Each case was served by <c>Record-CurlExchange.ps1</c> as
/// <c>HTTP/1.1 200 OK\r\nSet-Cookie: &lt;header&gt;\r\nContent-Length: 0\r\n\r\n</c> and fetched with
/// <c>curl -s --resolve &lt;host&gt;:&lt;port&gt;:127.0.0.1 -c jar.txt http://&lt;host&gt;:&lt;port&gt;&lt;path&gt;</c>;
/// the expected jar line is the line curl wrote, its fields joined by <c>|</c>, and <see langword="null"/> means
/// curl wrote no cookie. Expiry times are pinned against the <c>now</c> of the run, 1790458978.
/// </summary>
[TestClass]
public sealed class SetCookieParserTests
{
    private const long Now = 1_790_458_978;

    /// <summary>curl's 400-day cap for <see cref="Now"/>: <c>(Now + 34560000 + 30) / 60 * 60</c>.</summary>
    private const long CappedExpiry = 1_825_018_980;

    [TestMethod]
    [DataRow("http://example.com/a/b/c?q=1", "a=1", "example.com|FALSE|/a/b|FALSE|0|a|1")]
    [DataRow("http://localhost/x/", "a6=1", "localhost|FALSE|/x|FALSE|0|a6|1")]
    [DataRow("http://localhost/x", "a7=1", "localhost|FALSE|/|FALSE|0|a7|1")]
    [DataRow("http://localhost//", "a8=1", "localhost|FALSE|/|FALSE|0|a8|1")]
    [DataRow("http://localhost/a/b?c/d", "t6=v", "localhost|FALSE|/a|FALSE|0|t6|v")]
    [DataRow("http://example.com/a/b/c", "b=2; Path=/x", "example.com|FALSE|/x|FALSE|0|b|2")]
    [DataRow("http://example.com/a/b/c", "c=3; Path=x", "example.com|FALSE|/|FALSE|0|c|3")]
    [DataRow("http://example.com/a/b/c", "d=4; Path=\"/q/\"", "example.com|FALSE|/q|FALSE|0|d|4")]
    [DataRow("http://example.com/a/b/c", "e=5; Path=/q/", "example.com|FALSE|/q|FALSE|0|e|5")]
    [DataRow("http://example.com/a/b/c", "f=6; Path=", "example.com|FALSE|/a/b|FALSE|0|f|6")]
    [DataRow("http://localhost/", "n27=v; Path=/a; Path=/b", "localhost|FALSE|/b|FALSE|0|n27|v")]
    [DataRow("http://localhost/", "t3=v; Path=/x//", "localhost|FALSE|/x/|FALSE|0|t3|v")]
    [DataRow("http://localhost/", "s13=v; Path=\t/x", "localhost|FALSE|/x|FALSE|0|s13|v")]
    [DataRow("http://localhost/", "s14=v; Path=/x/y/", "localhost|FALSE|/x/y|FALSE|0|s14|v")]
    [DataRow("http://localhost/", "s15=v; Path=\"/x", "localhost|FALSE|/x|FALSE|0|s15|v")]
    [DataRow("http://localhost/", "s16=v; Path=\"", "localhost|FALSE|/|FALSE|0|s16|v")]
    [DataRow("http://localhost/", "s17=v; Path=/x\"", "localhost|FALSE|/x\"|FALSE|0|s17|v")]
    [DataRow("http://localhost/", "s18=v; Path=\"/x\"y\"", "localhost|FALSE|/x\"y|FALSE|0|s18|v")]
    [DataRow("http://localhost/", "t4=v; Path\t=/x", "localhost|FALSE|/|FALSE|0|t4|v")]
    [DataRow("http://localhost/", "s6=v; Path=/a\u0001b", null)]
    [DataRow("http://localhost/a/b/", "s11=v; Path=/a\tb", null)]
    public void Parse_Path_MatchesCurl(string url, string header, string? expectedJarLine) =>
        AssertParsesAsCurlDid(url, header, expectedJarLine);

    [TestMethod]
    [DataRow("http://example.com/", "g=7; Domain=example.com", ".example.com|TRUE|/|FALSE|0|g|7")]
    [DataRow("http://example.com/", "h=8; Domain=.example.com", ".example.com|TRUE|/|FALSE|0|h|8")]
    [DataRow("http://example.com/", "i=9; Domain=other.com", null)]
    [DataRow("http://www.example.com/", "j=10; Domain=example.com", ".example.com|TRUE|/|FALSE|0|j|10")]
    [DataRow("http://www.example.com/", "m12=v; Domain=ample.com", null)]
    [DataRow("http://example.com/", "m13=v; Domain=www.example.com", null)]
    [DataRow("http://localhost/", "a2=1; Domain=localhost", ".localhost|TRUE|/|FALSE|0|a2|1")]
    [DataRow("http://localhost/", "a3=1; Domain=.localhost", ".localhost|TRUE|/|FALSE|0|a3|1")]
    [DataRow("http://localhost/", "a4=1; Domain=", "localhost|FALSE|/|FALSE|0|a4|1")]
    [DataRow("http://localhost/", "a5=1; Domain=LOCALHOST", ".LOCALHOST|TRUE|/|FALSE|0|a5|1")]
    [DataRow("http://localhost/", "m7=v; Domain=\"localhost\"", null)]
    [DataRow("http://localhost/", "m8=v; Domain=localhost.", null)]
    [DataRow("http://localhost/", "m22=v; Domain=local", null)]
    [DataRow("http://localhost/", "w6=v; Domain=.", null)]
    [DataRow("http://localhost/", "w11=v; Domain=..localhost", null)]
    [DataRow("http://localhost/", "n28=v; Domain=localhost; Domain=other.com", null)]
    [DataRow("http://localhost/", "s7=v; Domain=local\u0001host", null)]
    [DataRow("http://127.0.0.1/", "m9=v; Domain=127.0.0.1", "127.0.0.1|FALSE|/|FALSE|0|m9|v")]
    [DataRow("http://127.0.0.1/", "m10=v; Domain=.0.0.1", null)]
    public void Parse_Domain_MatchesCurl(string url, string header, string? expectedJarLine) =>
        AssertParsesAsCurlDid(url, header, expectedJarLine);

    [TestMethod]
    [DataRow("http://example.com/", "k=11; Secure", null)]
    [DataRow("http://foo.localhost/", "s2=v; Secure", null)]
    [DataRow("http://localhost/", "k=11; Secure", "localhost|FALSE|/|TRUE|0|k|11")]
    [DataRow("http://127.0.0.1/", "s1=v; Secure", "127.0.0.1|FALSE|/|TRUE|0|s1|v")]
    [DataRow("http://localhost/", "k2=11; secure=yes", "localhost|FALSE|/|FALSE|0|k2|11")]
    [DataRow("http://localhost/", "n9=v;Secure=", "localhost|FALSE|/|FALSE|0|n9|v")]
    [DataRow("http://localhost/", "t10=v; Secure=\t", "localhost|FALSE|/|FALSE|0|t10|v")]
    [DataRow("http://localhost/", "n10=v; Secure; Secure", "localhost|FALSE|/|TRUE|0|n10|v")]
    [DataRow("http://localhost/", "t2=v; Secure\tx", "localhost|FALSE|/|TRUE|0|t2|v")]
    [DataRow("http://localhost/", "u3=v;Secure", "localhost|FALSE|/|TRUE|0|u3|v")]
    [DataRow("http://localhost/", "u4=v;  ;Secure", "localhost|FALSE|/|TRUE|0|u4|v")]
    [DataRow("http://localhost/", "u5=v; =x; Secure", "localhost|FALSE|/|TRUE|0|u5|v")]
    [DataRow("http://localhost/", "u1=v;\tSecure", "localhost|FALSE|/|FALSE|0|u1|v")]
    [DataRow("http://localhost/", "u2=v; \tSecure", "localhost|FALSE|/|FALSE|0|u2|v")]
    [DataRow("http://localhost/", "u6=v; \t; Secure", "localhost|FALSE|/|FALSE|0|u6|v")]
    [DataRow("http://example.com/", "l=12; HttpOnly", "#HttpOnly_example.com|FALSE|/|FALSE|0|l|12")]
    [DataRow("http://localhost/", "k3=11; HTTPONLY=1", "localhost|FALSE|/|FALSE|0|k3|11")]
    [DataRow("http://localhost/", "w7=v; HttpOnly=", "localhost|FALSE|/|FALSE|0|w7|v")]
    [DataRow("http://localhost/", "m4=v; SeCuRe; hTTpOnLy", "#HttpOnly_localhost|FALSE|/|TRUE|0|m4|v")]
    [DataRow("http://localhost/", "n11=v;;; HttpOnly", "#HttpOnly_localhost|FALSE|/|FALSE|0|n11|v")]
    [DataRow("http://localhost/", "t7=v; HttpOnly\t", "#HttpOnly_localhost|FALSE|/|FALSE|0|t7|v")]
    [DataRow("http://localhost/", "n26=v; Version=1; Comment=x; Foo", "localhost|FALSE|/|FALSE|0|n26|v")]
    [DataRow("http://localhost/", "t1=v; Fo\to=x", "localhost|FALSE|/|FALSE|0|t1|v")]
    public void Parse_SecureHttpOnlyAndOtherAttributes_MatchCurl(string url, string header, string? expectedJarLine) =>
        AssertParsesAsCurlDid(url, header, expectedJarLine);

    [TestMethod]
    [DataRow("https://example.com/", "k=11; Secure", "example.com|FALSE|/|TRUE|0|k|11")]
    [DataRow("wss://example.com/", "k=11; Secure", "example.com|FALSE|/|TRUE|0|k|11")]
    [DataRow("http://[::1]/", "k=11; Secure", "::1|FALSE|/|TRUE|0|k|11")]
    public void Parse_SecureFromTheOtherSecureOrigins_IsKept(string url, string header, string? expectedJarLine) =>
        AssertParsesAsCurlDid(url, header, expectedJarLine);

    [TestMethod]
    [DataRow("http://example.com/", "__Secure-v=22", null)]
    [DataRow("http://localhost/", "__Secure-v=22", null)]
    [DataRow("http://localhost/", "__Secure-w=23; Secure", "localhost|FALSE|/|TRUE|0|__Secure-w|23")]
    [DataRow("http://localhost/", "__Secure-=27; Secure", "localhost|FALSE|/|TRUE|0|__Secure-|27")]
    [DataRow("http://localhost/", "__Secure-ab", null)]
    [DataRow("http://example.com/", "__secure-ab=28", "example.com|FALSE|/|FALSE|0|__secure-ab|28")]
    [DataRow("http://localhost/", "__Host-x=24; Secure", "localhost|FALSE|/|TRUE|0|__Host-x|24")]
    [DataRow("http://localhost/", "__Host-=27; Secure", "localhost|FALSE|/|TRUE|0|__Host-|27")]
    [DataRow("http://localhost/a/b", "__Host-x2=24; Secure", null)]
    [DataRow("http://localhost/", "__Host-y=25; Secure; Path=/p", null)]
    [DataRow("http://localhost/", "__Host-y2=25; Secure; Path=/", "localhost|FALSE|/|TRUE|0|__Host-y2|25")]
    [DataRow("http://localhost/", "__Host-w12=v; Secure; Path=/x; Path=/", "localhost|FALSE|/|TRUE|0|__Host-w12|v")]
    [DataRow("http://localhost/a/", "__Host-w13=v; Secure; Path=", null)]
    [DataRow("http://localhost/", "__Host-z=26; Secure; Domain=localhost", null)]
    [DataRow("http://example.com/", "__Host-x=24; Secure", null)]
    [DataRow("http://localhost/", "__host-aa=27; Secure; Path=/p", "localhost|FALSE|/p|TRUE|0|__host-aa|27")]
    public void Parse_SecureAndHostPrefixes_MatchCurl(string url, string header, string? expectedJarLine) =>
        AssertParsesAsCurlDid(url, header, expectedJarLine);

    [TestMethod]
    [DataRow("=v1", null)]
    [DataRow("   =v", null)]
    [DataRow("n2=", "localhost|FALSE|/|FALSE|0|n2|")]
    [DataRow("n3", null)]
    [DataRow("  n4  =  v 4  ; Path=/", "localhost|FALSE|/|FALSE|0|n4|v 4")]
    [DataRow("n5=\"quoted\"", "localhost|FALSE|/|FALSE|0|n5|\"quoted\"")]
    [DataRow("n6=a=b=c", "localhost|FALSE|/|FALSE|0|n6|a=b=c")]
    [DataRow("m18 =v", "localhost|FALSE|/|FALSE|0|m18|v")]
    [DataRow("m20 x=v", "localhost|FALSE|/|FALSE|0|m20 x|v")]
    [DataRow("m21=v x", "localhost|FALSE|/|FALSE|0|m21|v x")]
    [DataRow("$n29=v", "localhost|FALSE|/|FALSE|0|$n29|v")]
    [DataRow("s5=vé", "localhost|FALSE|/|FALSE|0|s5|vé")]
    [DataRow("m6=v\t", "localhost|FALSE|/|FALSE|0|m6|v")]
    [DataRow("n7=a\tb", null)]
    [DataRow("n\t8=v", null)]
    [DataRow("t8\t=v", null)]
    [DataRow("\tt9=v", null)]
    [DataRow("s12=v; Foo=a\tb", null)]
    [DataRow("s3\u0001=v", null)]
    [DataRow("n30=v\u0001x", null)]
    [DataRow("n31=v\u007F", null)]
    [DataRow("s9=v; Foo=\u0001", null)]
    public void Parse_NameAndValue_MatchCurl(string header, string? expectedJarLine) =>
        AssertParsesAsCurlDid("http://localhost/", header, expectedJarLine);

    [TestMethod]
    [DataRow("m=13; Max-Age=100", Now + 100)]
    [DataRow("n16=v; Max-Age=\"100\"", Now + 100)]
    [DataRow("n17=v; Max-Age=100abc", Now + 100)]
    [DataRow("m3=v; Max-Age= 100", Now + 100)]
    [DataRow("s10=v; Max-Age=100\t", Now + 100)]
    [DataRow("n20=v; max-age=100", Now + 100)]
    [DataRow("w3=v; Max-Age=100; Max-Age=200", Now + 200)]
    [DataRow("t=20; Max-Age=1000; Expires=Wed, 09 Jun 2027 10:18:14 GMT", Now + 1000)]
    [DataRow("u=21; Expires=Wed, 09 Jun 2027 10:18:14 GMT; Max-Age=1000", Now + 1000)]
    [DataRow("n18=v; Max-Age=99999999999999999999999", CappedExpiry)]
    [DataRow("n19=v; Max-Age=", 0L)]
    [DataRow("t5=v; Max\t-Age=100", 0L)]
    public void Parse_MaxAge_CountsFromNow(string header, long expectedExpiry) =>
        Assert.AreEqual(expectedExpiry, Parse("http://localhost/", header)!.ExpiresUnixSeconds);

    /// <summary>curl wrote no cookie for these: it kept an already expired one, which a store deletes.</summary>
    [TestMethod]
    [DataRow("p=16; Max-Age=0")]
    [DataRow("q=17; Max-Age=-1")]
    [DataRow("r=18; Max-Age=abc")]
    [DataRow("m1=v; Max-Age=\"")]
    [DataRow("m2=v; Max-Age=+100")]
    [DataRow("w10=v; Max-Age=200; Max-Age=bad")]
    [DataRow("w14=v; Max-Age=100; Max-Age=0")]
    [DataRow("m16=v; Max-Age=0; Expires=Wed, 09 Jun 2027 10:18:14 GMT")]
    [DataRow("m17=v; Expires=Wed, 09 Jun 2027 10:18:14 GMT; Max-Age=0")]
    [DataRow("n13=v; Expires=Wed, 09 Jun 1960 10:18:14 GMT")]
    [DataRow("n14=v; Expires=Thu, 01 Jan 1970 00:00:00 GMT")]
    [DataRow("n15=v; Expires=Thu, 01 Jan 1970 00:00:01 GMT")]
    [DataRow("w4=v; Expires=Wed, 09 Jun 1583 10:18:14 GMT")]
    public void Parse_AlreadyExpired_ExpiresAtOne(string header) =>
        Assert.AreEqual(1L, Parse("http://localhost/", header)!.ExpiresUnixSeconds);

    [TestMethod]
    [DataRow("o=15; Expires=Wed, 09 Jun 2027 10:18:14 GMT", 1_812_536_294L)]
    [DataRow("n21=v; Expires=Wed, 09 Jun 2027 10:18:14 +0200", 1_812_529_094L)]
    [DataRow("n22=v; Expires=Wed, 09 Jun 2027 10:18:14 EST", 1_812_554_294L)]
    [DataRow("n12=v; Expires=Wed, 09 Jun 2027 10:18:14 GMT; Expires=Thu, 10 Jun 2027 10:18:14 GMT", 1_812_536_294L)]
    [DataRow("w8=v; Expires=; Expires=Wed, 09 Jun 2027 10:18:14 GMT", 1_812_536_294L)]
    [DataRow("w9=v; Expires=bad; Expires=Wed, 09 Jun 2027 10:18:14 GMT", 1_812_536_294L)]
    [DataRow("m15=v; Expires=Wed, 09 Jun 2027 10:18:14 GMT xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 1_812_536_294L)]
    [DataRow("w1=v; Expires=Wed, 09 Jun 2027 10:18:14 GMT xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 1_812_536_294L)]
    [DataRow("w2=v; Expires=Wed, 09 Jun 2027 10:18:14 GMT xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 0L)]
    [DataRow("s=19; Expires=garbage", 0L)]
    [DataRow("n32=v; Expires=Wed, 09 Jun 2027 10:18:14 gmt", 0L)]
    [DataRow("n24=v; Expires=Wed, 09 Jun 1500 10:18:14 GMT", 0L)]
    [DataRow("w5=v; Expires=Wed, 09 Jun 1582 10:18:14 GMT", 0L)]
    [DataRow("n=14; Expires=Wed, 09 Jun 2100 10:18:14 GMT", CappedExpiry)]
    [DataRow("n25=v; Expires=Wed, 09 Jun 10000 10:18:14 GMT", CappedExpiry)]
    public void Parse_Expires_MatchesCurl(string header, long expectedExpiry) =>
        Assert.AreEqual(expectedExpiry, Parse("http://localhost/", header)!.ExpiresUnixSeconds);

    /// <summary>curl wrote no cookie for this past date: it kept one already expired at 1994-11-06T08:49:37Z.</summary>
    [TestMethod]
    public void Parse_PastExpires_KeepsThePastInstant() =>
        Assert.AreEqual(784_111_777L, Parse("http://localhost/", "n23=v; Expires=Sunday, 06-Nov-94 08:49:37 GMT")!.ExpiresUnixSeconds);

    [TestMethod]
    public void Parse_ExpiryOneSecondPastTheCap_IsCapped() =>
        Assert.AreEqual(CappedExpiry, Parse("http://localhost/", "c=v; Max-Age=34560001")!.ExpiresUnixSeconds);

    [TestMethod]
    public void Parse_ExpiryAtTheCap_IsKept() =>
        Assert.AreEqual(Now + 34_560_000, Parse("http://localhost/", "c=v; Max-Age=34560000")!.ExpiresUnixSeconds);

    [TestMethod]
    public void Parse_MaxAgeThatOverflowsWhenAddedToNow_IsCapped() =>
        Assert.AreEqual(CappedExpiry, Parse("http://localhost/", "c=v; Max-Age=" + long.MaxValue)!.ExpiresUnixSeconds);

    [TestMethod]
    public void Parse_SpacesAroundAttributeNamesAndValues_AreTrimmed() =>
        Assert.AreEqual("localhost|FALSE|/p|FALSE|1790459078|m19|v", JarLine(Parse("http://localhost/", "m19= v; Path = /p ; Max-Age = 100")!));

    /// <summary>
    /// Measured with <c>Set-Cookie:x=v; Path=/&lt;p × n&gt;</c> (no space after the colon): a header value of
    /// 4998 characters is kept and one of 4999 dropped; with the usual space after the colon the same limit
    /// holds with the space counted.
    /// </summary>
    [TestMethod]
    [DataRow("", 4987, true)]
    [DataRow("", 4988, false)]
    [DataRow(" ", 4986, true)]
    [DataRow(" ", 4987, false)]
    [DataRow("    ", 4984, false)]
    public void Parse_HeaderLength_IsLimitedTo4998(string leadingSpace, int pathLength, bool kept)
    {
        string header = leadingSpace + "x=v; Path=/" + new string('p', pathLength);

        Cookie? cookie = SetCookieParser.Parse(header, CurlUrl.Parse("http://localhost/"), DateTimeOffset.FromUnixTimeSeconds(Now));

        Assert.AreEqual(kept, cookie is not null);
    }

    /// <summary>Measured: the name and value together may hold 4096 characters, no more; an attribute is not limited.</summary>
    [TestMethod]
    [DataRow(4096, 0, true)]
    [DataRow(4097, 0, false)]
    [DataRow(4090, 6, true)]
    [DataRow(4090, 5, true)]
    [DataRow(1, 4095, true)]
    [DataRow(1, 4096, false)]
    [DataRow(2100, 2100, false)]
    public void Parse_NameAndValueLength_IsLimitedTo4096(int nameLength, int valueLength, bool kept)
    {
        string header = new string('a', nameLength) + "=" + new string('b', valueLength);

        Assert.AreEqual(kept, Parse("http://localhost/", header) is not null);
    }

    [TestMethod]
    public void Parse_LongPath_IsKept() =>
        Assert.AreEqual(4201, Parse("http://localhost/", "x=v; Path=/" + new string('p', 4200))!.Path.Length);

    [TestMethod]
    public void Parse_Session_IsSessionCookie()
    {
        Cookie cookie = Parse("http://localhost/", "a=1")!;

        Assert.IsTrue(cookie.IsSessionCookie);
        Assert.IsFalse(Parse("http://localhost/", "a=1; Max-Age=5")!.IsSessionCookie);
    }

    [TestMethod]
    public void Parse_NullArguments_Throw()
    {
        CurlUrl uri = CurlUrl.Parse("http://localhost/");

        Assert.ThrowsExactly<ArgumentNullException>(() => SetCookieParser.Parse(null!, uri, DateTimeOffset.UnixEpoch));
        Assert.ThrowsExactly<ArgumentNullException>(() => SetCookieParser.Parse("a=1", null!, DateTimeOffset.UnixEpoch));
        Assert.ThrowsExactly<ArgumentNullException>(() => SetCookieParser.ParseFromCookieFile(null!, DateTimeOffset.UnixEpoch));
        Assert.ThrowsExactly<ArgumentNullException>(() => SetCookieParser.Parse(null!, uri, DateTimeOffset.UnixEpoch, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => SetCookieParser.Parse("a=1", null!, DateTimeOffset.UnixEpoch, out _));
    }

    /// <summary>
    /// Measured 2026-09-27 (BL-443): <c>curl -v -b file -c - http://127.0.0.1:&lt;port&gt;/</c> with a file of
    /// <c>Set-Cookie: f=v; Pa&lt;TAB&gt;th=/; X=&lt;0x01&gt;</c> and <c>Set-Cookie: g=v; X=&lt;0x01&gt;</c> sent
    /// <c>Cookie: f=v</c>: a control character after a part that a tab ended is never checked.
    /// </summary>
    [TestMethod]
    public void ParseFromCookieFile_ControlCharacterAfterATabEndedPart_IsNeverChecked()
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(Now);

        Cookie? read = SetCookieParser.ParseFromCookieFile("f=v; Pa\tth=/; X=\u0001", now);
        Cookie? refused = SetCookieParser.ParseFromCookieFile("g=v; X=\u0001", now);

        Assert.AreEqual("f", read?.Name);
        Assert.IsNull(refused);
    }

    private static void AssertParsesAsCurlDid(string url, string header, string? expectedJarLine)
    {
        Cookie? cookie = Parse(url, header);

        Assert.AreEqual(expectedJarLine, cookie is null ? null : JarLine(cookie));
    }

    /// <summary>Parses the header as curl received it in the measurement, after <c>Set-Cookie:</c> and one space.</summary>
    private static Cookie? Parse(string url, string header) =>
        SetCookieParser.Parse(" " + header, CurlUrl.Parse(url), DateTimeOffset.FromUnixTimeSeconds(Now));

    /// <summary>The fields curl's jar line carries, in its order, joined by <c>|</c> instead of tabs.</summary>
    private static string JarLine(Cookie cookie)
    {
        string domain = (cookie.IsHttpOnly ? "#HttpOnly_" : string.Empty) + (cookie.IncludesSubdomains ? "." : string.Empty) + cookie.Domain;
        return string.Join('|', domain, Flag(cookie.IncludesSubdomains), cookie.Path, Flag(cookie.IsSecure), cookie.ExpiresUnixSeconds, cookie.Name, cookie.Value);
    }

    private static string Flag(bool value) => value ? "TRUE" : "FALSE";
}
