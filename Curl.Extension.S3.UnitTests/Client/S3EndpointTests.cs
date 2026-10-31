namespace Curl.Extension.S3.Client;

/// <summary>Checks <see cref="S3Endpoint"/> against the Amazon S3 user guide's "Virtual hosting of buckets".</summary>
[TestClass]
public sealed class S3EndpointTests
{
    [TestMethod]
    public void For_AwsBucket_IsVirtualHostedOverTls()
    {
        S3Endpoint endpoint = S3Endpoint.For("bucket", "us-west-2", null)!;

        Assert.AreEqual(new S3Endpoint("bucket.s3.us-west-2.amazonaws.com", 443, true, false), endpoint);
        Assert.AreEqual("bucket.s3.us-west-2.amazonaws.com", endpoint.HostHeader);
        Assert.AreEqual("/a/b.txt", endpoint.PathOf("bucket", "a/b.txt"));
        Assert.AreEqual("/", endpoint.PathOf("bucket", string.Empty));
    }

    [TestMethod]
    public void For_AwsBucketWithADot_IsPathStyle()
    {
        S3Endpoint endpoint = S3Endpoint.For("my.bucket", "us-east-1", null)!;

        Assert.AreEqual(new S3Endpoint("s3.us-east-1.amazonaws.com", 443, true, true), endpoint);
        Assert.AreEqual("/my.bucket/k", endpoint.PathOf("my.bucket", "k"));
        Assert.AreEqual("/my.bucket", endpoint.PathOf("my.bucket", string.Empty));
    }

    [TestMethod]
    public void For_HttpEndpointOnAnotherPort_IsPathStyleAndNamesThePort()
    {
        S3Endpoint endpoint = S3Endpoint.For("bucket", "us-east-1", "http://minio:9000")!;

        Assert.AreEqual(new S3Endpoint("minio", 9000, false, true), endpoint);
        Assert.AreEqual("minio:9000", endpoint.HostHeader);
    }

    [TestMethod]
    public void For_HttpsEndpoint_UsesTlsOnItsDefaultPort()
    {
        S3Endpoint endpoint = S3Endpoint.For("bucket", "auto", "https://account.r2.cloudflarestorage.com")!;

        Assert.AreEqual(new S3Endpoint("account.r2.cloudflarestorage.com", 443, true, true), endpoint);
        Assert.AreEqual("account.r2.cloudflarestorage.com", endpoint.HostHeader);
        Assert.AreEqual("minio", new S3Endpoint("minio", 80, false, true).HostHeader);
        Assert.AreEqual("minio:443", new S3Endpoint("minio", 443, false, true).HostHeader);
    }

    [TestMethod]
    [DataRow("ftp://minio")]
    [DataRow("not a url")]
    public void For_EndpointThatIsNotHttp_IsNull(string endpointUrl) =>
        Assert.IsNull(S3Endpoint.For("bucket", "us-east-1", endpointUrl));
}
