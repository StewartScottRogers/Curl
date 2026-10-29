using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-178 (BL-3707): <c>--libcurl -</c> writes the source to standard output with CR LF line ends on Windows
/// while standard output is in text mode (<c>-o</c> to a file), and with LF once a body switched it to binary.
/// </summary>
[TestClass]
public sealed class Cgr178Tests
{
    private readonly InMemoryFileSystem files = new();

    private readonly MemoryStream standardOutput = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_LibcurlToStandardOutputOnWindowsWithOutputFile_WritesCrLf()
    {
        string stdout = await RunAsync(runsOnWindows: true, "-s", "--libcurl", "-", "-o", "out.bin", "http://127.0.0.1:22000/a");

        Diagnostics.Assert("has CR LF", true, stdout.Contains("\r\n", StringComparison.Ordinal));
        Assert.IsTrue(stdout.Contains("\r\n", StringComparison.Ordinal));
        Assert.IsFalse(stdout.Replace("\r\n", string.Empty, StringComparison.Ordinal).Contains('\r'));
        Assert.IsFalse(stdout.Replace("\r\n", string.Empty, StringComparison.Ordinal).Contains('\n'));
    }

    [TestMethod]
    public async Task RunAsync_LibcurlToStandardOutputOnWindowsWithBodyOnStandardOutput_WritesLf()
    {
        string stdout = await RunAsync(runsOnWindows: true, "-s", "--libcurl", "-", "http://127.0.0.1:22000/a");

        Assert.IsFalse(stdout.Contains('\r'));
    }

    [TestMethod]
    public async Task RunAsync_LibcurlToStandardOutputOffWindows_WritesLf()
    {
        string stdout = await RunAsync(runsOnWindows: false, "-s", "--libcurl", "-", "-o", "out.bin", "http://127.0.0.1:22000/a");

        Assert.IsFalse(stdout.Contains('\r'));
    }

    private async Task<string> RunAsync(bool runsOnWindows, params string[] arguments)
    {
        RecordingProtocolHandler http = new("http", async context =>
        {
            context.Events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
            byte[] body = "body\n"u8.ToArray();
            await context.Output.WriteAsync(body, context.CancellationToken);
            return TransferResult.Success(body.Length) with { Report = new TransferReport { ResponseCode = 200 } };
        });
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                files,
                files,
                standardOutput,
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: runsOnWindows,
                outputPaths: files)
            .RunAsync(arguments);
        Assert.AreEqual(0, exitCode);
        return Encoding.UTF8.GetString(standardOutput.ToArray());
    }
}
