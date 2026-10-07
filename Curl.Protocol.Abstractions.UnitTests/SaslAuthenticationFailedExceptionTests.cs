using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="SaslAuthenticationFailedException" />: it carries the exit code and the
/// message the mail handlers end the transfer with.
/// </summary>
[TestClass]
public sealed class SaslAuthenticationFailedExceptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_RoundTripsTheExitCodeAndMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.AuthError);
        diagnostics.Arrange("message", "An authentication function returned an error");

        SaslAuthenticationFailedException exception = new(CurlExitCode.AuthError, "An authentication function returned an error");

        diagnostics.Act("exit code", exception.ExitCode);
        diagnostics.Act("message", exception.Message);
        diagnostics.Assert("exit code", CurlExitCode.AuthError, exception.ExitCode);
        diagnostics.Diff("message", "An authentication function returned an error", exception.Message);
        Assert.AreEqual(CurlExitCode.AuthError, exception.ExitCode);
        Assert.AreEqual("An authentication function returned an error", exception.Message);
    }
}
