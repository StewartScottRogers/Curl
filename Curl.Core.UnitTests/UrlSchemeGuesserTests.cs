namespace Curl.Core;

/// <summary>
/// Pins the scheme <see cref="UrlSchemeGuesser" /> gives a URL typed without one, each case
/// measured against curl 8.21.0 on 2026-09-26 as the <c>%{scheme}</c> and
/// <c>%{url_effective}</c> of <c>curl -s -m 1 -o /dev/null "&lt;url&gt;"</c>.
/// </summary>
[TestClass]
public sealed class UrlSchemeGuesserTests
{
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
        Assert.AreEqual(expected, UrlSchemeGuesser.AddGuessedScheme(url));
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
        Assert.AreEqual("http://" + url, UrlSchemeGuesser.AddGuessedScheme(url));
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
        Assert.AreEqual(expected, UrlSchemeGuesser.AddGuessedScheme(url));
    }

    [TestMethod]
    [DataRow("HTTP://ftp.localhost:1")]
    [DataRow("http:/ftp.localhost:1")]
    [DataRow("foo:/x")]
    [DataRow("ftp.localhost:/x")]
    [DataRow("a+b.c-d://x")]
    public void AddGuessedScheme_UrlNamesAScheme_IsUnchanged(string url)
    {
        Assert.AreEqual(url, UrlSchemeGuesser.AddGuessedScheme(url));
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
        Assert.IsFalse(UrlSchemeGuesser.HasScheme(url));
    }

    [TestMethod]
    public void AddGuessedScheme_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlSchemeGuesser.AddGuessedScheme(null!));
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
        Assert.AreEqual(expected, UrlSchemeGuesser.AddScheme(url, defaultScheme));
    }

    [TestMethod]
    public void AddScheme_DefaultSchemeAndUrlNamesAScheme_IsUnchanged()
    {
        Assert.AreEqual("http://127.0.0.1:1/", UrlSchemeGuesser.AddScheme("http://127.0.0.1:1/", "ftp"));
    }

    [TestMethod]
    public void AddScheme_NoDefaultScheme_Guesses()
    {
        Assert.AreEqual("ftp://ftp.localhost:1/", UrlSchemeGuesser.AddScheme("ftp.localhost:1/", null));
    }

    [TestMethod]
    public void AddScheme_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlSchemeGuesser.AddScheme(null!, "https"));
    }

    [TestMethod]
    public void HasScheme_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlSchemeGuesser.HasScheme(null!));
    }

    [TestMethod]
    public void GuessScheme_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlSchemeGuesser.GuessScheme(null!));
    }
}
