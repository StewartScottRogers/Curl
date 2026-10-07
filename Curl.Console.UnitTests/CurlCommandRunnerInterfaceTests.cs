using System.Text;
using Curl.Core;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray());

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_WithAMalformedInterface_Exits43WritesTheWriteOutAndEndsTheRun()
    {
        // curl -w '[%{exitcode}]\n' --interface 'if!' http://127.0.0.1:47599/a http://127.0.0.1:47599/b
        // -> stdout [43], stderr curl: (43) setopt 0x274e got bad argument, nothing requested, exit 43.
        int exitCode = await RunAsync(["-w", "[%{exitcode}]\n", "--interface", "if!", "http://h/a", "http://h/b"]);

        string expectedStandardError = "curl: (43) setopt 0x274e got bad argument" + NewLine;
        Diagnostics.Assert("exit code", 43, exitCode);
        Diagnostics.Assert("requests made", 0, http.Contexts.Count);
        Diagnostics.Diff("stdout", "[43]\n", StandardOutputText);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Assert.AreEqual(43, exitCode);
        Assert.IsEmpty(http.Contexts);
        Assert.AreEqual("[43]\n", StandardOutputText);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithAMalformedInterfaceInALaterGroup_TransfersTheEarlierGroup()
    {
        // curl http://127.0.0.1:47599/a --next --interface 'if!' http://127.0.0.1:47599/b -> /a requested, exit 43.
        int exitCode = await RunAsync(["-s", "http://h/a", "--next", "-s", "--interface", "if!", "http://h/b"]);

        Diagnostics.Assert("exit code", 43, exitCode);
        Diagnostics.Assert("requests made", 1, http.Contexts.Count);
        Assert.AreEqual(43, exitCode);
        Assert.HasCount(1, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_WithAMalformedInterfaceAndVerbose_ReportsTheSetoptLine()
    {
        // curl -v --interface 'if!' ... -> * setopt 0x274e got bad argument, then the error line.
        int exitCode = await RunAsync(["-v", "--interface", "if!", "http://h/"]);

        string expectedStandardError = "* setopt 0x274e got bad argument\n" + "curl: (43) setopt 0x274e got bad argument" + NewLine;
        Diagnostics.Assert("exit code", 43, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Assert.AreEqual(43, exitCode);
        Assert.AreEqual(
            expectedStandardError,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithAnAcceptedInterface_Transfers()
    {
        int exitCode = await RunAsync(["-s", "--interface", "host!127.0.0.1", "http://h/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("requests made", 1, http.Contexts.Count);
        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, http.Contexts);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
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

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("requests made", http.Contexts.Count);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }
}
