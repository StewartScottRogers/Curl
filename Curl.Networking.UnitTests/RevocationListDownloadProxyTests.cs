namespace Curl.Networking.UnitTests;

[TestClass]
public sealed class RevocationListDownloadProxyTests
{
    [TestMethod]
    public void CreateHandler_ProxyEnvironmentVariablesSet_DoesNotUseAProxy()
    {
        using var handler = RevocationListDownload.CreateHandler();

        Assert.IsFalse(handler.UseProxy);
    }
}
