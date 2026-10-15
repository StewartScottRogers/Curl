namespace Curl.Conformance.Http2Server;

[TestClass]
public sealed class UpstreamHttp2ServerFactoryTests
{
    [TestMethod]
    public void Create_Backend_ReturnsAnH2cServerInFrontOfIt()
    {
        Assert.IsInstanceOfType<Http2ServerConnector>(UpstreamHttp2ServerFactory.Create(new RecordingBackendConnector(null)));
    }

    [TestMethod]
    public void Create_NullBackend_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamHttp2ServerFactory.Create(null!));
    }
}
