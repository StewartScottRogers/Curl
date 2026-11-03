using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that <see cref="InProcessCurl" />'s overload with an <see cref="IExtensionProcessStarter" /> hands it to the
/// extension handlers, so an <c>mcp+stdio://</c> run starts its server on the stand-in and no real process (BL-4577, ADR-1184).
/// </summary>
[TestClass]
public sealed class InProcessCurlExtensionProcessTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_McpStdioUrlWithAProcessStarter_StartsTheServerOnTheStandIn()
    {
        StandInProcessStarter starter = new();
        ScriptedConnector server = new([]);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        Diagnostics.Arrange("url", "mcp+stdio://localhost/server?a");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await InProcessCurl.RunAsync(["-sS", "mcp+stdio://localhost/server?a"], standardOutput, standardError, new MemoryStream(), new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), _ => null, TimeProvider.System, starter);
        }

        Diagnostics.Act("exit code", exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Diagnostics.Act("standard error", Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual("server", starter.StartedProgram);
        CollectionAssert.AreEqual(new[] { "a" }, starter.StartedArguments?.ToArray());
        Assert.AreNotEqual(0, exitCode);
    }

    [TestMethod]
    public void RunAsync_NoProcessStarter_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync(["-s", "mcp+stdio://localhost/server"], new MemoryStream(), new MemoryStream(), new MemoryStream(), new ScriptedTcpDialer(new ScriptedConnector([])), new LoopbackDnsResolver(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), _ => null, TimeProvider.System, null!));

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
