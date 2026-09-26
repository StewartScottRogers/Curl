using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the entry point: it refuses a null argument array and delegates to the runner.
/// </summary>
[TestClass]
public sealed class ProgramTests
{
    [TestMethod]
    public async Task Main_NullArguments_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Program.Main(null!));
    }

    [TestMethod]
    public async Task Main_RefusedCommandLine_ReturnsTheRefusalsExitCode()
    {
        int exitCode = await Program.Main(["--no-such-option", "file:///a"]);

        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
    }
}
