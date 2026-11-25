namespace Curl.Extended.Protocol.GoogleCloudStorage.Client;

/// <summary>Pins the endpoint against the XML API overview's "Request endpoints" (path-style <c>storage.googleapis.com/bucket/object</c>) and <c>STORAGE_EMULATOR_HOST</c>.</summary>
[TestClass]
public sealed class GcsEndpointTests
{
    [TestMethod]
    public void For_NoEmulator_IsTheXmlApiOverTls()
    {
        GcsEndpoint endpoint = GcsEndpoint.For(null)!;

        Assert.AreEqual(new GcsEndpoint("storage.googleapis.com", 443, true), endpoint);
        Assert.AreEqual("storage.googleapis.com", endpoint.HostHeader);
    }

    [TestMethod]
    public void For_BareHostAndPort_IsTakenAsHttp()
    {
        GcsEndpoint endpoint = GcsEndpoint.For("localhost:4443")!;

        Assert.AreEqual(new GcsEndpoint("localhost", 4443, false), endpoint);
        Assert.AreEqual("localhost:4443", endpoint.HostHeader);
    }

    [TestMethod]
    public void For_HttpUrlOnPort80_LeavesThePortOutOfTheHostHeader() =>
        Assert.AreEqual("emulator", GcsEndpoint.For("http://emulator")!.HostHeader);

    [TestMethod]
    public void For_HttpsUrl_UsesTls() =>
        Assert.AreEqual(new GcsEndpoint("emulator", 8443, true), GcsEndpoint.For("https://emulator:8443"));

    [TestMethod]
    [DataRow("ftp://emulator")]
    [DataRow("http://")]
    public void For_NotAnHttpEndpoint_IsNull(string emulatorHost) =>
        Assert.IsNull(GcsEndpoint.For(emulatorHost));

    [TestMethod]
    [DataRow("", "/bucket")]
    [DataRow("dir/a.txt", "/bucket/dir/a.txt")]
    public void PathOf_PutsTheBucketFirst(string objectName, string path) =>
        Assert.AreEqual(path, GcsEndpoint.PathOf("bucket", objectName));

    [TestMethod]
    [DataRow("s3://12345678/k", "0.188.97.78", "12345678")]
    [DataRow("gs://0x10/k", "0.0.0.16", "0x10")]
    [DataRow("s3://user:pw@bucket:8080/k?a=b#c", "bucket", "bucket")]
    [DataRow("s3://bucket", "bucket", "bucket")]
    [DataRow("s3://[::1]/k", "[::1]", "[::1]")]
    [DataRow("no-scheme", "fallback", "fallback")]
    public void BucketOf_OriginalUrl_KeepsTheTextTheUserWrote(string url, string host, string expected) =>
        Assert.AreEqual(expected, GcsEndpoint.BucketOf(url, host));
}
