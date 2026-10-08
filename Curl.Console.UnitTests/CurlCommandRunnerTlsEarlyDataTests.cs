using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that <c>%{tls_earlydata}</c> prints the TLS 1.3 early data bytes the transfer reported
/// sending, negative when the server rejected them, each transfer its own, and <c>0</c> without
/// a report (BL-1150), as curl 8.21.0's OpenSSL build prints <c>CURLINFO_EARLYDATA_SENT_T</c>.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTlsEarlyDataTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_WriteOutTlsEarlyData_PrintsWhatEachTransferReported()
    {
        RecordingProtocolHandler handler = new("https", context =>
        {
            if (context.Url.AbsolutePath == "/accepted")
            {
                context.Events.ReportTlsEarlyData(512);
            }
            else if (context.Url.AbsolutePath == "/rejected")
            {
                context.Events.ReportTlsEarlyData(-512);
            }

            return ValueTask.FromResult(TransferResult.Success(0));
        });
        Diagnostics.Arrange("reported early data", "/accepted 512, /rejected -512, /none no report");

        int exitCode = await RunAsync(
            ["-s", "--tls-earlydata", "-w", "%{tls_earlydata}\n", "https://127.0.0.1/accepted", "https://127.0.0.1/rejected", "https://127.0.0.1/none"],
            handler);

        string output = Encoding.UTF8.GetString(standardOutput.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "512\n-512\n0\n", output);
        Assert.AreEqual("512\n-512\n0\n", Encoding.UTF8.GetString(standardOutput.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows: false, TerminalColumns.Default)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Encoding.UTF8.GetString(standardOutput.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal));
        return exitCode;
    }
}
