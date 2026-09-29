namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="HttpAuthenticationFailedException" />: it carries the exit code and the
/// message the HTTP handler ends the transfer with.
/// </summary>
[TestClass]
public sealed class HttpAuthenticationFailedExceptionTests
{
    [TestMethod]
    public void Constructor_RoundTripsTheExitCodeAndMessage()
    {
        HttpAuthenticationFailedException exception = new(CurlExitCode.AuthError, "An authentication function returned an error");

        Assert.AreEqual(CurlExitCode.AuthError, exception.ExitCode);
        Assert.AreEqual("An authentication function returned an error", exception.Message);
    }
}
