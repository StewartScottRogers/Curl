using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;

namespace Curl.Console;

/// <summary>
/// Pins the runner against curl 8.21.0's behaviour, measured on Windows on 2026-09-26:
/// the <c>curl: (N) message</c> line, <c>-s</c> and <c>-sS</c>, the malformed-URL line,
/// the last transfer's exit code, the <c>-o</c> pairing and exit 23, and parser refusals.
/// Every test runs in memory with fake handlers and a fake file system.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTests
{
    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly MemoryStream standardInput = new();
    private readonly InMemoryFileSystem fileSystem = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_UnsupportedScheme_PrintsExit1LineAndReturns1()
    {
        int exitCode = await RunAsync(["foo://x/"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("curl: (1) Protocol \"foo\" not supported" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentUnsupportedScheme_PrintsNothingAndReturns1()
    {
        int exitCode = await RunAsync(["-s", "foo://x/"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentShowErrorUnsupportedScheme_PrintsExit1Line()
    {
        int exitCode = await RunAsync(["-sS", "foo://x/"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("curl: (1) Protocol \"foo\" not supported" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_MalformedUrl_PrintsExit3LineAndReturns3()
    {
        RecordingProtocolHandler dict = RecordingProtocolHandler.WritingPath("dict");

        int exitCode = await RunAsync(["-sS", "dict://exa mple.com/d:x"], dict);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("curl: (3) URL rejected: Malformed input to a URL function" + NewLine, StandardErrorText);
        Assert.IsEmpty(dict.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_FailureThenSuccess_ReturnsLastTransfersExitCode()
    {
        int exitCode = await RunAsync(["foo://x/", "file:///first"], RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("curl: (1) Protocol \"foo\" not supported" + NewLine, StandardErrorText);
        Assert.AreEqual("/first", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_SuccessThenFailure_ReturnsLastTransfersExitCode()
    {
        int exitCode = await RunAsync(["file:///first", "foo://x/"], RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("/first", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_TwoFailures_PrintsBothLinesInOrderAndReturnsLast()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.Failing(
            "file", CurlExitCode.FileCouldntReadFile, "Could not open file C:/nonexist/a");

        int exitCode = await RunAsync(["file:///C:/nonexist/a", "foo://x/"], file);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(
            "curl: (37) Could not open file C:/nonexist/a" + NewLine
            + "curl: (1) Protocol \"foo\" not supported" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TwoOutputFiles_PairsEachWithItsUrlInOrder()
    {
        int exitCode = await RunAsync(
            ["-sS", "-o", "a.txt", "-o", "b.txt", "file:///first", "file:///second"],
            RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/first", Encoding.ASCII.GetString(fileSystem.Written["a.txt"].ToArray()));
        Assert.AreEqual("/second", Encoding.ASCII.GetString(fileSystem.Written["b.txt"].ToArray()));
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_UrlWithoutMatchingOutputFile_GoesToStandardOutput()
    {
        int exitCode = await RunAsync(
            ["-o", "a.txt", "file:///first", "file:///second"],
            RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/first", Encoding.ASCII.GetString(fileSystem.Written["a.txt"].ToArray()));
        Assert.AreEqual("/second", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutputFileCannotBeCreated_ReturnsExit23WithCurlsWriteLine()
    {
        InMemoryFileSystem files = new() { ReadContent = new byte[92] };
        files.UnwritablePaths.Add("C:/nonexist/dir/x");
        CurlCommandRunner runner = new(
            _ => new ProtocolDispatcher([new FileProtocolHandler(files)]),
            files,
            standardOutput,
            standardError,
            standardInput);

        int exitCode = await runner.RunAsync(["-sS", "-o", "C:/nonexist/dir/x", "file:///C:/Windows/win.ini"]);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual("curl: (23) client returned ERROR on write of 92 bytes" + NewLine, StandardErrorText);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_EmptyTransferToOutputFile_CreatesEmptyFile()
    {
        RecordingProtocolHandler empty = new("file", _ => ValueTask.FromResult(TransferResult.Success(0)));

        int exitCode = await RunAsync(["-o", "empty.txt", "file:///empty"], empty);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(fileSystem.Written["empty.txt"].ToArray());
    }

    [TestMethod]
    public async Task RunAsync_EmptyTransferToUncreatableOutputFile_ReturnsExit23WithNoLine()
    {
        RecordingProtocolHandler empty = new("file", _ => ValueTask.FromResult(TransferResult.Success(0)));
        fileSystem.UnwritablePaths.Add("Z:/nonexist/x");

        int exitCode = await RunAsync(["-sS", "-o", "Z:/nonexist/x", "file:///empty"], empty);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferToOutputFile_DoesNotCreateTheFile()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.Failing(
            "file", CurlExitCode.FileCouldntReadFile, "Could not open file C:/nonexist/a");

        int exitCode = await RunAsync(["-o", "a.txt", "file:///C:/nonexist/a"], file);

        Assert.AreEqual(37, exitCode);
        Assert.IsFalse(fileSystem.Written.ContainsKey("a.txt"));
    }

    [TestMethod]
    public async Task RunAsync_ParserRefusal_PrintsEachLineWithNewLineAndRunsNoHandler()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync(["--no-such-option", "file:///a"], file);

        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
        Assert.AreEqual(
            "curl: option --no-such-option: is unknown" + NewLine
            + CommandLineRefusal.TryHelpLine + NewLine,
            StandardErrorText);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_EmptyCommandLine_TransfersNothingAndReturns0()
    {
        int exitCode = await RunAsync([]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_FailureWithoutMessage_PrintsNoLine()
    {
        RecordingProtocolHandler file = new(
            "file", _ => ValueTask.FromResult(new TransferResult(CurlExitCode.ReadError, 0)));

        int exitCode = await RunAsync(["file:///a"], file);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_ProtocolOptions_ReachTheHandlersContext()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync(
            [
                "-d", "payload", "-u", "user:secret", "-t", "TTYPE=vt100", "-t", "XDISPLOC=x:0",
                "--tftp-blksize", "1024", "--tftp-no-options", "--create-file-mode", "0600", "file:///a",
            ],
            file);

        ITransferContext context = file.Contexts.Single();
        Assert.AreEqual("payload", Encoding.UTF8.GetString(context.PostData!.Value.Span));
        Assert.AreEqual("user", context.Credentials!.UserName);
        Assert.AreEqual("secret", context.Credentials.Password);
        CollectionAssert.AreEqual(new[] { "TTYPE=vt100", "XDISPLOC=x:0" }, context.TelnetOptions.ToArray());
        Assert.AreEqual(1024, context.TftpBlockSize);
        Assert.IsTrue(context.TftpNoOptions);
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, context.CreateFileMode);
        Assert.AreEqual(new Uri("file:///a"), context.Url);
    }

    [TestMethod]
    public async Task RunAsync_NoProtocolOptions_ContextCarriesNotGivenValues()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync(["file:///a"], file);

        ITransferContext context = file.Contexts.Single();
        Assert.IsNull(context.PostData);
        Assert.IsNull(context.Credentials);
        Assert.IsEmpty(context.TelnetOptions);
        Assert.IsNull(context.TftpBlockSize);
        Assert.IsFalse(context.TftpNoOptions);
        Assert.AreEqual(TransferContext.DefaultCreateFileMode, context.CreateFileMode);
        Assert.AreSame(standardOutput, context.Output);
    }

    [TestMethod]
    public async Task RunAsync_TelnetUrl_ReceivesStandardInputAsUpload()
    {
        RecordingProtocolHandler telnet = RecordingProtocolHandler.WritingPath("telnet");

        await RunAsync(["telnet://h/"], telnet);

        Assert.AreSame(standardInput, telnet.Contexts.Single().Upload);
    }

    [TestMethod]
    public async Task RunAsync_DictUrl_ReceivesNullUpload()
    {
        RecordingProtocolHandler dict = RecordingProtocolHandler.WritingPath("dict");

        await RunAsync(["dict://h/d:x"], dict);

        Assert.IsNull(dict.Contexts.Single().Upload);
    }

    [TestMethod]
    public async Task RunAsync_RefusedCommandLine_NeverBuildsTheDispatcher()
    {
        int calls = 0;
        CurlCommandRunner runner = new(
            _ =>
            {
                calls++;
                return new ProtocolDispatcher([]);
            },
            fileSystem,
            standardOutput,
            standardError,
            standardInput);

        await runner.RunAsync(["--no-such-option", "file:///a"]);

        Assert.AreEqual(0, calls);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, params IProtocolHandler[] handlers) =>
        new CurlCommandRunner(_ => new ProtocolDispatcher(handlers), fileSystem, standardOutput, standardError, standardInput)
            .RunAsync(arguments);
}
