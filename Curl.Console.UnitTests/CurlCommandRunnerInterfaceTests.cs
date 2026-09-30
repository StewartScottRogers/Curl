using System.Text;
using Curl.Core;

namespace Curl.Console;

/// <summary>
/// Pins a <c>--interface</c> value libcurl refuses when curl sets it (BL-599's
/// <c>InterfaceBinding.IsMalformed</c>): curl 8.21.0 fails that transfer with exit 43 and
/// <c>setopt 0x274e got bad argument</c>, still writes its <c>-w</c> output, and transfers nothing
/// after it (measured on Windows 2026-09-29, BL-600 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerInterfaceTests
{
    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray());

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_WithAMalformedInterface_Exits43WritesTheWriteOutAndEndsTheRun()
    {
        // curl -w '[%{exitcode}]\n' --interface 'if!' http://127.0.0.1:47599/a http://127.0.0.1:47599/b
        // -> stdout [43], stderr curl: (43) setopt 0x274e got bad argument, nothing requested, exit 43.
        int exitCode = await RunAsync(["-w", "[%{exitcode}]\\n", "--interface", "if!", "http://h/a", "http://h/b"]);

        Assert.AreEqual(43, exitCode);
        Assert.IsEmpty(http.Contexts);
        Assert.AreEqual("[43]\n", StandardOutputText);
        Assert.AreEqual("curl: (43) setopt 0x274e got bad argument" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithAMalformedInterfaceInALaterGroup_TransfersTheEarlierGroup()
    {
        // curl http://127.0.0.1:47599/a --next --interface 'if!' http://127.0.0.1:47599/b -> /a requested, exit 43.
        int exitCode = await RunAsync(["-s", "http://h/a", "--next", "-s", "--interface", "if!", "http://h/b"]);

        Assert.AreEqual(43, exitCode);
        Assert.HasCount(1, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_WithAMalformedInterfaceAndVerbose_ReportsTheSetoptLine()
    {
        // curl -v --interface 'if!' ... -> * setopt 0x274e got bad argument, then the error line.
        int exitCode = await RunAsync(["-v", "--interface", "if!", "http://h/"]);

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual(
            "* setopt 0x274e got bad argument\n" + "curl: (43) setopt 0x274e got bad argument" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithAnAcceptedInterface_Transfers()
    {
        int exitCode = await RunAsync(["-s", "--interface", "host!127.0.0.1", "http://h/"]);

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
