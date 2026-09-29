using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

        int exitCode = await Runner(file, runsOnWindows).RunAsync(["-sS", "--engine", "list", Url]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "Build-time engines:" + Environment.NewLine + "  <none>" + Environment.NewLine,
            Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_DumpCaEmbed_PrintsNothingAndTransfersNothing(bool runsOnWindows)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await Runner(file, runsOnWindows).RunAsync(["--dump-ca-embed", Url]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual(0, standardError.Length);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("dynamic")]
    public async Task RunAsync_NamedEngineOnWindows_IsIgnored(string engine)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await Runner(file, runsOnWindows: true).RunAsync(["-sS", "--engine", engine, Url]);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, file.Contexts);
    }

    [TestMethod]
    [DataRow("bogus", CurlExitCode.SslEngineNotFound, NoCryptoEngines.EngineNotFoundMessage)]
    [DataRow("dynamic", CurlExitCode.SslEngineInitFailed, NoCryptoEngines.DynamicEngineFailedMessage)]
    public async Task RunAsync_NamedEngineOffWindows_FailsBeforeConnecting(string engine, CurlExitCode expected, string message)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await Runner(file, runsOnWindows: false).RunAsync(["-sS", "--engine", engine, Url]);

        Assert.AreEqual((int)expected, exitCode);
        Assert.AreEqual($"curl: ({(int)expected}) {message}{Environment.NewLine}", Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public void LoadFailure_NoEngine_IsNone()
    {
        Assert.IsNull(NoCryptoEngines.LoadFailure(null, runsOnWindows: false));
    }

    private CurlCommandRunner Runner(RecordingProtocolHandler handler, bool runsOnWindows) =>
        new(_ => new TransferDispatch(new ProtocolDispatcher([handler])), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows);
}
