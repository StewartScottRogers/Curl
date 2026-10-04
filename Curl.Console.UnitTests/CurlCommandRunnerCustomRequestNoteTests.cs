using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins curl 8.21.0's <c>customrequest_helper</c> lines before each transfer whose <c>-X</c> method is
/// given, measured with curl 8.21.0 (mingw, Schannel) and <c>Record-CurlExchange.ps1</c> on
/// 2026-10-03 (BL-1390): under <c>-v</c> the <c>Unnecessary use of -X</c> note when the method
/// repeats the inferred one, ignoring case; otherwise, for <c>HEAD</c> in any case and without
/// <c>-s</c>, the two wrapped warning lines. No test opens a socket.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerCustomRequestNoteTests
{
    private const string Url = "http://127.0.0.1:1/a";

    private const string HeadWarningFirstLine = "Warning: Setting custom HTTP method to HEAD with -X/--request may not work the ";

    private const string HeadWarningSecondLine = "Warning: way you want. Consider using -I/--head instead.";

    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();

    private readonly RecordingProtocolHandler http = new("http", context =>
    {
        context.Events.ReportInfo("Connection #0 to host 127.0.0.1:1 left intact");
        return ValueTask.FromResult(TransferResult.Success(0));
    });

    private readonly RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

    private string[] StandardErrorLines =>
        Encoding.UTF8.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static string Note(string method) => $"Note: Unnecessary use of -X or --request, {method} is already inferred.";

    [TestMethod]
    [DataRow(new[] { "-v", "-X", "GET" }, "GET")]
    [DataRow(new[] { "-v", "-X", "get" }, "GET")]
    [DataRow(new[] { "-v", "-X", "post", "-d", "x" }, "POST")]
    [DataRow(new[] { "-v", "-X", "POST", "-F", "a=b" }, "POST")]
    [DataRow(new[] { "-v", "-X", "HEAD", "-I" }, "HEAD")]
    [DataRow(new[] { "-v", "-X", "GET", "-d", "x", "-G" }, "GET")]
    [DataRow(new[] { "-v", "-s", "-X", "GET" }, "GET")]
    public async Task RunAsync_VerboseMethodRepeatsTheInferredOne_WritesTheNoteFirst(string[] options, string method)
    {
        await RunAsync([.. options, "-o", "out", Url]);

        Assert.AreEqual(Note(method), StandardErrorLines[0]);
    }

    [TestMethod]
    public async Task RunAsync_VerbosePutRepeatsTheUploadMethod_WritesThePutNote()
    {
        fileSystem.ExistingContent["up.txt"] = Encoding.ASCII.GetBytes("abc");

        await RunAsync(["-v", "-X", "PUT", "-T", "up.txt", "-o", "out", Url]);

        Assert.AreEqual(Note("PUT"), StandardErrorLines[0]);
    }

    [TestMethod]
    public async Task RunAsync_VerboseGetOnAnFtpUrl_WritesTheGetNote()
    {
        await RunAsync(["-v", "-X", "GET", "-o", "out", "ftp://127.0.0.1:1/a"]);

        Assert.AreEqual(Note("GET"), StandardErrorLines[0]);
        Assert.HasCount(1, ftp.Contexts);
    }

    [TestMethod]
    [DataRow(new[] { "-X", "POST", "-d", "x" })]
    [DataRow(new[] { "-X", "GET", "-I" })]
    [DataRow(new[] { "-X", "get" })]
    [DataRow(new[] { "-X", "POST" })]
    [DataRow(new[] { "-X", "GET", "-d", "x", "-G" })]
    [DataRow(new[] { "-v", "-X", "GET", "-d", "x" })]
    [DataRow(new[] { "-v", "-X", "POST" })]
    public async Task RunAsync_NoVerboseOrMethodNotInferred_WritesNoNoteOrWarning(string[] options)
    {
        await RunAsync([.. options, "--no-progress-meter", "-o", "out", Url]);

        Assert.IsFalse(StandardErrorLines.Any(line => line.StartsWith("Note: Unnecessary", StringComparison.Ordinal)));
        Assert.IsFalse(StandardErrorLines.Any(line => line.StartsWith(HeadWarningFirstLine, StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("HEAD")]
    [DataRow("head")]
    public async Task RunAsync_HeadMethodWithoutSilent_WritesTheTwoWrappedWarningLines(string method)
    {
        await RunAsync(["--no-progress-meter", "-X", method, "-o", "out", Url]);

        CollectionAssert.AreEqual(new[] { HeadWarningFirstLine, HeadWarningSecondLine, string.Empty }, StandardErrorLines);
    }

    [TestMethod]
    [DataRow("HEAD")]
    [DataRow("head")]
    public async Task RunAsync_HeadMethodWithSilent_WritesNothing(string method)
    {
        await RunAsync(["-s", "-X", method, "-o", "out", Url]);

        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_VerboseGetForTwoUrls_WritesTheNoteAgainAfterTheFirstTransfersLeftIntactLine()
    {
        await RunAsync(["-v", "-s", "-X", "GET", "-o", "one", Url, "-o", "two", "http://127.0.0.1:1/b"]);

        string[] lines = StandardErrorLines;
        int leftIntact = Array.IndexOf(lines, "* Connection #0 to host 127.0.0.1:1 left intact");
        Assert.AreEqual(Note("GET"), lines[0]);
        Assert.IsGreaterThan(0, leftIntact);
        Assert.AreEqual(Note("GET"), lines[leftIntact + 1]);
    }

    [TestMethod]
    public async Task RunAsync_HeadForTwoUrls_WritesTheWarningBeforeEachTransfer()
    {
        await RunAsync(["--no-progress-meter", "-X", "HEAD", "-o", "one", Url, "-o", "two", "http://127.0.0.1:1/b"]);

        CollectionAssert.AreEqual(
            new[] { HeadWarningFirstLine, HeadWarningSecondLine, HeadWarningFirstLine, HeadWarningSecondLine, string.Empty },
            StandardErrorLines);
        Assert.HasCount(2, http.Contexts);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http, ftp]), []),
                fileSystem,
                fileSystem,
                new MemoryStream(),
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                TerminalColumns.Default)
            .RunAsync(arguments);
}
