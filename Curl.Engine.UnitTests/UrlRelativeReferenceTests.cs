using Curl.Api.EngineContracts.Url;

namespace Curl.Engine;

[TestClass]
public sealed class UrlRelativeReferenceTests
{
    [TestMethod]
    [DataRow("http://x", CurlUrlFlags.None, true)]
    [DataRow("foo:bar", CurlUrlFlags.None, true)]
    [DataRow("foo:bar", CurlUrlFlags.GuessScheme, false)]
    [DataRow("foo:/bar", CurlUrlFlags.DefaultScheme, true)]
    [DataRow("foo:", CurlUrlFlags.GuessScheme, false)]
    [DataRow("1x:y", CurlUrlFlags.None, false)]
    [DataRow("", CurlUrlFlags.None, false)]
    [DataRow("abc", CurlUrlFlags.None, false)]
    [DataRow("a_b:c", CurlUrlFlags.None, false)]
    [DataRow("abcdefghijabcdefghijabcdefghijabcdefghijk:x", CurlUrlFlags.None, false)]
    public void IsAbsolute_Text_SaysWhetherItStartsWithASchemeAndColon(string text, CurlUrlFlags flags, bool expected)
    {
        bool absolute = UrlRelativeReference.IsAbsolute(text, flags);

        Assert.AreEqual(expected, absolute);
    }

    [TestMethod]
    [DataRow("c", "http://h/a/c")]
    [DataRow("c?x y", "http://h/a/c?x+y")]
    [DataRow("/x", "http://h/x")]
    [DataRow("//o/p", "http://o/p")]
    [DataRow("?n", "http://h/a/b?n")]
    [DataRow("#g", "http://h/a/b?q#g")]
    [DataRow("", "http://h/a/b?q")]
    public void Resolve_Reference_BuildsTheUrlAsUpstreamsRedirectUrl(string relative, string expected)
    {
        string resolved = UrlRelativeReference.Resolve("http://h/a/b?q#f", relative);

        Assert.AreEqual(expected, resolved);
    }

    [TestMethod]
    public void Resolve_ReferenceOnAUrlWithoutQueryOrFragment_KeepsThePath()
    {
        string resolved = UrlRelativeReference.Resolve("http://h/a/b", "#g");

        Assert.AreEqual("http://h/a/b#g", resolved);
    }
}
