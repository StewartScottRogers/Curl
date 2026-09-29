namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="SaslAuthenticationFailedException" />: it carries the exit code and the
/// message the mail handlers end the transfer with.
/// </summary>
[TestClass]
public sealed class SaslAuthenticationFailedExceptionTests
{
    [TestMethod]
    public void Constructor_RoundTripsTheExitCodeAndMessage()
    {
        SaslAuthenticationFailedException exception = new(CurlExitCode.AuthError, "An authentication function returned an error");

        Assert.AreEqual(CurlExitCode.AuthError, exception.ExitCode);
        Assert.AreEqual("An authentication function returned an error", exception.Message);
    }
}
