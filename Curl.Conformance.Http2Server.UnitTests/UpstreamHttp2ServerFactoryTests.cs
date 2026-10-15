using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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

    [TestMethod]
    public void CreateTls_BackendAndCertificate_ReturnsATlsServerInFrontOfIt()
    {
        using X509Certificate2 certificate = CreateCertificate();

        Assert.IsInstanceOfType<Http2TlsServerConnector>(UpstreamHttp2ServerFactory.CreateTls(new RecordingBackendConnector(null), certificate));
    }

    [TestMethod]
    public void CreateTls_NullBackend_Throws()
    {
        using X509Certificate2 certificate = CreateCertificate();

        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamHttp2ServerFactory.CreateTls(null!, certificate));
    }

    [TestMethod]
    public void CreateTls_NullCertificate_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamHttp2ServerFactory.CreateTls(new RecordingBackendConnector(null), null!));
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create();
        return new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
