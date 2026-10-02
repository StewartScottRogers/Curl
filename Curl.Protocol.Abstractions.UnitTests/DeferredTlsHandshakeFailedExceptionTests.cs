namespace Curl.Protocol.Abstractions;

[TestClass]
public sealed class DeferredTlsHandshakeFailedExceptionTests
{
    [TestMethod]
    public void Constructor_KeepsTheExitCodeAndMessage()
    {
        var exception = new DeferredTlsHandshakeFailedException(CurlExitCode.SslConnectError, "handshake failed");

        Assert.AreEqual(CurlExitCode.SslConnectError, exception.ExitCode);
        Assert.AreEqual("handshake failed", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception);
    }

    [TestMethod]
    public void Constructor_WithOk_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DeferredTlsHandshakeFailedException(CurlExitCode.Ok, "fine"));
}
