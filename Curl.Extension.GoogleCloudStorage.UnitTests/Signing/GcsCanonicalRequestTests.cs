namespace Curl.Extension.GoogleCloudStorage.Signing;

[TestClass]
public sealed class GcsCanonicalRequestTests
{
    [TestMethod]
    public void Build_NullRequest_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => GcsCanonicalRequest.Build(null!));

    [TestMethod]
    public void Build_RepeatedAndSpacedHeadersAndRepeatedParameters_CanonicalizesThem()
    {
        var request = new GcsSigningRequest(
            "PUT",
            "/b/o",
            [Header("k", "2"), Header("k", "1")],
            [Header("X-Goog-Meta-A", "  one   two "), Header("x-goog-meta-a", "three"), Header("Host", "h")],
            "abc");

        var (canonicalRequest, signedHeaders) = GcsCanonicalRequest.Build(request);

        Assert.AreEqual("PUT\n/b/o\nk=1&k=2\nhost:h\nx-goog-meta-a:one two,three\n\nhost;x-goog-meta-a\nabc", canonicalRequest);
        Assert.AreEqual("host;x-goog-meta-a", signedHeaders);
    }

    private static KeyValuePair<string, string> Header(string name, string value) => new(name, value);
}
