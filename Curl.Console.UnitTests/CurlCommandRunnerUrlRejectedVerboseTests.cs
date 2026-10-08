using System.Text;
using Curl.Core;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-v</c> info line curl 8.21.0's <c>failf</c> writes for a transfer URL its parser
/// rejects, <c>* URL rejected: &lt;reason&gt;</c>, before the <c>curl: (3)</c> (or <c>(67)</c>) line
/// (measured with curl 8.21.0 on Windows 2026-10-03, BL-1332).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerUrlRejectedVerboseTests
{
    private const string MalformedLine = "URL rejected: Malformed input to a URL function";

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
    public async Task RunAsync_WithVerboseAndAMalformedUrl_WritesTheInfoLineThenTheErrorLine()
    {
        // curl -v "http://h/a b" -> * URL rejected: Malformed input to a URL function, then curl: (3) ..., exit 3.
        int exitCode = await RunAsync(["-v", "http://h/a b"]);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Assert("handler call count", 0, http.Contexts.Count);
        Assert.IsEmpty(http.Contexts);
        Diagnostics.Diff("stderr", "* " + MalformedLine + "\n" + "curl: (3) " + MalformedLine + "\n", Normalized(StandardErrorText));
        Assert.AreEqual("* " + MalformedLine + "\n" + "curl: (3) " + MalformedLine + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithSilentVerboseAndAMalformedUrl_WritesOnlyTheInfoLine()
    {
        // curl -sv "http://h/a b" -> only * URL rejected: Malformed input to a URL function, exit 3.
        int exitCode = await RunAsync(["-sv", "http://h/a b"]);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Diff("stderr", "* " + MalformedLine + "\n", Normalized(StandardErrorText));
        Assert.AreEqual("* " + MalformedLine + "\n", StandardErrorText);
    }

    [TestMethod]
    [DataRow(new[] { "-v", "http://127.0.0.1:99999/" }, 3, "Port number was not a decimal number between 0 and 65535", DisplayName = "bad port")]
    [DataRow(new[] { "-v", "file://host/x" }, 3, "Bad file:// URL", DisplayName = "file URL with a host")]
    [DataRow(new[] { "-v", "--disallow-username-in-url", "http://u@127.0.0.1/" }, 67, "Credentials was passed in the URL when prohibited", DisplayName = "user in the URL when prohibited")]
    public async Task RunAsync_WithVerboseAndARejectedUrl_WritesTheReasonAsAnInfoLine(string[] arguments, int expectedExitCode, string reason)
    {
        Diagnostics.Arrange("expected rejection reason", reason);

        int exitCode = await RunAsync(arguments);

        Diagnostics.Assert("exit code", expectedExitCode, exitCode);
        Assert.AreEqual(expectedExitCode, exitCode);
        Diagnostics.Diff(
            "stderr",
            "* URL rejected: " + reason + "\n" + $"curl: ({expectedExitCode}) URL rejected: " + reason + "\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "* URL rejected: " + reason + "\n" + $"curl: ({expectedExitCode}) URL rejected: " + reason + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithTraceAsciiToStandardOutputAndAMalformedUrl_TracesTheInfoLine()
    {
        // curl --trace-ascii - "http://h/a b" -> stdout the 50 bytes below, stderr curl: (3) ..., exit 3.
        int exitCode = await RunAsync(["--trace-ascii", "-", "http://h/a b"]);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Diff("stdout", "* " + MalformedLine + "\n", Normalized(StandardOutputText));
        Assert.AreEqual("* " + MalformedLine + "\n", StandardOutputText);
        Diagnostics.Diff("stderr", "curl: (3) " + MalformedLine + "\n", Normalized(StandardErrorText));
        Assert.AreEqual("curl: (3) " + MalformedLine + NewLine, StandardErrorText);
    }

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("handler behaviour", "http writes the URL path and succeeds");

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
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Act("stderr", Normalized(StandardErrorText));
        return exitCode;
    }
}
