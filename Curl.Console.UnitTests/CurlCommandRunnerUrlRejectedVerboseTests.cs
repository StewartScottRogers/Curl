using System.Text;
using Curl.Core;

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

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray());

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_WithVerboseAndAMalformedUrl_WritesTheInfoLineThenTheErrorLine()
    {
        // curl -v "http://h/a b" -> * URL rejected: Malformed input to a URL function, then curl: (3) ..., exit 3.
        int exitCode = await RunAsync(["-v", "http://h/a b"]);

        Assert.AreEqual(3, exitCode);
        Assert.IsEmpty(http.Contexts);
        Assert.AreEqual("* " + MalformedLine + "\n" + "curl: (3) " + MalformedLine + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithSilentVerboseAndAMalformedUrl_WritesOnlyTheInfoLine()
    {
        // curl -sv "http://h/a b" -> only * URL rejected: Malformed input to a URL function, exit 3.
        int exitCode = await RunAsync(["-sv", "http://h/a b"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("* " + MalformedLine + "\n", StandardErrorText);
    }

    [TestMethod]
    [DataRow(new[] { "-v", "http://127.0.0.1:99999/" }, 3, "Port number was not a decimal number between 0 and 65535", DisplayName = "bad port")]
    [DataRow(new[] { "-v", "file://host/x" }, 3, "Bad file:// URL", DisplayName = "file URL with a host")]
    [DataRow(new[] { "-v", "--disallow-username-in-url", "http://u@127.0.0.1/" }, 67, "Credentials was passed in the URL when prohibited", DisplayName = "user in the URL when prohibited")]
    public async Task RunAsync_WithVerboseAndARejectedUrl_WritesTheReasonAsAnInfoLine(string[] arguments, int expectedExitCode, string reason)
    {
        int exitCode = await RunAsync(arguments);

        Assert.AreEqual(expectedExitCode, exitCode);
        Assert.AreEqual(
            "* URL rejected: " + reason + "\n" + $"curl: ({expectedExitCode}) URL rejected: " + reason + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithTraceAsciiToStandardOutputAndAMalformedUrl_TracesTheInfoLine()
    {
        // curl --trace-ascii - "http://h/a b" -> stdout the 50 bytes below, stderr curl: (3) ..., exit 3.
        int exitCode = await RunAsync(["--trace-ascii", "-", "http://h/a b"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("* " + MalformedLine + "\n", StandardOutputText);
        Assert.AreEqual("curl: (3) " + MalformedLine + NewLine, StandardErrorText);
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
