namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="CurlUrl.TryParseAllowingEmptyHost" />, libcurl's <c>CURLU_NO_AUTHORITY</c>: at most
/// two slashes after the scheme, the rest starting the path, and an empty authority an empty host, as
/// curl 8.22.0's <c>parseurl</c> reads lib1560's rows (BL-2734).
/// </summary>
[TestClass]
public sealed class CurlUrlAllowingEmptyHostTests
{
    [TestMethod]
    [DataRow("weird:///path", "/path", null)]
    [DataRow("weird:////path", "//path", null)]
    [DataRow("custom-scheme://?expected=test-new-good", "/", "expected=test-new-good")]
    [DataRow("http://", "/", null)]
    public void TryParseAllowingEmptyHost_EmptyAuthority_IsAnEmptyHost(string text, string path, string? query)
    {
        bool parsed = CurlUrl.TryParseAllowingEmptyHost(text, pathAsIs: false, disallowUser: false, out CurlUrl? url, out CurlUrlRejection rejection);

        Assert.IsTrue(parsed);
        Assert.AreEqual(CurlUrlRejection.None, rejection);
        Assert.AreEqual(string.Empty, url!.Host);
        Assert.AreEqual(path, url.AbsolutePath);
        Assert.AreEqual(query, url.Query);
    }

    [TestMethod]
    public void TryParseAllowingEmptyHost_HostWritten_IsThatHost()
    {
        bool parsed = CurlUrl.TryParseAllowingEmptyHost("custom-scheme://host?q", pathAsIs: false, disallowUser: false, out CurlUrl? url, out _);

        Assert.IsTrue(parsed);
        Assert.AreEqual("host", url!.Host);
        Assert.AreEqual("q", url.Query);
    }

    [TestMethod]
    public void TryParseAllowingEmptyHost_SchemeLessEmptyAuthority_GuessesHttp()
    {
        bool parsed = CurlUrl.TryParseAllowingEmptyHost("/path", pathAsIs: false, disallowUser: false, out CurlUrl? url, out _);

        Assert.IsTrue(parsed);
        Assert.AreEqual("http", url!.Scheme);
        Assert.AreEqual(string.Empty, url.Host);
        Assert.AreEqual("/path", url.AbsolutePath);
    }

    [TestMethod]
    [DataRow("weird://u@/", false, CurlUrlRejection.NoHost)]
    [DataRow("weird://u@h/", true, CurlUrlRejection.UserNotAllowed)]
    public void TryParseAllowingEmptyHost_LoginOrUserRefused_SaysWhy(string text, bool disallowUser, CurlUrlRejection expected)
    {
        bool parsed = CurlUrl.TryParseAllowingEmptyHost(text, pathAsIs: false, disallowUser, out CurlUrl? url, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.IsNull(url);
        Assert.AreEqual(expected, rejection);
    }

    [TestMethod]
    public void TryParseAllowingEmptyHost_NullText_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlUrl.TryParseAllowingEmptyHost(null!, pathAsIs: false, disallowUser: false, out _, out _));
}
