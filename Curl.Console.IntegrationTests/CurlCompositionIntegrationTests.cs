using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the composition root's <c>-w %output{}</c> opens its file on the real disk, in
/// a temporary directory removed afterwards. The socket-free and disk-free composition cases
/// stay in <c>CurlCompositionTests</c>.
/// </summary>
[TestClass]
public sealed class CurlCompositionIntegrationTests
{
    private const string ConnectFailure = "Failed to connect to h:2628 after 0 ms: Could not connect to server";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CreateRunner_WriteOutOutputFile_OpensItOnDisk()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"curl-bl280-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.bin");
        string target = Path.Combine(directory, "w.txt");
        await System.IO.File.WriteAllBytesAsync(source, [1]);
        Diagnostics.Arrange("arguments", "<source.bin url> -w %output{<directory>/w.txt}F\\n");

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: true)
                .RunAsync([new Uri(source).AbsoluteUri, "-w", $"%output{{{target}}}F\\n"]);

            Diagnostics.Act("exit code", exitCode);
            Diagnostics.Assert("exit code", 0, exitCode);
            Assert.AreEqual(0, exitCode);
            string expected = OperatingSystem.IsWindows() ? "F\r\n" : "F\n";
            string written = await System.IO.File.ReadAllTextAsync(target);
            Diagnostics.Act("w.txt content length", written.Length);
            Diagnostics.Assert("w.txt content", expected.Replace("\r", "\\r").Replace("\n", "\\n"), written.Replace("\r", "\\r").Replace("\n", "\\n"));
            Assert.AreEqual(expected, written);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CreateRunnerOverConnectors_WriteOutFileOpenerGiven_OpensTheOutputFileOnDisk()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"curl-bl1445-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.bin");
        string target = Path.Combine(directory, "w.txt");
        await System.IO.File.WriteAllBytesAsync(source, [1]);
        Diagnostics.Arrange("arguments", "<source.bin url> -w %output{<directory>/w.txt}F\\n");

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(
                    standardOutput,
                    standardError,
                    standardInput,
                    new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
                    new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
                    writeOutFileOpener: new DiskWriteOutFileOpener(writesLineFeedAsCrLf: false))
                .RunAsync([new Uri(source).AbsoluteUri, "-w", $"%output{{{target}}}F\\n"]);

            string written = await System.IO.File.ReadAllTextAsync(target);
            Diagnostics.Act("exit code", exitCode);
            Diagnostics.Act("w.txt content length", written.Length);
            Diagnostics.Assert("exit code", 0, exitCode);
            Diagnostics.Assert("w.txt content", "F\\n", written.Replace("\r", "\\r").Replace("\n", "\\n"));
            Assert.AreEqual(0, exitCode);
            Assert.AreEqual("F\n", written);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
