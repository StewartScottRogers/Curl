using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>Pins that NO_PROXY and --noproxy are matched against the host a cloud-storage URL connects to, not its bucket or account (BL-5480, EXT-086).</summary>
[TestClass]
public sealed class ProxySelectorStorageSchemeNoProxyTests
{
    private const string Proxy = "http://127.0.0.1:47051";

    private static bool GoesViaProxy(string url, string noProxy, params (string Name, string Value)[] extra)
    {
        Dictionary<string, string> env = new(StringComparer.Ordinal) { ["HTTPS_PROXY"] = Proxy, ["ALL_PROXY"] = Proxy };
        foreach ((string name, string value) in extra)
        {
            env[name] = value;
        }

        ProxySelector selector = new(name => env.GetValueOrDefault(name));
        Assert.IsTrue(selector.TrySelect(CurlUrl.Parse(url), null, ProxyKind.Http, noProxy, out ProxyEndpoint? proxy, out _));
        return proxy is not null;
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("localhost,127.0.0.1")]
    public void TrySelect_S3EndpointHostInNoProxy_ConnectsDirectly(string noProxy)
    {
        Assert.IsFalse(GoesViaProxy("s3://bkt/a.txt", noProxy, ("AWS_ENDPOINT_URL", "http://127.0.0.1:47040")));
    }

    [TestMethod]
    public void TrySelect_GcsEmulatorHostInNoProxy_ConnectsDirectly()
    {
        Assert.IsFalse(GoesViaProxy("gs://gbkt/a.txt", "127.0.0.1", ("STORAGE_EMULATOR_HOST", "https://127.0.0.1:47020")));
        Assert.IsFalse(GoesViaProxy("gs://gbkt/a.txt", "127.0.0.1", ("STORAGE_EMULATOR_HOST", "127.0.0.1:47020")));
    }

    [TestMethod]
    [DataRow("s3://bkt/a.txt", "amazonaws.com")]
    [DataRow("s3://a.b/a.txt", "amazonaws.com")]
    [DataRow("gs://gbkt/a.txt", "googleapis.com")]
    [DataRow("az://acct/ctr/x", "windows.net")]
    [DataRow("abfss://fs@acct/x", "windows.net")]
    [DataRow("abfs://fs@acct.dfs.core.windows.net/x", "windows.net")]
    public void TrySelect_RealCloudHostSuffixInNoProxy_ConnectsDirectly(string url, string noProxy)
    {
        Assert.IsFalse(GoesViaProxy(url, noProxy));
    }

    [TestMethod]
    [DataRow("s3://bkt/a.txt", "bkt")]
    [DataRow("gs://gbkt/a.txt", "gbkt")]
    [DataRow("az://acct/ctr/x", "acct")]
    [DataRow("abfss://fs@acct/x", "acct")]
    public void TrySelect_BucketOrAccountNameInNoProxy_StillUsesTheProxy(string url, string noProxy)
    {
        Assert.IsTrue(GoesViaProxy(url, noProxy));
    }

    [TestMethod]
    public void TrySelect_AzureLocalEmulatorHost_IsMatchedAsWritten()
    {
        Assert.IsFalse(GoesViaProxy("az://127.0.0.1:10000/acct/ctr", "127.0.0.1"));
        Assert.IsFalse(GoesViaProxy("az://localhost:10000/acct/ctr", "localhost"));
    }

    [TestMethod]
    public void TrySelect_S3RegionAndInvalidEndpoint_FallBackToTheAwsHost()
    {
        Assert.IsFalse(GoesViaProxy("s3://bkt/a", "bkt.s3.eu-west-1.amazonaws.com", ("AWS_REGION", "eu-west-1")));
        Assert.IsFalse(GoesViaProxy("s3://bkt/a", "bkt.s3.eu-west-2.amazonaws.com", ("AWS_DEFAULT_REGION", "eu-west-2"), ("AWS_ENDPOINT_URL_S3", "ftp://x")));
        Assert.IsFalse(GoesViaProxy("gs://gbkt/a", "storage.googleapis.com", ("STORAGE_EMULATOR_HOST", "ftp://x")));
    }
}
