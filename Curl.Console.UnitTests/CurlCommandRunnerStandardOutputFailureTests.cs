using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;
using Curl.Protocol.Telnet;

namespace Curl.Console;

/// <summary>
/// Pins, end to end through the real <c>file</c> and <c>telnet</c> handlers, the line and
/// exit code a transfer to a closed standard output ends with, against curl 8.21.0 measured
/// on Windows on 2026-09-26 with <c>curl -sS URL &gt;&amp;-</c> (BL-099): <c>passed N</c> is
/// the write that overflowed curl's 4096-byte stdio buffer and <c>returned M</c> the room
/// that buffer had left for it.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerStandardOutputFailureTests
{
    private static readonly string NewLine = Environment.NewLine;

    private readonly FailingWriteStream closedStandardOutput = new();
    private readonly MemoryStream standardError = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_FileBodySmallerThanTheStdioBuffer_PrintsFailedWritingBody()
    {
        int exitCode = await RunFileAsync(4095);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual("curl: Failed writing body" + NewLine, StandardErrorText);
    }

    [TestMethod]
    [DataRow(4096, "passed 4096 returned 0")]
    [DataRow(4097, "passed 4097 returned 0")]
    [DataRow(8192, "passed 8192 returned 0")]
    [DataRow(16384, "passed 16384 returned 0")]
    [DataRow(16385, "passed 16384 returned 0")]
    [DataRow(20000, "passed 16384 returned 0")]
    public async Task RunAsync_FileBodyFillingTheStdioBuffer_PrintsCurlsPassedReturnedLine(int bodyLength, string counts)
    {
        int exitCode = await RunFileAsync(bodyLength);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual("curl: (23) Failure writing output to destination, " + counts + NewLine, StandardErrorText);
    }

    [TestMethod]
    [DataRow(100, 100, "passed 100 returned 96")]
    [DataRow(300, 100, "passed 300 returned 196")]
    [DataRow(1000, 20, "passed 1000 returned 96")]
    [DataRow(30, 400, "passed 30 returned 16")]
    [DataRow(5000, 5, "passed 4096 returned 0")]
    public async Task RunAsync_TelnetLinesToAClosedStandardOutput_PrintsCurlsPassedReturnedLine(
        int lineLength,
        int lineCount,
        string counts)
    {
        int exitCode = await RunTelnetAsync(lineLength, lineCount);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual("curl: (23) Failure writing output to destination, " + counts + NewLine, StandardErrorText);
    }

    private Task<int> RunFileAsync(int bodyLength)
    {
        FileProtocolHandler file = new(new InMemoryFileSystem { ReadContent = new byte[bodyLength] });

        return RunAsync(["-sS", "file:///C:/body.bin"], file);
    }

    private Task<int> RunTelnetAsync(int lineLength, int lineCount)
    {
        byte[] line = [.. Enumerable.Repeat((byte)'a', lineLength - 2), (byte)'\r', (byte)'\n'];
        ScriptedConnector server = new(Enumerable.Repeat(line, lineCount));

        return RunAsync(["-sS", "telnet://127.0.0.1:2323/"], new TelnetProtocolHandler(server));
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, params IProtocolHandler[] handlers) =>
        new CurlCommandRunner(
            _ => new ProtocolDispatcher(handlers),
            new InMemoryFileSystem(),
            new InMemoryFileSystem(),
            closedStandardOutput,
            standardError,
            new MemoryStream(),
            runsOnWindows: false)
            .RunAsync(arguments);
}
