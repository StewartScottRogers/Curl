using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins that <see cref="UpstreamHttp3Server.InFrontOf"/> sends datagram opens to
/// <see cref="UpstreamHttp3Server.Http3Port"/> to a fresh server over the given backend and certificate, and every other port on.
/// </summary>
[TestClass]
public sealed class UpstreamHttp3ServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(UpstreamHttp3Server.Http3Port, "http3 over sws with CN=localhost")]
    [DataRow(9003, "other")]
    public async Task InFrontOf_OpenToPort_ReachesTheServerForThatPort(int port, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("port", port);
        using ECDsa key = ECDsa.Create();
        using X509Certificate2 certificate = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        UpstreamHttp3Server server = new((backend, presented) => new NamedDatagramConnector($"http3 over {((NamedConnector)backend).Name} with {presented.Subject}"));
        IDatagramConnector servers = server.InFrontOf(new NamedDatagramConnector("other"), new NamedConnector("sws"), certificate);

        DatagramOpenResult result = await servers.OpenAsync("127.0.0.1", port, CancellationToken.None);

        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("server reached", expected, result.ErrorMessage);
        Assert.AreEqual(expected, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(UpstreamHttp3Server.Http3Port, "Failed to connect to 127.0.0.1:9019 after 0 ms: Could not connect to server")]
    [DataRow(8990, "other")]
    public async Task RefusingTcpOnHttp3Port_ConnectToPort_RefusesOnlyTheHttp3Port(int port, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("port", port);
        IConnector servers = UpstreamHttp3Server.RefusingTcpOnHttp3Port(new NamedConnector("other"));

        ConnectResult result = await servers.ConnectAsync(new ConnectTarget("127.0.0.1", port, false), CancellationToken.None);

        diagnostics.Assert("error message", expected, result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.AreEqual(expected, result.ErrorMessage);
    }

    [TestMethod]
    public void Http3Port_IsTheRunnersHttp3Port()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        string port = UpstreamHttp3Server.Http3Port.ToString(System.Globalization.CultureInfo.InvariantCulture);

        diagnostics.Assert("port", UpstreamCaseRunner.Http3Port, port);
        Assert.AreEqual(UpstreamCaseRunner.Http3Port, port);
    }

    // Refuses every connection with its own name, which the server under test reads.
    private sealed class NamedConnector(string name) : IConnector
    {
        public string Name => name;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Refused(name));
    }

    // Refuses every datagram open with its own name, so a test can tell which connector was reached.
    private sealed class NamedDatagramConnector(string name) : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, name));
    }
}
