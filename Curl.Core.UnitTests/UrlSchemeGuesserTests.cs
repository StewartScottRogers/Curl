using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins the scheme <see cref="UrlSchemeGuesser" /> gives a URL typed without one, each case
/// measured against curl 8.21.0 on 2026-09-26 as the <c>%{scheme}</c> and
/// <c>%{url_effective}</c> of <c>curl -s -m 1 -o /dev/null "&lt;url&gt;"</c>.
/// </summary>
[TestClass]
public sealed class UrlSchemeGuesserTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("example.localhost:1", "http://example.localhost:1")]
    [DataRow("ftp.localhost:1", "ftp://ftp.localhost:1")]
    [DataRow("FTP.localhost:1", "ftp://FTP.localhost:1")]
    [DataRow("Ftp.localhost:1", "ftp://Ftp.localhost:1")]
    [DataRow("dict.localhost:1", "dict://dict.localhost:1")]
    [DataRow("ldap.localhost:1", "ldap://ldap.localhost:1")]
    [DataRow("imap.localhost:1", "imap://imap.localhost:1")]
    [DataRow("smtp.localhost:1", "smtp://smtp.localhost:1")]
    [DataRow("pop3.localhost:1", "pop3://pop3.localhost:1")]
    [DataRow("ftp.:1", "ftp://ftp.:1")]
    public void AddGuessedScheme_HostPrefix_PicksItsScheme(string url, string expected)
    {
        Assert.AreEqual(expected, Guess(url, expected));
    }

    [TestMethod]
    [DataRow("pop3s.localhost:1")]
    [DataRow("ftps.localhost:1")]
    [DataRow("ftpx.localhost:1")]
    [DataRow("ftp:1")]
    [DataRow("[::1]:1")]
    [DataRow("a:b@c:1")]
    public void AddGuessedScheme_NoPrefix_IsHttp(string url)
    {
        Assert.AreEqual("http://" + url, Guess(url, "http://" + url));
    }

    [TestMethod]
    [DataRow("u:p@ftp.localhost:1", "ftp://u:p@ftp.localhost:1")]
    [DataRow("u@dict.localhost:1", "dict://u@dict.localhost:1")]
    [DataRow("ftp.x@dict.localhost:1", "dict://ftp.x@dict.localhost:1")]
    [DataRow("u@ftp.localhost:1/p@imap.x", "ftp://u@ftp.localhost:1/p@imap.x")]
    [DataRow("ftp.localhost?x=1", "ftp://ftp.localhost?x=1")]
    [DataRow("ftp.localhost:1#frag", "ftp://ftp.localhost:1#frag")]
    [DataRow("imap.localhost/ftp.x", "imap://imap.localhost/ftp.x")]
    public void AddGuessedScheme_UserInfoPathQueryFragment_ReadsOnlyTheHost(string url, string expected)
    {
        Assert.AreEqual(expected, Guess(url, expected));
    }

    [TestMethod]
    [DataRow("HTTP://ftp.localhost:1")]
    [DataRow("http:/ftp.localhost:1")]
    [DataRow("foo:/x")]
    [DataRow("ftp.localhost:/x")]
    [DataRow("a+b.c-d://x")]
    public void AddGuessedScheme_UrlNamesAScheme_IsUnchanged(string url)
    {
        Assert.AreEqual(url, Guess(url, url));
    }

    [TestMethod]
    [DataRow("localhost:1")]
    [DataRow("1host://x")]
    [DataRow("ho_st://x")]
    [DataRow("host")]
    [DataRow("host:")]
    [DataRow("")]
    public void HasScheme_NoSchemeThenColonSlash_IsFalse(string url)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", url);

        var hasScheme = UrlSchemeGuesser.HasScheme(url);

        diagnostics.Act("has scheme", hasScheme);
        diagnostics.Assert("has scheme", false, hasScheme);
        Assert.IsFalse(hasScheme);
    }

    [TestMethod]
    public void AddGuessedScheme_Null_Throws()
    {
        WriteNullCall("AddGuessedScheme(null)");
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => UrlSchemeGuesser.AddGuessedScheme(null!));
        WriteException(exception);
    }

    // --proto-default cases measured with Record-CurlExchange.ps1, curl 8.21.0, Windows, 2026-09-28 (BL-524).
    [TestMethod]
    [DataRow("127.0.0.1:18524/", "https", "https://127.0.0.1:18524/")]
    [DataRow("127.0.0.1:18525/", "ftp", "ftp://127.0.0.1:18525/")]
    [DataRow("ftp.localhost:1/", "dict", "dict://ftp.localhost:1/")]
    [DataRow("ftp.localhost:1/", "https", "https://ftp.localhost:1/")]
    [DataRow("u:p@dict.localhost:1/x", "ftp", "ftp://u:p@dict.localhost:1/x")]
    public void AddScheme_DefaultScheme_ReplacesTheGuess(string url, string defaultScheme, string expected)
    {
        Assert.AreEqual(expected, Add(url, defaultScheme, expected));
    }

    [TestMethod]
    public void AddScheme_DefaultSchemeAndUrlNamesAScheme_IsUnchanged()
    {
        Assert.AreEqual("http://127.0.0.1:1/", Add("http://127.0.0.1:1/", "ftp", "http://127.0.0.1:1/"));
    }

    [TestMethod]
    public void AddScheme_NoDefaultScheme_Guesses()
    {
        Assert.AreEqual("ftp://ftp.localhost:1/", Add("ftp.localhost:1/", null, "ftp://ftp.localhost:1/"));
    }

    [TestMethod]
    public void AddScheme_Null_Throws()
    {
        WriteNullCall("AddScheme(null, \"https\")");
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => UrlSchemeGuesser.AddScheme(null!, "https"));
        WriteException(exception);
    }

    [TestMethod]
    public void HasScheme_Null_Throws()
    {
        WriteNullCall("HasScheme(null)");
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => UrlSchemeGuesser.HasScheme(null!));
        WriteException(exception);
    }

    [TestMethod]
    public void GuessScheme_Null_Throws()
    {
        WriteNullCall("GuessScheme(null)");
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => UrlSchemeGuesser.GuessScheme(null!));
        WriteException(exception);
    }

    private string Guess(string url, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", url);

        var actual = UrlSchemeGuesser.AddGuessedScheme(url);

        diagnostics.Act("with scheme", actual);
        diagnostics.Assert("with scheme", expected, actual);
        return actual;
    }

    private string Add(string url, string? defaultScheme, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("default scheme", defaultScheme ?? "(none)");

        var actual = UrlSchemeGuesser.AddScheme(url, defaultScheme);

        diagnostics.Act("with scheme", actual);
        diagnostics.Assert("with scheme", expected, actual);
        return actual;
    }

    private void WriteNullCall(string call) => TestDiagnostics.For(TestContext).Arrange("call", call);

    private void WriteException(ArgumentNullException exception)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Act("exception", exception.GetType().Name + " (" + exception.ParamName + ")");
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }
}
