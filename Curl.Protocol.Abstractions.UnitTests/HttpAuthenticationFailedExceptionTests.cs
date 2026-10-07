using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="HttpAuthenticationFailedException" />: it carries the exit code and the
/// message the HTTP handler ends the transfer with.
/// </summary>
[TestClass]
public sealed class HttpAuthenticationFailedExceptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_RoundTripsTheExitCodeAndMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.AuthError);
        diagnostics.Arrange("message", "An authentication function returned an error");

        HttpAuthenticationFailedException exception = new(CurlExitCode.AuthError, "An authentication function returned an error");

        diagnostics.Act("exit code", exception.ExitCode);
        diagnostics.Act("message", exception.Message);
        diagnostics.Assert("exit code", CurlExitCode.AuthError, exception.ExitCode);
        diagnostics.Diff("message", "An authentication function returned an error", exception.Message);
        Assert.AreEqual(CurlExitCode.AuthError, exception.ExitCode);
        Assert.AreEqual("An authentication function returned an error", exception.Message);
    }
}
