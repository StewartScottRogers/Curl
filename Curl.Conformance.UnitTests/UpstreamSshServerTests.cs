using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins that <see cref="UpstreamSshServer.InFrontOf"/> sends connections to
/// <see cref="UpstreamSshServer.SshPort"/> to a fresh SSH server and every other port on.
/// </summary>
[TestClass]
public sealed class UpstreamSshServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(UpstreamSshServer.SshPort, "ssh")]
    [DataRow(8990, "other")]
    public async Task InFrontOf_ConnectToPort_ReachesTheServerForThatPort(int port, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("port", port);
        UpstreamSshServer server = new(() => new NamedConnector("ssh"), "user", ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, "md5", "sha256");
        IConnector servers = server.InFrontOf(new NamedConnector("other"));

        ConnectResult result = await servers.ConnectAsync(new ConnectTarget("127.0.0.1", port, false), CancellationToken.None);

        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("server reached", expected, result.ErrorMessage);
        Assert.AreEqual(expected, result.ErrorMessage);
    }

    // Refuses every connection with its own name, so a test can tell which connector was reached.
    private sealed class NamedConnector(string name) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Refused(name));
    }
}
