using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-w</c> values after a write failure ends an HTTP transfer with exit 23: the response that arrived
/// still reports its own status and sizes. Measured on 2026-10-22 with curl 8.22.0 (mingw, Schannel) through
/// <c>Record-CurlExchange.ps1</c> on 127.0.0.1, with the response <c>HTTP/1.1 200 OK</c>,
/// <c>Content-Length: 5</c>, body <c>hello</c> (38 header bytes) (BL-3170 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerWriteOutAfterWriteFailureTests
{
    private const string Url = "http://127.0.0.1:47123/";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private const string Sizes = "%{http_code} %{size_download} %{size_header}";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    /// <summary>
    /// curl 8.22.0 with standard output opened read-only (<c>1&lt;file</c>): its stdio buffer takes the body and the
    /// flush at the end fails, so the whole body counts: <c>curl: Failed writing body</c>, then <c>200 5 38</c>.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_StandardOutputFailsOnTheFirstBodyWrite_WritesOutTheRealStatusAndSizes()
    {
        FailingWriteStream standardOutput = new();

        int exitCode = await RunAsync(standardOutput, "-sS", "-w", "%{stderr}" + Sizes, Url);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", "curl: Failed writing body\n200 5 38", Lf(StandardErrorText));
        Assert.AreEqual("curl: Failed writing body" + NewLine + "200 5 38", StandardErrorText);
    }

    /// <summary>
    /// curl 8.22.0 with <c>-o</c> naming a directory: the file fails to open at the first body write, after the
    /// headers, so <c>200 0 38</c> with <c>curl: (23) client returned ERROR on write of 5 bytes</c>.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_OutputFileFailsToOpenAfterTheHeaders_WritesOutTheRealStatusAndSizes()
    {
        files.UnwritablePaths.Add("out.bin");
        MemoryStream standardOutput = new();

        int exitCode = await RunAsync(standardOutput, "-sS", "-o", "out.bin", "-w", Sizes, Url);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stdout", "200 0 38", Encoding.Latin1.GetString(standardOutput.ToArray()));
        Assert.AreEqual("200 0 38", Encoding.Latin1.GetString(standardOutput.ToArray()));
        Diagnostics.Diff("stderr", "curl: (23) client returned ERROR on write of 5 bytes\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (23) client returned ERROR on write of 5 bytes" + NewLine, StandardErrorText);
    }

    /// <summary>
    /// curl 8.22.0 with <c>-D</c> naming a directory: the header file fails to open before the transfer starts, so
    /// nothing arrived and <c>000 0 0</c> is written out, after <c>curl: Failed to open</c> and the <c>(23)</c> line.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_HeaderFileFailsToOpen_WritesOutNoTransfer()
    {
        files.UnwritablePaths.Add("headers.txt");
        MemoryStream standardOutput = new();

        int exitCode = await RunAsync(standardOutput, "-sS", "-D", "headers.txt", "-w", Sizes, Url);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stdout", "000 0 0", Encoding.Latin1.GetString(standardOutput.ToArray()));
        Assert.AreEqual("000 0 0", Encoding.Latin1.GetString(standardOutput.ToArray()));
        string expectedStandardError = "curl: Failed to open headers.txt" + NewLine
            + "curl: (23) Failed writing received data to disk/application" + NewLine;
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    /// <summary>
    /// A retried attempt whose handler gives no report and never opened the <c>-o</c> file keeps no bytes under
    /// <c>-i</c> and writes out no transfer, as a failed connect does: <c>000 0</c>.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_RetriedAttemptWithoutAReportUnderInclude_WritesOutNoTransfer()
    {
        MemoryStream standardOutput = new();
        IProtocolHandler failing = RecordingProtocolHandler.Failing("fail", CurlExitCode.OperationTimedOut, "Operation timed out");

        int exitCode = await RunAsync(standardOutput, failing, "-s", "-i", "--retry", "1", "-o", "out.bin", "-w", "%{http_code} %{size_download}", "fail://h/x");

        Diagnostics.Assert("exit code", 28, exitCode);
        Assert.AreEqual(28, exitCode);
        Diagnostics.Diff("stdout", "000 0", Encoding.Latin1.GetString(standardOutput.ToArray()));
        Assert.AreEqual("000 0", Encoding.Latin1.GetString(standardOutput.ToArray()));
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private Task<int> RunAsync(MemoryStream standardOutput, params string[] arguments)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);
        Diagnostics.Arrange("scripted HTTP response (CRLF shown as LF)", Lf(Ok));
        return RunAsync(
            standardOutput,
            new HttpProtocolHandler(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false))),
            arguments);
    }

    private async Task<int> RunAsync(MemoryStream standardOutput, IProtocolHandler handler, params string[] arguments)
    {
        Diagnostics.Arrange("unwritable paths", string.Join(", ", files.UnwritablePaths));
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                timeProvider: new ImmediateTimerTimeProvider())
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Act("stderr (CRLF shown as LF)", Lf(StandardErrorText));
        return exitCode;
    }
}
