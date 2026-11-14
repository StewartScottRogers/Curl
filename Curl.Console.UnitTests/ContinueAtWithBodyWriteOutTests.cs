using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>Pins BL-5119: a <c>-C</c> offset refused beside <c>-d</c>, <c>-F</c> or <c>--json</c> still writes the <c>-w</c> output.</summary>
[TestClass]
public sealed class ContinueAtWithBodyWriteOutTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private RecordingProtocolHandler? handler;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-d", "x", "--data")]
    [DataRow("-F", "a=b", "--form")]
    public async Task RunAsync_ContinueAtWithBody_WritesTheWriteOut(string option, string value, string named)
    {
        int exitCode = await RunAsync(["-sS", "-C", "100", option, value, "-w", "[W %{http_code} %{exitcode}]", "http://127.0.0.1:1/"]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("[W 000 2]", Text(standardOutput));
        StringAssert.Contains(Text(standardError), "curl: cannot mix --continue-at with " + named);
        StringAssert.Contains(Text(standardError), "curl: (2) Failed initialization");
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtWithJson_WritesTheWriteOut()
    {
        int exitCode = await RunAsync(["-sS", "-C", "100", "-X", "POST", "--json", "{}", "-w", "[W %{http_code} %{exitcode}]", "http://127.0.0.1:1/"]);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("[W 000 2]", Text(standardOutput));
        StringAssert.Contains(Text(standardError), "curl: (2) Failed initialization");
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtWithBodyAndOnError_WritesTheOnErrorOutput()
    {
        int exitCode = await RunAsync(["-sS", "-C", "100", "-d", "x", "-w", "%{onerror}[E %{exitcode}]", "http://127.0.0.1:1/"]);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("[E 2]", Text(standardOutput));
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtWithBodyAndNoWriteOut_WritesNothingAndConnectsNowhere()
    {
        int exitCode = await RunAsync(["-sS", "-C", "100", "-d", "x", "http://127.0.0.1:1/"]);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(string.Empty, Text(standardOutput));
        Assert.IsEmpty(handler!.Contexts);
    }

    private static string Text(MemoryStream stream) => Encoding.UTF8.GetString(stream.ToArray());

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        handler = RecordingProtocolHandler.WritingPath("http");
        int exitCode = await new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows: false, TerminalColumns.Default)
            .RunAsync(arguments);
        Diagnostics.Act("stdout", Text(standardOutput));
        Diagnostics.Act("stderr", Text(standardError));

        return exitCode;
    }
}
