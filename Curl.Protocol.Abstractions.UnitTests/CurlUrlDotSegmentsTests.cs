namespace Curl.Protocol.Abstractions.UnitTests;

[TestClass]
public sealed class CurlUrlDotSegmentsTests
{
    [TestMethod]
    [DataRow("/a/b/c/./../../g", "/a/g")]
    [DataRow("./moo", "moo")]
    [DataRow("../moo", "moo")]
    [DataRow("..", "")]
    [DataRow("./", "")]
    [DataRow("%2e", "")]
    [DataRow("%2e%2E", "")]
    [DataRow(".%2e/a", "a")]
    [DataRow("..a/./b", "..a/b")]
    [DataRow(".a/./b", ".a/b")]
    [DataRow("/%2f%2e", "/%2f%2e")]
    [DataRow("/a/.%2e", "/")]
    public void Dedotdotify_PathWithDotSegments_RemovesThemAsUpstream(string input, string expected)
    {
        Assert.AreEqual(expected, CurlUrlDotSegments.Dedotdotify(input));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(".")]
    [DataRow("/%2f%2e%2")]
    [DataRow("/123?")]
    [DataRow("..well-known")]
    [DataRow("test/this")]
    public void Dedotdotify_PathWithoutDotSegments_IsNull(string input)
    {
        Assert.IsNull(CurlUrlDotSegments.Dedotdotify(input));
    }

    [TestMethod]
    public void Remove_PathWithoutDotSegments_IsThePathItself()
    {
        Assert.AreEqual("/a/b", CurlUrlDotSegments.Remove("/a/b"));
    }
}
