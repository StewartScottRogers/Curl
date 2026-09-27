namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="CurlUrl" /> to what curl 8.21.0 (the mingw build, <c>/mingw64/bin/curl</c>,
/// ADR-0018) accepts, rejects and holds for each URL (ADR-0010). Every accepted URL here
/// was measured with <c>-w</c> and its <c>%{url.*}</c> and <c>%{url_effective}</c>
/// variables, and every rejected one ended with exit 3; the commands are in BL-292.
/// </summary>
[TestClass]
public sealed class CurlUrlTests
{
    [TestMethod]
    public void TryParse_WithNullText_ThrowsArgumentNullException()
    {
        string? text = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CurlUrl.TryParse(text!, pathAsIs: false, out _));

        Assert.AreEqual("text", exception.ParamName);
    }

    [TestMethod]
    public void TryParse_OnThePublicOverload_ParsesAsCurlDoes()
    {
        bool parsed = CurlUrl.TryParse("http://example.com/a/../b", pathAsIs: false, out CurlUrl? url);

        Assert.IsTrue(parsed);
        Assert.AreEqual("/b", url!.AbsolutePath);
    }

    [TestMethod]
    public void Parse_WithAUrlCurlAccepts_ReturnsItParsed()
    {
        CurlUrl url = CurlUrl.Parse("http://example.com/a/../b");

        Assert.AreEqual("/b", url.AbsolutePath);
    }

    [TestMethod]
    public void Parse_WithPathAsIs_KeepsTheDotSegments()
    {
        CurlUrl url = CurlUrl.Parse("http://example.com/a/../b", pathAsIs: true);

        Assert.AreEqual("/a/../b", url.AbsolutePath);
    }

    [TestMethod]
    public void Parse_WithAUrlCurlRejects_ThrowsFormatException()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => CurlUrl.Parse("http://exa mple.com/"));

        Assert.AreEqual("curl rejects the URL \"http://exa mple.com/\".", exception.Message);
    }

    [TestMethod]
    public void Equals_WithTheSameTextParsedTwice_IsTrue()
    {
        Assert.AreEqual(CurlUrl.Parse("http://example.com/a"), CurlUrl.Parse("http://example.com/a"));
    }

    [TestMethod]
    public void Equals_WithTheSameTextParsedWithAndWithoutPathAsIs_IsFalse()
    {
        Assert.AreNotEqual(CurlUrl.Parse("http://example.com/a/../b"), CurlUrl.Parse("http://example.com/a/../b", pathAsIs: true));
    }

    [TestMethod]
    public void TryParse_WhenRejected_ReturnsFalseAndNull()
    {
        bool parsed = CurlUrl.TryParse("http://a b/", pathAsIs: false, out CurlUrl? url);

        Assert.IsFalse(parsed);
        Assert.IsNull(url);
    }

    // ADR-0010's case table, and the two HTTP paths BL-292 names.
    [TestMethod]
    [DataRow("file:///C:%2FWindows/win.ini", false, "/C:%2FWindows/win.ini")]
    [DataRow("file:///c|/x", false, "c|/x")]
    [DataRow("file:////server/share", false, "//server/share")]
    [DataRow("file:////server/../x", false, "//x")]
    [DataRow("file:///C:/dir\\..\\x", false, "C:/x")]
    [DataRow("file:///C:/dir\\..\\x", true, "C:/dir/../x")]
    [DataRow("file://C:", false, "C:")]
    [DataRow("file:///C:", false, "C:")]
    [DataRow("file:///Q:dir/../x", false, "/x")]
    [DataRow("http://example.com/a/../b", false, "/b")]
    [DataRow("http://example.com/a/../b", true, "/a/../b")]
    [DataRow("http://example.com/a%2Fb", false, "/a%2Fb")]
    [DataRow("http://example.com/a%2Fb", true, "/a%2Fb")]
    public void TryParse_WithAnAdr0010Case_KeepsThePathCurlKeeps(string text, bool pathAsIs, string path)
    {
        CurlUrl url = Parse(text, pathAsIs);

        Assert.AreEqual(path, url.AbsolutePath);
        Assert.AreEqual(text, url.OriginalString);
    }

    [TestMethod]
    [DataRow("file://user:pass@localhost/x")]
    [DataRow("file://ab:/x")]
    public void TryParse_WithAnAdr0010FileCaseCurlRejects_ReturnsFalse(string text)
    {
        AssertRejected(text);
    }

    // The Uri members handlers read, for each scheme a handler exists for.
    [TestMethod]
    [DataRow("http://example.com/a", "http", "example.com", "example.com", 80, true, "/a")]
    [DataRow("http://example.com:8080", "http", "example.com", "example.com", 8080, false, "/")]
    [DataRow("http://example.com:080/", "http", "example.com", "example.com", 80, true, "/")]
    [DataRow("https://example.com", "https", "example.com", "example.com", 443, true, "/")]
    [DataRow("https://ex%C3%A5mple.com:8443/", "https", "ex\u00e5mple.com", "xn--exmple-jua.com", 8443, false, "/")]
    [DataRow("dict://example.com/d", "dict", "example.com", "example.com", 2628, true, "/d")]
    [DataRow("dict://h:2629/d", "dict", "h", "h", 2629, false, "/d")]
    [DataRow("gopher://example.com", "gopher", "example.com", "example.com", 70, true, "/")]
    [DataRow("gopher://h:70/1x", "gopher", "h", "h", 70, true, "/1x")]
    [DataRow("mqtt://example.com/t", "mqtt", "example.com", "example.com", 1883, true, "/t")]
    [DataRow("mqtts://h/t", "mqtts", "h", "h", 8883, true, "/t")]
    [DataRow("telnet://example.com", "telnet", "example.com", "example.com", 23, true, "/")]
    [DataRow("telnet://h:24", "telnet", "h", "h", 24, false, "/")]
    [DataRow("tftp://example.com/f", "tftp", "example.com", "example.com", 69, true, "/f")]
    [DataRow("tftp://[::1]:69/f", "tftp", "[::1]", "::1", 69, true, "/f")]
    [DataRow("file:///C:/x", "file", "", "", -1, true, "C:/x")]
    [DataRow("file://localhost/C:/x", "file", "", "", -1, true, "C:/x")]
    [DataRow("foo://h\\x", "foo", "h", "h", -1, true, "/x")]
    [DataRow("foo://h:1/", "foo", "h", "h", 1, false, "/")]
    public void TryParse_ForEachScheme_ExposesTheMembersHandlersRead(
        string text,
        string scheme,
        string host,
        string idnHost,
        int port,
        bool isDefaultPort,
        string path)
    {
        CurlUrl url = Parse(text);

        Assert.AreEqual(scheme, url.Scheme);
        Assert.AreEqual(host, url.Host);
        Assert.AreEqual(idnHost, url.IdnHost);
        Assert.AreEqual(port, url.Port);
        Assert.AreEqual(isDefaultPort, url.IsDefaultPort);
        Assert.AreEqual(path, url.AbsolutePath);
        Assert.AreEqual(text, url.OriginalString);
    }

    [TestMethod]
    [DataRow("file://localhost/C:/nope", "C:/nope")]
    [DataRow("file://LOCALHOST/C:/nope", "C:/nope")]
    [DataRow("file://127.0.0.1/C:/nope", "C:/nope")]
    [DataRow("file:/C:/nope", "C:/nope")]
    [DataRow("file:///", "/")]
    [DataRow("file:///C:/a/%2e%2e/nope", "C:/nope")]
    [DataRow("file:///C:\\x\\y", "C:/x/y")]
    [DataRow("file://\\C:/x", "C:/x")]
    [DataRow("file:/\\C:/x", "/\\C:/x")]
    [DataRow("file:/C:\\x", "C:\\x")]
    [DataRow("file://\\\\C:/x", "//C:/x")]
    [DataRow("file://\\server\\share", "/server/share")]
    public void TryParse_WithAFileUrl_KeepsThePathCurlKeepsOnWindows(string text, string path)
    {
        CurlUrl url = Parse(text);

        Assert.AreEqual(path, url.AbsolutePath);
        Assert.AreEqual(string.Empty, url.Host);
        Assert.IsNull(url.User);
    }

    [TestMethod]
    [DataRow("file://")]
    [DataRow("file://localhost")]
    [DataRow("file://server/share/x")]
    [DataRow("file:C:/nope")]
    public void TryParse_WithAFileAuthorityCurlRejects_ReturnsFalse(string text)
    {
        AssertRejected(text);
    }

    [TestMethod]
    [DataRow("file:///C:/nope?q#f", "C:/nope", "q", "f")]
    [DataRow("file:///C:/x#f?q", "C:/x", null, "f?q")]
    public void TryParse_WithAFileQueryOrFragment_SplitsThemFromThePath(
        string text,
        string path,
        string? query,
        string? fragment)
    {
        CurlUrl url = Parse(text);

        Assert.AreEqual(path, url.AbsolutePath);
        Assert.AreEqual(query, url.Query);
        Assert.AreEqual(fragment, url.Fragment);
    }

    // curl's source (lib/urlapi.c) rejects a drive letter in a file URL outside Windows.
    [TestMethod]
    [DataRow("file:///C:/x")]
    [DataRow("file://C:")]
    [DataRow("file://c|/x")]
    public void TryParse_WithADriveLetterOutsideWindows_ReturnsFalse(string text)
    {
        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, driveLetters: false, out CurlUrl? url);

        Assert.IsFalse(parsed);
        Assert.IsNull(url);
    }

    [TestMethod]
    public void TryParse_WithAFilePathOutsideWindows_KeepsItsSlash()
    {
        bool parsed = CurlUrl.TryParse("file:///x/y", pathAsIs: false, driveLetters: false, out CurlUrl? url);

        Assert.IsTrue(parsed);
        Assert.AreEqual("/x/y", url!.AbsolutePath);
    }

    [TestMethod]
    public void TryParse_WithADriveLikeSchemeOutsideWindows_ReadsItAsAScheme()
    {
        bool parsed = CurlUrl.TryParse("c:/x", pathAsIs: false, driveLetters: false, out CurlUrl? url);

        Assert.IsTrue(parsed);
        Assert.AreEqual("c", url!.Scheme);
        Assert.AreEqual("x", url.Host);
    }

    [TestMethod]
    [DataRow("http://u:p;o@example.com:8080/p?q#f", "u", "p;o", null)]
    [DataRow("http://u;o@h/", "u;o", null, null)]
    [DataRow("http://u:p:q@h/", "u", "p:q", null)]
    [DataRow("http://:p@h/", "", "p", null)]
    [DataRow("http://@h/", "", null, null)]
    [DataRow("http://h/", null, null, null)]
    [DataRow("imap://u:p;AUTH=x@h/", "u", "p", "AUTH=x")]
    [DataRow("imap://u;AUTH=x:p@h/", "u", "p", "AUTH=x")]
    [DataRow("imap://;o@h/", "", null, "o")]
    [DataRow("pop3://u;a;b@h/", "u", null, "a;b")]
    [DataRow("u;o@imap.x/", "u;o", null, null)]
    public void TryParse_WithUserInformation_SplitsItAsCurlDoes(
        string text,
        string? user,
        string? password,
        string? options)
    {
        CurlUrl url = Parse(text);

        Assert.AreEqual(user, url.User);
        Assert.AreEqual(password, url.Password);
        Assert.AreEqual(options, url.Options);
    }

    [TestMethod]
    public void TryParse_WithEveryPart_ExposesEachAsCurlHoldsIt()
    {
        CurlUrl url = Parse("http://u:p;o@example.com:8080/p?q#f");

        Assert.AreEqual("example.com", url.Host);
        Assert.AreEqual(8080, url.Port);
        Assert.AreEqual("/p", url.AbsolutePath);
        Assert.AreEqual("q", url.Query);
        Assert.AreEqual("f", url.Fragment);
        Assert.IsNull(url.ZoneId);
    }

    [TestMethod]
    [DataRow("HTTP://EXAMPLE.com", "http", "EXAMPLE.com")]
    [DataRow("http://H%41/", "http", "HA")]
    [DataRow("http://0x7f.1/", "http", "127.0.0.1")]
    [DataRow("http://0X7f.1/", "http", "127.0.0.1")]
    [DataRow("http://2130706433/", "http", "127.0.0.1")]
    [DataRow("http://017700000001/", "http", "127.0.0.1")]
    [DataRow("http://1.2.3/", "http", "1.2.0.3")]
    [DataRow("http://1.256/", "http", "1.0.1.0")]
    [DataRow("http://1.2.3.4./", "http", "1.2.3.4")]
    [DataRow("http://1.2.3.4.5/", "http", "1.2.3.4.5")]
    [DataRow("http://1.2.3.4.5./", "http", "1.2.3.4.5.")]
    [DataRow("http://1.2.3.a./", "http", "1.2.3.a.")]
    [DataRow("http://999.1.1.1/", "http", "999.1.1.1")]
    [DataRow("http://08/", "http", "08")]
    [DataRow("http://0x/", "http", "0x")]
    [DataRow("http://4294967296/", "http", "4294967296")]
    [DataRow("http://0x100000000/", "http", "0x100000000")]
    [DataRow("http://077777777777/", "http", "077777777777")]
    [DataRow("http://0x7g/", "http", "0x7g")]
    [DataRow("http://1.16777216/", "http", "1.16777216")]
    [DataRow("http://1.2.65536/", "http", "1.2.65536")]
    [DataRow("http://1.2.3.256/", "http", "1.2.3.256")]
    [DataRow("http://h.example./", "http", "h.example.")]
    [DataRow("http://a..b/", "http", "a..b")]
    [DataRow("http://.a/", "http", ".a")]
    [DataRow("http://..a/", "http", "..a")]
    [DataRow("http://a-.b/", "http", "a-.b")]
    [DataRow("http://h_x/", "http", "h_x")]
    [DataRow("http://h~x/", "http", "h~x")]
    [DataRow("http://a%7Fb/", "http", "a\u007fb")]
    [DataRow("http://a%80b/", "http", "a\ufffdb")]
    [DataRow("example.com", "http", "example.com")]
    [DataRow("x:81", "http", "x")]
    [DataRow("ftpx.y", "http", "ftpx.y")]
    [DataRow("ftp.example.com", "ftp", "ftp.example.com")]
    [DataRow("FTP.x", "ftp", "FTP.x")]
    [DataRow("ftp.", "ftp", "ftp.")]
    [DataRow("Dict.x/p", "dict", "Dict.x")]
    [DataRow("ldap.x", "ldap", "ldap.x")]
    [DataRow("imap.x", "imap", "imap.x")]
    [DataRow("smtp.x", "smtp", "smtp.x")]
    [DataRow("pop3.x", "pop3", "pop3.x")]
    public void TryParse_WithAHost_HoldsItAsCurlDoes(string text, string scheme, string host)
    {
        CurlUrl url = Parse(text);

        Assert.AreEqual(scheme, url.Scheme);
        Assert.AreEqual(host, url.Host);
    }

    [TestMethod]
    public void TryParse_WithAHostIdnMappingRefuses_ResolvesTheHostAsWritten()
    {
        CurlUrl url = Parse("http://a%80b/");

        Assert.AreEqual("a\ufffdb", url.IdnHost);
    }

    [TestMethod]
    public void TryParse_WithASchemeOfFortyCharacters_ReadsTheScheme()
    {
        string scheme = new('a', 40);

        CurlUrl url = Parse(scheme + "://h/");

        Assert.AreEqual(scheme, url.Scheme);
    }

    [TestMethod]
    public void TryParse_WithASchemeOfFortyOneCharacters_ReturnsFalse()
    {
        AssertRejected(new string('a', 41) + "://h/");
    }

    [TestMethod]
    [DataRow("http://[::FFFF:1.2.3.4]/", "[::ffff:1.2.3.4]", "::ffff:1.2.3.4", null, 80)]
    [DataRow("http://[0:0:0:0:0:0:0:1]:8/", "[::1]", "::1", null, 8)]
    [DataRow("http://[::]/", "[::]", "::", null, 80)]
    [DataRow("http://[::1]:/", "[::1]", "::1", null, 80)]
    [DataRow("http://[1:2:3:4:5:6:7:8]/", "[1:2:3:4:5:6:7:8]", "1:2:3:4:5:6:7:8", null, 80)]
    [DataRow("http://[ffff::1.2.3.4]/", "[ffff::102:304]", "ffff::102:304", null, 80)]
    [DataRow("http://[::1.2.3.4]/", "[::1.2.3.4]", "::1.2.3.4", null, 80)]
    [DataRow("http://u:p@[::1]:8/", "[::1]", "::1", null, 8)]
    [DataRow("http://[fe80::1%25eth0]:81/x", "[fe80::1]", "fe80::1%eth0", "eth0", 81)]
    [DataRow("http://[fe80::1%eth0]/", "[fe80::1]", "fe80::1%eth0", "eth0", 80)]
    [DataRow("http://[::1%2]/", "[::1]", "::1%2", "2", 80)]
    [DataRow("http://[::1%2525]/", "[::1]", "::1%25", "25", 80)]
    [DataRow("http://[::1%25abcdefghijklmno]/", "[::1]", "::1%abcdefghijklmno", "abcdefghijklmno", 80)]
    public void TryParse_WithAnIPv6Host_NormalisesItAsCurlDoes(
        string text,
        string host,
        string idnHost,
        string? zoneId,
        int port)
    {
        CurlUrl url = Parse(text);

        Assert.AreEqual(host, url.Host);
        Assert.AreEqual(idnHost, url.IdnHost);
        Assert.AreEqual(zoneId, url.ZoneId);
        Assert.AreEqual(port, url.Port);
    }

    [TestMethod]
    [DataRow("http://example.com/a/%2e%2E/b/./c/.", "/b/c/")]
    [DataRow("http://h/a/./b/../../c", "/c")]
    [DataRow("http://h/a/..", "/")]
    [DataRow("http://h/a/.", "/a/")]
    [DataRow("http://h/..", "/")]
    [DataRow("http://h/./", "/")]
    [DataRow("http://h/a/./", "/a/")]
    [DataRow("http://h/.a/..b/...", "/.a/..b/...")]
    [DataRow("http://h/%2E%2e/x", "/x")]
    [DataRow("http://h/%2e", "/")]
    [DataRow("http://h/a/%2e%2e", "/")]
    [DataRow("http://h/.%2e/x", "/x")]
    [DataRow("http://h/a/../../../b", "/b")]
    [DataRow("http://h/a//../b", "/a/b")]
    [DataRow("http://h/%2e%2", "/%2e%2")]
    [DataRow("http://h/a/..%2f", "/a/..%2f")]
    [DataRow("http://h/a%20b", "/a%20b")]
    [DataRow("http://h/%zz", "/%zz")]
    [DataRow("http://example.com/a\\b", "/a/b")]
    [DataRow("http://example.com/a\\..\\b", "/b")]
    [DataRow("http://h\\x/", "/x/")]
    [DataRow("HTTP://h\\x", "/x")]
    [DataRow("http://h/a\\\\b", "/a//b")]
    [DataRow("http:/h/a\\b", "/a\\b")]
    [DataRow("http:///h/a\\b", "/a/b")]
    [DataRow("http://\\h/x", "/x")]
    [DataRow("http:/example.com/", "/")]
    [DataRow("http:///h/", "/")]
    public void TryParse_WithAPath_RemovesDotSegmentsAsCurlDoes(string text, string path)
    {
        Assert.AreEqual(path, Parse(text).AbsolutePath);
    }

    [TestMethod]
    [DataRow("http://h/a/./b/../c", "/a/./b/../c")]
    [DataRow("http://h/a\\..\\b", "/a/../b")]
    [DataRow("file:///C:/dir/../nope", "C:/dir/../nope")]
    public void TryParse_WithPathAsIs_KeepsDotSegments(string text, string path)
    {
        Assert.AreEqual(path, Parse(text, pathAsIs: true).AbsolutePath);
    }

    [TestMethod]
    [DataRow("http://example.com?x#y", "/", "x", "y")]
    [DataRow("http://h/?", "/", "", null)]
    [DataRow("http://h/#", "/", null, "")]
    [DataRow("http://h?#", "/", "", "")]
    [DataRow("http://h/#a#b", "/", null, "a#b")]
    [DataRow("http://h/?a/../b", "/", "a/../b", null)]
    [DataRow("http://h/a?#/../b", "/a", "", "/../b")]
    [DataRow("http://h#f?q", "/", null, "f?q")]
    [DataRow("http://h:8?q", "/", "q", null)]
    [DataRow("http://example.com/a\\b?c\\d#e\\f", "/a/b", "c\\d", "e\\f")]
    public void TryParse_WithAQueryOrFragment_SplitsThemAsCurlDoes(
        string text,
        string path,
        string? query,
        string? fragment)
    {
        CurlUrl url = Parse(text);

        Assert.AreEqual(path, url.AbsolutePath);
        Assert.AreEqual(query, url.Query);
        Assert.AreEqual(fragment, url.Fragment);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("http://")]
    [DataRow("http://a b.com/")]
    [DataRow("http://example.com/a b")]
    [DataRow("http://h#a b")]
    [DataRow("http://h/\u0001")]
    [DataRow("http://h/\u007f")]
    [DataRow("http://example.com:99999/")]
    [DataRow("http://example.com:65536/")]
    [DataRow("http://example.com:+80/")]
    [DataRow("http://h:80x/")]
    [DataRow("http://h:65535:1/")]
    [DataRow("http://h:1:/")]
    [DataRow("http://:80/")]
    [DataRow("http://user@/x")]
    [DataRow("http://a@b@c/")]
    [DataRow("http:////h/")]
    [DataRow("http://///h/")]
    [DataRow("http:///\\h/x")]
    [DataRow("http://\\\\h/x")]
    [DataRow("http:/\\h/x")]
    [DataRow("http:/h\\x")]
    [DataRow("http:example.com")]
    [DataRow("http:h")]
    [DataRow("x:")]
    [DataRow("c:/x")]
    [DataRow("h\\x/")]
    [DataRow("ftp.h\\x")]
    [DataRow("http://h%2fx/")]
    [DataRow("http://h%20x/")]
    [DataRow("http://h%00/")]
    [DataRow("http://h%01/")]
    [DataRow("http://h%1F/")]
    [DataRow("http://h%ZZ/")]
    [DataRow("http://h%/")]
    [DataRow("http://h%4/")]
    [DataRow("http://./")]
    [DataRow("http://../")]
    [DataRow("http://1../")]
    [DataRow("http://1.2.3../")]
    [DataRow("http://1.2.3.4../")]
    [DataRow("http://a.b../")]
    [DataRow("http://[::1/")]
    [DataRow("http://[::1")]
    [DataRow("http://[]/")]
    [DataRow("http://[:]/")]
    [DataRow("http://[::g]/")]
    [DataRow("http://[1::2::3]/")]
    [DataRow("http://[::1]x/")]
    [DataRow("http://[::1].x/")]
    [DataRow("http://[::1%25]/")]
    [DataRow("http://[::1%]/")]
    [DataRow("http://[::1%25abcdefghijklmnop]/")]
    [DataRow("http://[::1%abcdefghijklmnop]/")]
    [DataRow("http://[::1%25a%5D/")]
    public void TryParse_WithAUrlCurlRejects_ReturnsFalse(string text)
    {
        AssertRejected(text);
    }

    [TestMethod]
    public void TryParse_WithEveryCharacterCurlRefusesInAHostName_ReturnsFalse()
    {
        foreach (char character in "!\"#$%&'()*+,/:;<=>?@[\\]^`{|}")
        {
            string text = $"http://a%{(int)character:X2}b/";

            Assert.IsFalse(CurlUrl.TryParse(text, pathAsIs: false, driveLetters: true, out _), text);
        }
    }

    [TestMethod]
    public void TryParse_WithEveryCharacterCurlAllowsInAHostName_ReturnsTrue()
    {
        const string Allowed = "-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz~";
        foreach (char character in Allowed)
        {
            string text = $"http://a%{(int)character:X2}b/";

            Assert.IsTrue(CurlUrl.TryParse(text, pathAsIs: false, driveLetters: true, out _), text);
        }
    }

    // CURL_MAX_INPUT_LENGTH in curl's source; not measured, as no command line holds it.
    [TestMethod]
    public void TryParse_WithMoreThanEightMillionCharacters_ReturnsFalse()
    {
        AssertRejected("http://h/" + new string('a', 8_000_000));
    }

    [TestMethod]
    public void TryParse_WithMoreThanEightMillionUtf8Bytes_ReturnsFalse()
    {
        AssertRejected("http://h/" + new string('\u20ac', 3_000_000));
    }

    [TestMethod]
    public void TryParse_WithEightMillionCharacters_ReturnsTrue()
    {
        const string Prefix = "http://h/";

        CurlUrl url = Parse(Prefix + new string('a', 8_000_000 - Prefix.Length));

        Assert.AreEqual(8_000_000 - Prefix.Length + 1, url.AbsolutePath.Length);
    }

    private static CurlUrl Parse(string text, bool pathAsIs = false)
    {
        bool parsed = CurlUrl.TryParse(text, pathAsIs, driveLetters: true, out CurlUrl? url);

        Assert.IsTrue(parsed, text);

        return url!;
    }

    private static void AssertRejected(string text)
    {
        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, driveLetters: true, out CurlUrl? url);

        Assert.IsFalse(parsed, text);
        Assert.IsNull(url);
    }
}
