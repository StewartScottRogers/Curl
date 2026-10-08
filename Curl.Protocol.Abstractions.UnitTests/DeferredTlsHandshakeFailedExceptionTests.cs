using Curl.Testing;

namespace Curl.Protocol.Abstractions;

[TestClass]
public sealed class DeferredTlsHandshakeFailedExceptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_KeepsTheExitCodeAndMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.SslConnectError);
        diagnostics.Arrange("message", "handshake failed");

        var exception = new DeferredTlsHandshakeFailedException(CurlExitCode.SslConnectError, "handshake failed");

        diagnostics.Act("exit code", exception.ExitCode);
        diagnostics.Act("message", exception.Message);
        diagnostics.Assert("exit code", CurlExitCode.SslConnectError, exception.ExitCode);
        diagnostics.Diff("message", "handshake failed", exception.Message);
        Assert.AreEqual(CurlExitCode.SslConnectError, exception.ExitCode);
        Assert.AreEqual("handshake failed", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception);
    }

    [TestMethod]
    public void Constructor_WithOk_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.Ok);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DeferredTlsHandshakeFailedException(CurlExitCode.Ok, "fine"));

        diagnostics.Act("exception message", exception.Message);
        diagnostics.Assert("exception type", typeof(ArgumentOutOfRangeException), exception.GetType());
    }
}
