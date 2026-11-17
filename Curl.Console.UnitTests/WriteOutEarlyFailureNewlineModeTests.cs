using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>Pins BL-5341: on Windows, <c>-w</c> on standard output keeps curl's LF or CR LF choice in three early failures.</summary>
[TestClass]
public sealed class WriteOutEarlyFailureNewlineModeTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly InMemoryFileSystem files = new();

    [TestMethod]
    public async Task RunAsync_ContinueAtBesideBodyOnWindows_WritesWriteOutWithLf()
    {
        int exitCode = await RunAsync(["-s", "-C", "100", "-d", "x", "-w", "[W %{exitcode}]\n", "http://127.0.0.1:1/"]);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("[W 2]\n", Text());
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtBesideBodyToFileOnWindows_WritesWriteOutWithCrLf()
    {
        await RunAsync(["-s", "-C", "100", "-d", "x", "-o", "out.bin", "-w", "[W %{exitcode}]\n", "http://127.0.0.1:1/"]);

        Assert.AreEqual("[W 2]\r\n", Text());
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtBesideBodyWithUseAsciiOnWindows_WritesWriteOutWithCrLf()
    {
        await RunAsync(["-s", "-B", "-C", "100", "-d", "x", "-w", "[W %{exitcode}]\n", "http://127.0.0.1:1/"]);

        Assert.AreEqual("[W 2]\r\n", Text());
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtBesideBodyWithHeadersToStandardOutputOnWindows_WritesWriteOutWithLf()
    {
        await RunAsync(["-s", "-B", "-D", "-", "-C", "100", "-d", "x", "-w", "[W %{exitcode}]\n", "http://127.0.0.1:1/"]);

        Assert.AreEqual("[W 2]\n", Text());
    }

    [TestMethod]
    public async Task RunAsync_UploadUrlMalformedOnWindows_WritesWriteOutWithCrLf()
    {
        int exitCode = await RunAsync(["-s", "-T", "k5.cfg", "-w", "E%{exitcode}\n", "http://a%20b/"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("E3\r\n", Text());
    }

    [TestMethod]
    public async Task RunAsync_FailEarlyParallelOnWindows_WritesEveryWriteOutWithLf()
    {
        await RunAsync(["-s", "-Z", "--fail-early", "-w", "A[%{exitcode}]B\n", "http://a.h/1", "http://a.h/2"]);

        string text = Text();
        Assert.DoesNotContain("\r", text);
        Assert.IsTrue(text.StartsWith("A[7]B\n", StringComparison.Ordinal), text);
    }

    [TestMethod]
    public async Task RunAsync_SuccessfulStandardOutputTransferOnWindows_WritesWriteOutWithLf()
    {
        await RunAsync(["-s", "-w", "[%{exitcode}]\n", "http://a.h/ok"], succeeds: true);

        Assert.EndsWith("[0]\n", Text());
        Assert.DoesNotContain("\r", Text());
    }

    private string Text() => Encoding.UTF8.GetString(standardOutput.ToArray());

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, bool succeeds = false)
    {
        RecordingProtocolHandler handler = succeeds
            ? RecordingProtocolHandler.WritingPath("http")
            : RecordingProtocolHandler.Failing("http", CurlExitCode.CouldntConnect, "Could not connect to server");
        return await new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), files, files, standardOutput, new MemoryStream(), new MemoryStream(), runsOnWindows: true, TerminalColumns.Default)
            .RunAsync(arguments);
    }
}
