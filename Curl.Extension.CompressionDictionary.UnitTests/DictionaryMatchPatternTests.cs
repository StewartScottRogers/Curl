namespace Curl.Extension.CompressionDictionary.UnitTests;

[TestClass]
public sealed class DictionaryMatchPatternTests
{
    private static readonly Uri DictionaryUrl = new("https://example.com/app/dictionary.dat");

    // RFC 9842 sections 2.1.1 and 2.2.2: the pattern resolves against the dictionary URL and matches same-origin URLs.
    [TestMethod]
    [DataRow("/app/*", "https://example.com/app/main.js", true)]
    [DataRow("/app/*", "https://EXAMPLE.com/app/main.js?v=2", true)]
    [DataRow("/app/*", "https://example.com/other/main.js", false)]
    [DataRow("/app/*", "https://other.example/app/main.js", false)]
    [DataRow("/app/*", "http://example.com/app/main.js", false)]
    [DataRow("/app/*", "https://example.com:8443/app/main.js", false)]
    [DataRow("*.js", "https://example.com/app/main.js", true)]
    [DataRow("https://example.com/app/*.js", "https://example.com/app/x.js", true)]
    [DataRow("/app/:version/main.js", "https://example.com/app/v2/main.js", true)]
    [DataRow("/app/:version/main.js", "https://example.com/app/v2/x/main.js", false)]
    [DataRow("/app/:", "https://example.com/app/:", true)]
    [DataRow("/a.js", "https://example.com/aXjs", false)]
    [DataRow("/app/*?lang=*", "https://example.com/app/x?lang=en", true)]
    [DataRow("/app/*?lang=*", "https://example.com/app/x?region=en", false)]
    [DataRow("/app/*?lang=:code", "https://example.com/app/x?lang=en", true)]
    public void Matches_RequestUrl_FollowsThePattern(string match, string requestUrl, bool expected)
    {
        var pattern = DictionaryMatchPattern.Compile(DictionaryUrl, match);

        Assert.IsNotNull(pattern);
        Assert.AreEqual(expected, pattern.Matches(new Uri(requestUrl)));
    }

    // RFC 9842 section 2.1.1: a cross-origin pattern or one with regexp groups is ignored.
    [TestMethod]
    [DataRow("https://other.example/app/*")]
    [DataRow("/app/(\\d+)/*")]
    [DataRow("https://exa mple.com:99999/")]
    public void Compile_IgnoredPattern_ReturnsNull(string match) =>
        Assert.IsNull(DictionaryMatchPattern.Compile(DictionaryUrl, match));
}
