namespace Curl.Api.Concurrency;

[TestClass]
public sealed class CurlClientOptionsConcurrencyTests
{
    [TestMethod]
    public void NewOptions_ConnectionLimits_DefaultToNoLimit()
    {
        CurlClientOptions options = new();

        Assert.AreEqual(0, options.MaxConnections);
        Assert.AreEqual(0, options.MaxConnectionsPerHost);
    }

    [TestMethod]
    public void ConnectionLimits_SetToPositiveValues_AreKept()
    {
        CurlClientOptions options = new() { MaxConnections = 6, MaxConnectionsPerHost = 2 };

        Assert.AreEqual(6, options.MaxConnections);
        Assert.AreEqual(2, options.MaxConnectionsPerHost);
    }

    [TestMethod]
    public void MaxConnections_Negative_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlClientOptions { MaxConnections = -1 });

    [TestMethod]
    public void MaxConnectionsPerHost_Negative_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlClientOptions { MaxConnectionsPerHost = -1 });
}
