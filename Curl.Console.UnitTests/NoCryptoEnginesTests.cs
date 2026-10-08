using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>--engine</c> and <c>--dump-ca-embed</c> end to end as ADR-0151 gives them: <c>--engine list</c>
/// prints no build-time engines and <c>--dump-ca-embed</c> prints nothing, each exiting 0 without a
/// transfer on every platform; a named engine is ignored on Windows and fails the transfer before it
/// connects elsewhere, with the OpenSSL build's exit code and text.
/// </summary>
[TestClass]
public sealed class NoCryptoEnginesTests
{
    private const string Url = "file:///dir/x";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly MemoryStream standardInput = new();
    private readonly InMemoryFileSystem fileSystem = new();

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_EngineList_PrintsNoEnginesAndTransfersNothing(bool runsOnWindows)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");
        Diagnostics.Arrange("arguments", "-sS --engine list " + Url);
        Diagnostics.Arrange("runsOnWindows", runsOnWindows);

        int exitCode = await Runner(file, runsOnWindows).RunAsync(["-sS", "--engine", "list", Url]);
        string output = Encoding.UTF8.GetString(standardOutput.ToArray());
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("standard output", output.Replace("\r", string.Empty));
        Diagnostics.Act("standard error length", standardError.Length);
        Diagnostics.Act("transfer count", file.Contexts.Count);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert(
            "standard output",
            "Build-time engines:\n  <none>\n",
            output.Replace("\r", string.Empty));
        Assert.AreEqual(
            "Build-time engines:" + Environment.NewLine + "  <none>" + Environment.NewLine,
            Encoding.UTF8.GetString(standardOutput.ToArray()));
        Diagnostics.Assert("standard error length", 0L, standardError.Length);
        Assert.AreEqual(0, standardError.Length);
        Diagnostics.Assert("transfer count", 0, file.Contexts.Count);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_DumpCaEmbed_PrintsNothingAndTransfersNothing(bool runsOnWindows)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        Diagnostics.Arrange("arguments", "--dump-ca-embed " + Url);
        Diagnostics.Arrange("runsOnWindows", runsOnWindows);

        int exitCode = await Runner(file, runsOnWindows).RunAsync(["--dump-ca-embed", Url]);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("standard output length", standardOutput.Length);
        Diagnostics.Act("standard error length", standardError.Length);
        Diagnostics.Act("transfer count", file.Contexts.Count);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("standard output length", 0L, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
        Diagnostics.Assert("standard error length", 0L, standardError.Length);
        Assert.AreEqual(0, standardError.Length);
        Diagnostics.Assert("transfer count", 0, file.Contexts.Count);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("dynamic")]
    public async Task RunAsync_NamedEngineOnWindows_IsIgnored(string engine)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        Diagnostics.Arrange("engine", engine);
        Diagnostics.Arrange("runsOnWindows", true);

        int exitCode = await Runner(file, runsOnWindows: true).RunAsync(["-sS", "--engine", engine, Url]);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("transfer count", file.Contexts.Count);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("transfer count", 1, file.Contexts.Count);
        Assert.HasCount(1, file.Contexts);
    }

    [TestMethod]
    [DataRow("bogus", CurlExitCode.SslEngineNotFound, NoCryptoEngines.EngineNotFoundMessage)]
    [DataRow("dynamic", CurlExitCode.SslEngineInitFailed, NoCryptoEngines.DynamicEngineFailedMessage)]
    public async Task RunAsync_NamedEngineOffWindows_FailsBeforeConnecting(string engine, CurlExitCode expected, string message)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        Diagnostics.Arrange("engine", engine);
        Diagnostics.Arrange("expected exit code", expected);
        Diagnostics.Arrange("message", message);

        int exitCode = await Runner(file, runsOnWindows: false).RunAsync(["-sS", "--engine", engine, Url]);
        string errorText = Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("standard error", errorText.Replace("\r", string.Empty));
        Diagnostics.Act("transfer count", file.Contexts.Count);

        Diagnostics.Assert("exit code", (int)expected, exitCode);
        Assert.AreEqual((int)expected, exitCode);
        Diagnostics.Assert("standard error", $"curl: ({(int)expected}) {message}\n", errorText.Replace("\r", string.Empty));
        Assert.AreEqual($"curl: ({(int)expected}) {message}{Environment.NewLine}", Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.Assert("transfer count", 0, file.Contexts.Count);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public void LoadFailure_NoEngine_IsNone()
    {
        Diagnostics.Arrange("engine", null);
        Diagnostics.Arrange("runsOnWindows", false);

        object? failure = NoCryptoEngines.LoadFailure(null, runsOnWindows: false);
        Diagnostics.Act("failure is null", failure is null);

        Diagnostics.Assert("failure", null, failure);
        Assert.IsNull(NoCryptoEngines.LoadFailure(null, runsOnWindows: false));
    }

    private CurlCommandRunner Runner(RecordingProtocolHandler handler, bool runsOnWindows) =>
        new(_ => new TransferDispatch(new ProtocolDispatcher([handler])), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows);
}
