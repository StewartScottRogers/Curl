namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="MultiplexedStreamResetException" /> and
/// <see cref="MultiplexedConnectionFailedException" />: each carries what it was built
/// with and is an <see cref="IOException" />.
/// </summary>
[TestClass]
public sealed class MultiplexedStreamExceptionTests
{
    [TestMethod]
    public void StreamReset_Constructor_CarriesTheCodeAndMessage()
    {
        var exception = new MultiplexedStreamResetException(0x10c, "HTTP/3 stream 0 reset by server");

        Assert.AreEqual(0x10cL, exception.ApplicationErrorCode);
        Assert.AreEqual("HTTP/3 stream 0 reset by server", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception);
    }

    [TestMethod]
    public void ConnectionFailed_Constructor_CarriesTheExitCodeAndMessage()
    {
        var exception = new MultiplexedConnectionFailedException(CurlExitCode.SendError, "ngtcp2_conn_writev_stream returned error: ERR_CLOSING");

        Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode);
        Assert.AreEqual("ngtcp2_conn_writev_stream returned error: ERR_CLOSING", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception);
    }

    [TestMethod]
    public void ConnectionFailed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new MultiplexedConnectionFailedException(CurlExitCode.Ok, "unused"));

        Assert.AreEqual("exitCode", exception.ParamName);
    }
}
