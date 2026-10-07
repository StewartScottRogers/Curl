using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="MultiplexedStreamResetException" /> and
/// <see cref="MultiplexedConnectionFailedException" />: each carries what it was built
/// with and is an <see cref="IOException" />.
/// </summary>
[TestClass]
public sealed class MultiplexedStreamExceptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void StreamReset_Constructor_CarriesTheCodeAndMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("application error code", 0x10cL);
        diagnostics.Arrange("message", "HTTP/3 stream 0 reset by server");

        var exception = new MultiplexedStreamResetException(0x10c, "HTTP/3 stream 0 reset by server");

        diagnostics.Act("application error code", exception.ApplicationErrorCode);
        diagnostics.Act("message", exception.Message);
        diagnostics.Diff("message", "HTTP/3 stream 0 reset by server", exception.Message);
        Assert.AreEqual(0x10cL, exception.ApplicationErrorCode);
        Assert.AreEqual("HTTP/3 stream 0 reset by server", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception);
    }

    [TestMethod]
    public void ConnectionFailed_Constructor_CarriesTheExitCodeAndMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.SendError);
        diagnostics.Arrange("message", "ngtcp2_conn_writev_stream returned error: ERR_CLOSING");

        var exception = new MultiplexedConnectionFailedException(CurlExitCode.SendError, "ngtcp2_conn_writev_stream returned error: ERR_CLOSING");

        diagnostics.Act("exit code", exception.ExitCode);
        diagnostics.Act("message", exception.Message);
        diagnostics.Diff("message", "ngtcp2_conn_writev_stream returned error: ERR_CLOSING", exception.Message);
        Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode);
        Assert.AreEqual("ngtcp2_conn_writev_stream returned error: ERR_CLOSING", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception);
    }

    [TestMethod]
    public void ConnectionFailed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.Ok);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new MultiplexedConnectionFailedException(CurlExitCode.Ok, "unused"));

        diagnostics.Act("param name", exception.ParamName);
        diagnostics.Assert("param name", "exitCode", exception.ParamName);
        Assert.AreEqual("exitCode", exception.ParamName);
    }
}
