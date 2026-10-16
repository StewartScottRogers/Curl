namespace Curl.Api.Messages;

[TestClass]
public sealed class CurlRequestTests
{
    [TestMethod]
    public void Constructor_KeepsMethodAndUrl_WithNoHeadersAndNoBody()
    {
        CurlRequest request = new(HttpMethod.Put, "dict://example/word");

        Assert.AreEqual(HttpMethod.Put, request.Method);
        Assert.AreEqual("dict://example/word", request.Url);
        Assert.AreEqual(0, request.Headers.Count);
        Assert.IsNull(request.Body);
    }

    [TestMethod]
    public void Constructor_NullMethod_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlRequest(null!, "http://example/"));

    [TestMethod]
    public void Constructor_NullUrl_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlRequest(HttpMethod.Get, null!));

    [TestMethod]
    public void With_NullHeaders_Throws()
    {
        CurlRequest request = new(HttpMethod.Get, "http://example/");

        Assert.ThrowsExactly<ArgumentNullException>(() => request with { Headers = null! });
    }

    [TestMethod]
    public void With_ChangesOnePartAndLeavesTheOriginal()
    {
        CurlRequest original = new(HttpMethod.Get, "http://example/");

        CurlRequest changed = original with { Body = new byte[] { 1 } };

        Assert.IsNull(original.Body);
        Assert.AreEqual(1, changed.Body!.Value.Length);
        Assert.AreEqual(original.Url, changed.Url);
    }
}
