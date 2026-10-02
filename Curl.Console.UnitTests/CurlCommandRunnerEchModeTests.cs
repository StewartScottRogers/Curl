using System.Text;
using Curl.Core;

namespace Curl.Console;

/// <summary>
/// Pins an <c>--ech</c> mode libcurl refuses when curl sets it (<c>CommandLineOptions.EchModeIsMalformed</c>):
/// curl 8.21.0's ECH build fails that transfer with exit 43 and <c>setopt 0x2855 got bad argument</c>
/// (measured 2026-10-02, BL-1107), refused as a malformed <c>--interface</c> is, so the <c>-w</c> output and
/// the <c>-v</c> line follow that measurement (ADR-0378).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerEchModeTests
{
    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray());

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_WithAnUnknownEchMode_Exits43WritesTheWriteOutAndEndsTheRun()
    {
        // curl --ech bogus https://... -> curl: (43) setopt 0x2855 got bad argument, exit 43 (BL-1107).
        int exitCode = await RunAsync(["-w", "[%{exitcode}]\n", "--ech", "bogus", "http://h/a", "http://h/b"]);

        Assert.AreEqual(43, exitCode);
        Assert.IsEmpty(http.Contexts);
        Assert.AreEqual("[43]\n", StandardOutputText);
        Assert.AreEqual("curl: (43) setopt 0x2855 got bad argument" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithAnUnknownEchModeBesideAConfigList_Exits43()
    {
        // curl --ech bogus --ech ecl:AEX+ https://... -> the same refusal (BL-1107, "with or without ecl:").
        int exitCode = await RunAsync(["--ech", "bogus", "--ech", "ecl:AEX+", "http://h/"]);

        Assert.AreEqual(43, exitCode);
        Assert.IsEmpty(http.Contexts);
        Assert.AreEqual("curl: (43) setopt 0x2855 got bad argument" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithAnUnknownEchModeAndVerbose_ReportsTheSetoptLine()
    {
        int exitCode = await RunAsync(["-v", "--ech", "bogus", "http://h/"]);

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual(
            "* setopt 0x2855 got bad argument\n" + "curl: (43) setopt 0x2855 got bad argument" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithAnUnknownEchModeInALaterGroup_TransfersTheEarlierGroup()
    {
        int exitCode = await RunAsync(["-s", "http://h/a", "--next", "-s", "--ech", "bogus", "http://h/b"]);

        Assert.AreEqual(43, exitCode);
        Assert.HasCount(1, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_WithAnAcceptedEchMode_Transfers()
    {
        int exitCode = await RunAsync(["-s", "--ech", "false", "http://h/"]);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, http.Contexts);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                outputPaths: fileSystem)
            .RunAsync(arguments);
}
