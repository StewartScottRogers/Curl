using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TryParse_WithNullText_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string? text = null;
        diagnostics.Arrange("url text", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CurlUrl.TryParse(text!, pathAsIs: false, out _));

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
        diagnostics.Assert("parameter name", "text", exception.ParamName);
        Assert.AreEqual("text", exception.ParamName);
    }

    [TestMethod]
    public void TryParse_OnThePublicOverload_ParsesAsCurlDoes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url text", "http://example.com/a/../b");

        bool parsed = CurlUrl.TryParse("http://example.com/a/../b", pathAsIs: false, out CurlUrl? url);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("absolute path", url?.AbsolutePath);
        diagnostics.Assert("parsed", true, parsed);
        diagnostics.Assert("absolute path", "/b", url?.AbsolutePath);
        Assert.IsTrue(parsed);
        Assert.AreEqual("/b", url!.AbsolutePath);
    }

    [TestMethod]
    public void Parse_WithAUrlCurlAccepts_ReturnsItParsed()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url text", "http://example.com/a/../b");

        CurlUrl url = CurlUrl.Parse("http://example.com/a/../b");

        diagnostics.Act("absolute path", url.AbsolutePath);
        diagnostics.Assert("absolute path", "/b", url.AbsolutePath);
        Assert.AreEqual("/b", url.AbsolutePath);
    }

    [TestMethod]
    public void Parse_WithPathAsIs_KeepsTheDotSegments()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url text", "http://example.com/a/../b");
        diagnostics.Arrange("path as is", true);

        CurlUrl url = CurlUrl.Parse("http://example.com/a/../b", pathAsIs: true);

        diagnostics.Act("absolute path", url.AbsolutePath);
        diagnostics.Assert("absolute path", "/a/../b", url.AbsolutePath);
        Assert.AreEqual("/a/../b", url.AbsolutePath);
    }

    [TestMethod]
    public void Parse_WithAUrlCurlRejects_ThrowsFormatException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url text", "http://exa mple.com/");

        var exception = Assert.ThrowsExactly<FormatException>(() => CurlUrl.Parse("http://exa mple.com/"));

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", nameof(FormatException), exception.GetType().Name);
        diagnostics.Diff("exception message", "curl rejects the URL \"http://exa mple.com/\".", exception.Message);
        Assert.AreEqual("curl rejects the URL \"http://exa mple.com/\".", exception.Message);
    }

    [TestMethod]
    public void Equals_WithTheSameTextParsedTwice_IsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url text", "http://example.com/a");

        CurlUrl first = CurlUrl.Parse("http://example.com/a");
        CurlUrl second = CurlUrl.Parse("http://example.com/a");

        diagnostics.Act("equal", first.Equals(second));
        diagnostics.Assert("equal", true, first.Equals(second));
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Equals_WithTheSameTextParsedWithAndWithoutPathAsIs_IsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url text", "http://example.com/a/../b");

        CurlUrl normalised = CurlUrl.Parse("http://example.com/a/../b");
        CurlUrl asIs = CurlUrl.Parse("http://example.com/a/../b", pathAsIs: true);

        diagnostics.Act("equal", normalised.Equals(asIs));
        diagnostics.Assert("equal", false, normalised.Equals(asIs));
        Assert.AreNotEqual(normalised, asIs);
    }

    [TestMethod]
    public void TryParse_WhenRejected_ReturnsFalseAndNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url text", "http://a b/");

        bool parsed = CurlUrl.TryParse("http://a b/", pathAsIs: false, out CurlUrl? url);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("url", url);
        diagnostics.Assert("parsed", false, parsed);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path as is", pathAsIs);

        CurlUrl url = Parse(diagnostics, text, pathAsIs: pathAsIs);

        diagnostics.Act("absolute path", url.AbsolutePath);
        diagnostics.Act("original string", url.OriginalString);
        diagnostics.Assert("absolute path", path, url.AbsolutePath);
        diagnostics.Assert("original string", text, url.OriginalString);
        Assert.AreEqual(path, url.AbsolutePath);
        Assert.AreEqual(text, url.OriginalString);
    }

    [TestMethod]
    [DataRow("file://user:pass@localhost/x")]
    [DataRow("file://ab:/x")]
    public void TryParse_WithAnAdr0010FileCaseCurlRejects_ReturnsFalse(string text)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRejected(diagnostics, text);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange(
            "expected parts",
            $"scheme={scheme} host={host} idnHost={idnHost} port={port} isDefaultPort={isDefaultPort} path={path}");

        CurlUrl url = Parse(diagnostics, text);

        diagnostics.Act(
            "actual parts",
            $"scheme={url.Scheme} host={url.Host} idnHost={url.IdnHost} port={url.Port} isDefaultPort={url.IsDefaultPort} path={url.AbsolutePath}");
        diagnostics.Assert("scheme", scheme, url.Scheme);
        diagnostics.Assert("host", host, url.Host);
        diagnostics.Assert("idn host", idnHost, url.IdnHost);
        diagnostics.Assert("port", port, url.Port);
        diagnostics.Assert("is default port", isDefaultPort, url.IsDefaultPort);
        diagnostics.Assert("absolute path", path, url.AbsolutePath);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = Parse(diagnostics, text);

        diagnostics.Act("host", url.Host);
        diagnostics.Act("user", url.User);
        diagnostics.Assert("absolute path", path, url.AbsolutePath);
        diagnostics.Assert("host", string.Empty, url.Host);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRejected(diagnostics, text);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected parts", $"path={path} query={query} fragment={fragment}");

        CurlUrl url = Parse(diagnostics, text);

        diagnostics.Act("actual parts", $"path={url.AbsolutePath} query={url.Query} fragment={url.Fragment}");
        diagnostics.Assert("absolute path", path, url.AbsolutePath);
        diagnostics.Assert("query", query, url.Query);
        diagnostics.Assert("fragment", fragment, url.Fragment);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url text", text);
        diagnostics.Arrange("drive letters", false);

        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, driveLetters: false, out CurlUrl? url);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("url", url);
        diagnostics.Assert("parsed", false, parsed);
        Assert.IsFalse(parsed);
        Assert.IsNull(url);
    }

    [TestMethod]
    public void TryParse_WithAFilePathOutsideWindows_KeepsItsSlash()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url text", "file:///x/y");
        diagnostics.Arrange("drive letters", false);

        bool parsed = CurlUrl.TryParse("file:///x/y", pathAsIs: false, driveLetters: false, out CurlUrl? url);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("absolute path", url?.AbsolutePath);
        diagnostics.Assert("parsed", true, parsed);
        diagnostics.Assert("absolute path", "/x/y", url?.AbsolutePath);
        Assert.IsTrue(parsed);
        Assert.AreEqual("/x/y", url!.AbsolutePath);
    }

    [TestMethod]
    public void TryParse_WithADriveLikeSchemeOutsideWindows_ReadsItAsAScheme()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url text", "c:/x");
        diagnostics.Arrange("drive letters", false);

        bool parsed = CurlUrl.TryParse("c:/x", pathAsIs: false, driveLetters: false, out CurlUrl? url);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("scheme", url?.Scheme);
        diagnostics.Act("host", url?.Host);
        diagnostics.Assert("parsed", true, parsed);
        diagnostics.Assert("scheme", "c", url?.Scheme);
        diagnostics.Assert("host", "x", url?.Host);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected login", $"user={user} password={password} options={options}");

        CurlUrl url = Parse(diagnostics, text);

        diagnostics.Act("actual login", $"user={url.User} password={url.Password} options={url.Options}");
        diagnostics.Assert("user", user, url.User);
        diagnostics.Assert("password", password, url.Password);
        diagnostics.Assert("options", options, url.Options);
        Assert.AreEqual(user, url.User);
        Assert.AreEqual(password, url.Password);
        Assert.AreEqual(options, url.Options);
    }

    [TestMethod]
    public void TryParse_WithEveryPart_ExposesEachAsCurlHoldsIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = Parse(diagnostics, "http://u:p;o@example.com:8080/p?q#f");

        diagnostics.Act(
            "parts",
            $"host={url.Host} port={url.Port} path={url.AbsolutePath} query={url.Query} fragment={url.Fragment} zoneId={url.ZoneId}");
        diagnostics.Assert("host", "example.com", url.Host);
        diagnostics.Assert("port", 8080, url.Port);
        diagnostics.Assert("absolute path", "/p", url.AbsolutePath);
        diagnostics.Assert("query", "q", url.Query);
        diagnostics.Assert("fragment", "f", url.Fragment);
        diagnostics.Assert("zone id", null, url.ZoneId);
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
    [DataRow("http://a\u200bb/", "http", "a\u200bb")]
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected parts", $"scheme={scheme} host={host}");

        CurlUrl url = Parse(diagnostics, text);

        diagnostics.Act("actual parts", $"scheme={url.Scheme} host={url.Host}");
        diagnostics.Assert("scheme", scheme, url.Scheme);
        diagnostics.Assert("host", host, url.Host);
        Assert.AreEqual(scheme, url.Scheme);
        Assert.AreEqual(host, url.Host);
    }

    [TestMethod]
    public void TryParse_WithAHostIdnMappingRefuses_ResolvesTheHostAsWritten()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = Parse(diagnostics, "http://a%80b/");

        diagnostics.Act("idn host", url.IdnHost);
        diagnostics.Assert("idn host", "a\ufffdb", url.IdnHost);
        Assert.AreEqual("a\ufffdb", url.IdnHost);
    }

    [TestMethod]
    public void TryParse_WithASchemeOfFortyCharacters_ReadsTheScheme()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string scheme = new('a', 40);

        CurlUrl url = Parse(diagnostics, scheme + "://h/");

        diagnostics.Act("scheme", url.Scheme);
        diagnostics.Assert("scheme", scheme, url.Scheme);
        Assert.AreEqual(scheme, url.Scheme);
    }

    [TestMethod]
    public void TryParse_WithASchemeOfFortyOneCharacters_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRejected(diagnostics, new string('a', 41) + "://h/");
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected parts", $"host={host} idnHost={idnHost} zoneId={zoneId} port={port}");

        CurlUrl url = Parse(diagnostics, text);

        diagnostics.Act("actual parts", $"host={url.Host} idnHost={url.IdnHost} zoneId={url.ZoneId} port={url.Port}");
        diagnostics.Assert("host", host, url.Host);
        diagnostics.Assert("idn host", idnHost, url.IdnHost);
        diagnostics.Assert("zone id", zoneId, url.ZoneId);
        diagnostics.Assert("port", port, url.Port);
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
    [DataRow("http://example.com/a\\b", "/a\\b")]
    [DataRow("http://example.com/a\\..\\b", "/a\\..\\b")]
    [DataRow("http://h/{}\\/214", "/{}\\/214")]
    [DataRow("http://h/a\\\\b", "/a\\\\b")]
    [DataRow("http:/h/a\\b", "/a\\b")]
    [DataRow("http:///h/a\\b", "/a\\b")]
    [DataRow("http://h/a\\b?c\\d", "/a\\b")]
    [DataRow("http:/example.com/", "/")]
    [DataRow("http:///h/", "/")]
    public void TryParse_WithAPath_RemovesDotSegmentsAsCurlDoes(string text, string path)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        string actual = Parse(diagnostics, text).AbsolutePath;

        diagnostics.Act("absolute path", actual);
        diagnostics.Diff("absolute path", path, actual);
        diagnostics.Assert("absolute path", path, actual);
        Assert.AreEqual(path, actual);
    }

    [TestMethod]
    [DataRow("http://h/a/./b/../c", "/a/./b/../c")]
    [DataRow("http://h/a\\..\\b", "/a\\..\\b")]
    [DataRow("file:///C:/dir/../nope", "C:/dir/../nope")]
    public void TryParse_WithPathAsIs_KeepsDotSegments(string text, string path)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path as is", true);

        string actual = Parse(diagnostics, text, pathAsIs: true).AbsolutePath;

        diagnostics.Act("absolute path", actual);
        diagnostics.Diff("absolute path", path, actual);
        diagnostics.Assert("absolute path", path, actual);
        Assert.AreEqual(path, actual);
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
    [DataRow("http://example.com/a\\b?c\\d#e\\f", "/a\\b", "c\\d", "e\\f")]
    public void TryParse_WithAQueryOrFragment_SplitsThemAsCurlDoes(
        string text,
        string path,
        string? query,
        string? fragment)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected parts", $"path={path} query={query} fragment={fragment}");

        CurlUrl url = Parse(diagnostics, text);

        diagnostics.Act("actual parts", $"path={url.AbsolutePath} query={url.Query} fragment={url.Fragment}");
        diagnostics.Assert("absolute path", path, url.AbsolutePath);
        diagnostics.Assert("query", query, url.Query);
        diagnostics.Assert("fragment", fragment, url.Fragment);
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
    [DataRow("http://h\\x/")]
    [DataRow("HTTP://h\\x")]
    [DataRow("http://\\h/x")]
    [DataRow("http:\\\\h/x")]
    [DataRow("http://h:1\\x/")]
    [DataRow("foo://h\\x")]
    public void TryParse_WithAUrlCurlRejects_ReturnsFalse(string text)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRejected(diagnostics, text);
    }

    [TestMethod]
    public void TryParse_WithEveryCharacterCurlRefusesInAHostName_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        const string Refused = "!\"#$%&'()*+,/:;<=>?@[\\]^`{|}";
        diagnostics.Arrange("refused characters", Refused);
        int refusedCount = 0;

        foreach (char character in Refused)
        {
            string text = $"http://a%{(int)character:X2}b/";

            Assert.IsFalse(CurlUrl.TryParse(text, pathAsIs: false, driveLetters: true, out _), text);
            refusedCount++;
        }

        diagnostics.Act("refused count", refusedCount);
        diagnostics.Assert("refused count", Refused.Length, refusedCount);
    }

    [TestMethod]
    public void TryParse_WithEveryCharacterCurlAllowsInAHostName_ReturnsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        const string Allowed = "-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz~";
        diagnostics.Arrange("allowed characters", Allowed);
        int acceptedCount = 0;

        foreach (char character in Allowed)
        {
            string text = $"http://a%{(int)character:X2}b/";

            Assert.IsTrue(CurlUrl.TryParse(text, pathAsIs: false, driveLetters: true, out _), text);
            acceptedCount++;
        }

        diagnostics.Act("accepted count", acceptedCount);
        diagnostics.Assert("accepted count", Allowed.Length, acceptedCount);
    }

    // CURL_MAX_INPUT_LENGTH in curl's source; not measured, as no command line holds it.
    [TestMethod]
    public void TryParse_WithMoreThanEightMillionCharacters_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRejected(diagnostics, "http://h/" + new string('a', 8_000_000), "http://h/ plus 8000000 a");
    }

    [TestMethod]
    public void TryParse_WithMoreThanEightMillionUtf8Bytes_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRejected(diagnostics, "http://h/" + new string('\u20ac', 3_000_000), "http://h/ plus 3000000 euro signs");
    }

    [TestMethod]
    public void TryParse_WithEightMillionCharacters_ReturnsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        const string Prefix = "http://h/";

        CurlUrl url = Parse(diagnostics, Prefix + new string('a', 8_000_000 - Prefix.Length), "http://h/ plus a to 8000000 characters");

        diagnostics.Act("absolute path length", url.AbsolutePath.Length);
        diagnostics.Assert("absolute path length", 8_000_000 - Prefix.Length + 1, url.AbsolutePath.Length);
        Assert.AreEqual(8_000_000 - Prefix.Length + 1, url.AbsolutePath.Length);
    }

    private static CurlUrl Parse(TestDiagnostics diagnostics, string text, string? description = null, bool pathAsIs = false)
    {
        diagnostics.Arrange("url text", description ?? text);

        bool parsed = CurlUrl.TryParse(text, pathAsIs, driveLetters: true, out CurlUrl? url);

        diagnostics.Act("parsed", parsed);
        Assert.IsTrue(parsed, text);

        return url!;
    }

    private static void AssertRejected(TestDiagnostics diagnostics, string text, string? description = null)
    {
        diagnostics.Arrange("url text", description ?? text);

        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, driveLetters: true, out CurlUrl? url);

        diagnostics.Act("parsed", parsed);
        diagnostics.Assert("parsed", false, parsed);
        Assert.IsFalse(parsed, text);
        Assert.IsNull(url);
    }
}
