using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins curl 8.22.0's exit 3 for a URL typed without a scheme whose authority ends in an empty
/// port, rejected before any handler is called (measured with <c>Record-CurlExchange.ps1</c> on
/// Windows, 2026-10-22, BL-3175 Notes), and the malformed-input rejection beside it unchanged.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerUrlRejectionPortTests
{
    private const string BadPortLine = "curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535";

    private const string MalformedLine = "curl: (3) URL rejected: Malformed input to a URL function";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    [DataRow("http:")]
    [DataRow("1http://127.0.0.1:9/")]
    public async Task RunAsync_EmptyGuessedPort_ExitsThreeWithoutCallingTheHandler(string url)
    {
        int exitCode = await RunAsync(["-sS", url]);

        AssertRejected(BadPortLine, exitCode);
    }

    // On Windows "h:" is a drive prefix, so curl guesses http for the whole text and the port is empty.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_DriveLetterSchemeOnWindows_ExitsThreeWithoutCallingTheHandler()
    {
        int exitCode = await RunAsync(["-sS", "h://127.0.0.1:9/x"]);

        AssertRejected(BadPortLine, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_SpaceInTheScheme_StaysMalformedInput()
    {
        int exitCode = await RunAsync(["-sS", "ht tp://127.0.0.1:9/"]);

        AssertRejected(MalformedLine, exitCode);
    }

    private void AssertRejected(string expectedLine, int exitCode)
    {
        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Assert("handler call count", 0, http.Contexts.Count);
        Assert.IsEmpty(http.Contexts);
        Diagnostics.Diff("stderr", expectedLine + "\n", StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.AreEqual(expectedLine + Environment.NewLine, StandardErrorText);
    }

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                outputPaths: fileSystem)
            .RunAsync(arguments);

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", StandardErrorText);
        return exitCode;
    }
}
