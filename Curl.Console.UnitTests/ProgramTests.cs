using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the entry point: it refuses a null argument array and delegates to the runner.
/// </summary>
[TestClass]
public sealed class ProgramTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Main_NullArguments_Throws()
    {
        Diagnostics.Arrange("arguments", "null");

        Diagnostics.Act("Main", "called with a null argument array");
        Diagnostics.Assert("exception", nameof(ArgumentNullException), "checked by Assert.ThrowsExactlyAsync");
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Program.Main(null!));
    }

    [TestMethod]
    public async Task Main_RefusedCommandLine_ReturnsTheRefusalsExitCode()
    {
        Diagnostics.Arrange("command line", "curl --no-such-option file:///a");

        int exitCode = await Program.Main(["--no-such-option", "file:///a"]);
        Diagnostics.Act("exit code", exitCode);

        Diagnostics.Assert("exit code", (int)CurlExitCode.FailedInit, exitCode);
        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
    }
}
