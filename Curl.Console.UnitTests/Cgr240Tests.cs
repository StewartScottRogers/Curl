using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-240: measured on curl 8.22.0 (Windows), <c>-o out.txt URL -d foobar -C 3</c> exits 2 with
/// <c>cannot mix --continue-at with --data</c> and leaves an empty <c>out.txt</c>, because curl opens a
/// resumed transfer's output file before it meets that check.
/// </summary>
[TestClass]
public sealed class Cgr240Tests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_ContinueAtWithData_CreatesTheEmptyOutputFile()
    {
        int exitCode = await RunAsync(["-o", "out.txt", "http://127.0.0.1:21359/426", "-d", "foobar", "-C", "3"]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Assert.AreEqual(2, exitCode);
        Diagnostics.Assert("out.txt written", true, outputFiles.Written.ContainsKey("out.txt"));
        Assert.IsTrue(outputFiles.Written.ContainsKey("out.txt"));
        Assert.IsEmpty(outputFiles.Written["out.txt"].ToArray());
    }

    [TestMethod]
    public async Task RunAsync_UnknownOptionAfterContinueAtWithData_CreatesNoFile()
    {
        int exitCode = await RunAsync(["-o", "out.txt", "http://127.0.0.1:21359/426", "-d", "foobar", "-C", "3", "--no-such-option"]);

        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(outputFiles.Written);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtWithDataToAnUnwritableFile_StillExitsTwo()
    {
        outputFiles.UnwritablePaths.Add("out.txt");

        int exitCode = await RunAsync(["-o", "out.txt", "http://127.0.0.1:21359/426", "-d", "foobar", "-C", "3"]);

        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(outputFiles.Written);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtWithFormUnderOutputDir_CreatesTheEmptyFileThere()
    {
        int exitCode = await RunAsync(["--output-dir", "dir", "-o", "out.txt", "http://127.0.0.1:21359/426", "-F", "a=b", "-C", "3"]);

        Assert.AreEqual(2, exitCode);
        Assert.IsTrue(outputFiles.Written.ContainsKey("dir/out.txt"));
    }

    [TestMethod]
    [DataRow("-o", "-")]
    [DataRow("-s", "-s")]
    public async Task RunAsync_ContinueAtWithDataToStandardOutput_CreatesNoFile(string first, string second)
    {
        int exitCode = await RunAsync([first, second, "http://127.0.0.1:21359/426", "-d", "foobar", "-C", "3"]);

        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(outputFiles.Written);
    }

    [TestMethod]
    [DataRow("-d", "foobar", "-C", "3")]
    [DataRow("-d", "a=b", "-G", "-C")]
    [DataRow("-C", "0", "-d", "foobar")]
    [DataRow("-C", "3", "-X", "GET")]
    public async Task RunAsync_NoUrlRefusal_CreatesNoFile(string first, string second, string third, string fourth)
    {
        string[] arguments = first == "-d" && third == "-G"
            ? ["-o", "out.txt", first, second, third, fourth, "3"]
            : ["-o", "out.txt", first, second, third, fourth];

        int exitCode = await RunAsync(arguments);

        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(outputFiles.Written);
    }

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        int exitCode = await new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows: false, TerminalColumns.Default)
            .RunAsync(arguments);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Encoding.UTF8.GetString(standardError.ToArray()));

        return exitCode;
    }
}
