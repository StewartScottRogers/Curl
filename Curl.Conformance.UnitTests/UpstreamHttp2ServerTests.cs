using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins that <see cref="UpstreamHttp2Server.InFrontOf"/> sends connections to
/// <see cref="UpstreamHttp2Server.Http2Port"/> to a fresh server over the given backend and every other port on.
/// </summary>
[TestClass]
public sealed class UpstreamHttp2ServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(UpstreamHttp2Server.Http2Port, "http2 over sws")]
    [DataRow(8990, "other")]
    public async Task InFrontOf_ConnectToPort_ReachesTheServerForThatPort(int port, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("port", port);
        UpstreamHttp2Server server = new(backend => new NamedConnector("http2 over " + ((NamedConnector)backend).Name));
        IConnector servers = server.InFrontOf(new NamedConnector("other"), new NamedConnector("sws"));

        ConnectResult result = await servers.ConnectAsync(new ConnectTarget("127.0.0.1", port, false), CancellationToken.None);

        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("server reached", expected, result.ErrorMessage);
        Assert.AreEqual(expected, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(true, true, "tls over sws")]
    [DataRow(true, false, "other")]
    [DataRow(false, true, "other")]
    public async Task InFrontOf_ConnectToTheTlsPort_ReachesTheTlsServerOnlyWithACertificateAndATlsServer(bool hasTlsServer, bool hasCertificate, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("has TLS server", hasTlsServer);
        diagnostics.Arrange("has certificate", hasCertificate);
        using var key = System.Security.Cryptography.ECDsa.Create();
        using System.Security.Cryptography.X509Certificates.X509Certificate2 certificate = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", key, System.Security.Cryptography.HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        UpstreamHttp2Server server = new(
            backend => new NamedConnector("http2 over " + ((NamedConnector)backend).Name),
            hasTlsServer ? (backend, _) => new NamedConnector("tls over " + ((NamedConnector)backend).Name) : null);
        IConnector servers = server.InFrontOf(new NamedConnector("other"), new NamedConnector("sws"), hasCertificate ? certificate : null);

        ConnectResult result = await servers.ConnectAsync(new ConnectTarget("127.0.0.1", UpstreamHttp2Server.Http2TlsPort, false), CancellationToken.None);

        diagnostics.Assert("server reached", expected, result.ErrorMessage);
        Assert.AreEqual(expected, result.ErrorMessage);
    }

    [TestMethod]
    public void Http2TlsPort_IsTheRunnersHttp2TlsPort()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        string port = UpstreamHttp2Server.Http2TlsPort.ToString(System.Globalization.CultureInfo.InvariantCulture);

        diagnostics.Assert("port", UpstreamCaseRunner.Http2TlsPort, port);
        Assert.AreEqual(UpstreamCaseRunner.Http2TlsPort, port);
    }

    [TestMethod]
    public void Http2Port_IsTheRunnersHttp2Port()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        string port = UpstreamHttp2Server.Http2Port.ToString(System.Globalization.CultureInfo.InvariantCulture);

        diagnostics.Assert("port", UpstreamCaseRunner.Http2Port, port);
        Assert.AreEqual(UpstreamCaseRunner.Http2Port, port);
    }

    // Refuses every connection with its own name, so a test can tell which connector was reached.
    private sealed class NamedConnector(string name) : IConnector
    {
        public string Name => name;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Refused(name));
    }
}
