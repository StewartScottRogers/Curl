using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that <c>%{ssl_verify_result}</c> and <c>%{proxy_ssl_verify_result}</c> print the codes
/// the transfer's certificate checks reported, each transfer its own, and <c>0</c> without one
/// (BL-661). curl 8.18.0 (OpenSSL) printed <c>18</c> under <c>-k</c> for a self-signed server
/// and, for the same run with the self-signed server as an HTTPS proxy, <c>0 18</c>.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerVerifyResultTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    [TestMethod]
    public async Task RunAsync_WriteOutVerifyResults_PrintsWhatEachTransferReported()
    {
        RecordingProtocolHandler handler = new("https", context =>
        {
            if (context.Url.AbsolutePath == "/checked")
            {
                context.Events.ReportCertificateVerifyResult(20, isProxy: true);
                context.Events.ReportCertificateVerifyResult(18, isProxy: false);
            }

            return ValueTask.FromResult(TransferResult.Success(0));
        });

        int exitCode = await RunAsync(
            ["-s", "-k", "-w", "%{ssl_verify_result} %{proxy_ssl_verify_result}\\n", "https://127.0.0.1/checked", "https://127.0.0.1/plain"],
            handler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("18 20\n0 0\n", Encoding.UTF8.GetString(standardOutput.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler) =>
        new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows: false, TerminalColumns.Default)
            .RunAsync(arguments);
}
