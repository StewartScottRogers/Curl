using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="UnreachableDatagramConnector"/> fails every open without a socket.</summary>
[TestClass]
public sealed class UnreachableDatagramConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OpenAsync_FailsWithCouldntConnect()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("host and port", "127.0.0.1:69");

        DatagramOpenResult result = await new UnreachableDatagramConnector().OpenAsync("127.0.0.1", 69, CancellationToken.None);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        diagnostics.Assert("error message", UnreachableDatagramConnector.Message, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(UnreachableDatagramConnector.Message, result.ErrorMessage);
    }
}
