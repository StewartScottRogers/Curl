using Curl.Conformance.Extended.Harness;
using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.UnitTests.Harness;

/// <summary>
/// Pins that <see cref="ExtendedConformanceHarness" />'s overload with a process starter hands it to curl, so a case
/// runs an <c>mcp+stdio://</c> server on a stand-in and starts no real process (BL-4577, ADR-1184).
/// </summary>
[TestClass]
public sealed class ExtendedConformanceHarnessProcessTests
{
    private static readonly InProcessCurlProcessRunner Curl = InProcessCurl.RunAsync;

    [TestMethod]
    public async Task RunAsync_WithAProcessStarter_StartsTheMcpServerOnTheStandIn()
    {
        StandInProcessStarter starter = new();

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [], ["-sS", "mcp+stdio://localhost/server?a"], starter);

        Assert.AreEqual("server", starter.StartedProgram);
        CollectionAssert.AreEqual(new[] { "a" }, starter.StartedArguments?.ToArray());
        Assert.AreNotEqual(0, result.ExitCode);
        Assert.AreEqual(0, result.Exchanges.Count);
    }

    [TestMethod]
    public void RunAsync_NullProcessStarter_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtendedConformanceHarness.RunAsync(Curl, [], ["-s"], null!));

    /// <summary>Records the program it is asked to start and refuses to start it, as a missing program is refused.</summary>
    private sealed class StandInProcessStarter : IExtensionProcessStarter
    {
        public string? StartedProgram { get; private set; }

        public IReadOnlyList<string>? StartedArguments { get; private set; }

        public string? ReadEnvironmentVariable(string name) => null;

        public IExtensionProcess Start(string program, IReadOnlyList<string> arguments)
        {
            StartedProgram = program;
            StartedArguments = arguments;
            throw new IOException("The system cannot find the file specified.");
        }
    }
}
