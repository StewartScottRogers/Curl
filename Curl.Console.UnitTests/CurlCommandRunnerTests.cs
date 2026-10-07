using System.Globalization;
using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;
using Curl.Testing;

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
    private const int ShownCharacterCap = 200;

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly MemoryStream standardInput = new();
    private readonly InMemoryFileSystem fileSystem = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_UnsupportedScheme_PrintsExit1LineAndReturns1()
    {
        int exitCode = await RunAsync(["foo://x/"]);

        Diagnostics.Assert("exit code", 1, exitCode);
        Assert.AreEqual(1, exitCode);
        Diagnostics.Diff("stderr", "curl: (1) Protocol \"foo\" not supported\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (1) Protocol \"foo\" not supported" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentUnsupportedScheme_PrintsNothingAndReturns1()
    {
        int exitCode = await RunAsync(["-s", "foo://x/"]);

        Diagnostics.Assert("exit code", 1, exitCode);
        Assert.AreEqual(1, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentShowErrorUnsupportedScheme_PrintsExit1Line()
    {
        int exitCode = await RunAsync(["-sS", "foo://x/"]);

        Diagnostics.Assert("exit code", 1, exitCode);
        Assert.AreEqual(1, exitCode);
        Diagnostics.Diff("stderr", "curl: (1) Protocol \"foo\" not supported\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (1) Protocol \"foo\" not supported" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_NextWithNoUrlAfterIt_RunsTheFirstGroupThenRefusesNoUrl()
    {
        int exitCode = await RunAsync(["foo://x/", "--next"]);

        Diagnostics.Assert("exit code", 2, exitCode);
        Assert.AreEqual(2, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: (1) Protocol \"foo\" not supported\ncurl: (2) no URL specified\ncurl: try 'curl --help' or 'curl --manual' for more information\n",
            Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (1) Protocol \"foo\" not supported" + NewLine
            + "curl: (2) no URL specified" + NewLine
            + "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_MalformedUrl_PrintsExit3LineAndReturns3()
    {
        RecordingProtocolHandler dict = RecordingProtocolHandler.WritingPath("dict");

        int exitCode = await RunAsync(["-sS", "dict://exa mple.com/d:x"], dict);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Diff("stderr", "curl: (3) URL rejected: Malformed input to a URL function\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (3) URL rejected: Malformed input to a URL function" + NewLine, StandardErrorText);
        Diagnostics.Assert("dict contexts", 0, dict.Contexts.Count);
        Assert.IsEmpty(dict.Contexts);
    }

    // curl 8.21.0 cuts a transfer's message to its 255-byte error buffer (ADR-0072):
    // `curl -sS file:///nodir/<300 a's>` exits 37 and prints "curl: (37) Could not open file
    // /nodir/" and the first 228 a's, 268 bytes with CR LF (measured 2026-09-27, BL-380).
    [TestMethod]
    public async Task RunAsync_FailureMessageOver255Bytes_PrintsItCutTo255Bytes()
    {
        string name = new('a', 300);
        RecordingProtocolHandler file = RecordingProtocolHandler.Failing(
            "file", CurlExitCode.FileCouldntReadFile, "Could not open file /nodir/" + name);
        Diagnostics.Arrange("failure message length", ("Could not open file /nodir/" + name).Length);

        int exitCode = await RunAsync(["-sS", "file:///nodir/" + name], file);

        Diagnostics.Assert("exit code", 37, exitCode);
        Assert.AreEqual(37, exitCode);
        Diagnostics.Diff("stderr", "curl: (37) Could not open file /nodir/" + name[..228] + "\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (37) Could not open file /nodir/" + name[..228] + NewLine, StandardErrorText);
    }

    // A bad glob's lines are formatted by curl's tool, not libcurl's error buffer, so they are
    // not cut: `curl -sS http://x/<300 a's>[1-` prints "curl: (3) bad range in position 313:"
    // and the whole URL (measured 2026-09-27, BL-380).
    [TestMethod]
    public async Task RunAsync_BadGlobOver255Bytes_PrintsTheWholeUrl()
    {
        string url = "http://x/" + new string('a', 300) + "[1-";
        Diagnostics.Arrange("url length", url.Length);

        int exitCode = await RunAsync(["-sS", url]);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Assert("stderr holds the whole URL on its own line", true, Lf(StandardErrorText).Contains("\n" + url + "\n", StringComparison.Ordinal));
        Assert.Contains(NewLine + url + NewLine, StandardErrorText);
    }

    // System.Uri refused file://C:, so it ended with exit 3 before any handler ran. curl
    // 8.21.0 on Windows prints "curl: (37) Could not open file C:" (measured 2026-09-27);
    // outside Windows it rejects a drive letter with exit 3, and CurlUrl does the same.
    [TestMethod]
    public async Task RunAsync_BareDriveLetterFileUrl_ReachesTheFileHandlerAndReturns37()
    {
        InMemoryFileSystem files = new();
        files.UnreadablePaths.Add("C:");
        Diagnostics.Arrange("unreadable paths", "C:");

        int exitCode = await RunAsync(["-sS", "file://C:"], new FileProtocolHandler(files));

        if (OperatingSystem.IsWindows())
        {
            Diagnostics.Assert("exit code (Windows)", 37, exitCode);
            Assert.AreEqual(37, exitCode);
            Diagnostics.Diff("stderr (Windows)", "curl: (37) Could not open file C:\n", Lf(StandardErrorText));
            Assert.AreEqual("curl: (37) Could not open file C:" + NewLine, StandardErrorText);
            Diagnostics.Assert("read paths (Windows)", "C:", string.Join(", ", files.ReadPaths));
            CollectionAssert.AreEqual(new[] { "C:" }, files.ReadPaths.ToArray());
        }
        else
        {
            Diagnostics.Assert("exit code (not Windows)", 3, exitCode);
            Assert.AreEqual(3, exitCode);
            Diagnostics.Assert("read paths (not Windows)", string.Empty, string.Join(", ", files.ReadPaths));
            Assert.IsEmpty(files.ReadPaths);
        }
    }

    // curl 8.21.0 rejects each with exit 3 and "URL rejected: Bad file:// URL" (measured
    // 2026-09-27).
    [TestMethod]
    [DataRow("file://user:pass@localhost/x")]
    [DataRow("file://ab:/x")]
    [DataRow("file://example.com/x")]
    public async Task RunAsync_FileUrlCurlRejects_PrintsBadFileUrlAndReturns3WithoutOpeningAnything(string url)
    {
        InMemoryFileSystem files = new();

        int exitCode = await RunAsync(["-sS", url], new FileProtocolHandler(files));

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Diff("stderr", "curl: (3) URL rejected: Bad file:// URL\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (3) URL rejected: Bad file:// URL" + NewLine, StandardErrorText);
        Diagnostics.Assert("read paths", string.Empty, string.Join(", ", files.ReadPaths));
        Assert.IsEmpty(files.ReadPaths);
    }

    // Each line is curl 8.21.0's for the same URL, measured with /mingw64/bin/curl -gsS on
    // 2026-09-27 (BL-324).
    [TestMethod]
    [DataRow("http:////h/", "Unsupported number of slashes following scheme")]
    [DataRow("http://u@/", "No host part in the URL")]
    [DataRow("http://h:99999/", "Port number was not a decimal number between 0 and 65535")]
    [DataRow("http://[::g]/", "Bad IPv6 address")]
    [DataRow("http://a!b/", "Bad hostname")]
    [DataRow("http://h/a b", "Malformed input to a URL function")]
    public async Task RunAsync_UrlCurlRejects_PrintsCurlsReasonAndReturns3(string url, string reason)
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");
        Diagnostics.Arrange("expected reason", reason);

        int exitCode = await RunAsync(["-gsS", url], http);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Diff("stderr", "curl: (3) URL rejected: " + reason + "\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (3) URL rejected: " + reason + NewLine, StandardErrorText);
        Diagnostics.Assert("http contexts", 0, http.Contexts.Count);
        Assert.IsEmpty(http.Contexts);
    }

    // FR-064: curl 8.21.0 refuses a --url-query name holding a space before connecting.
    [TestMethod]
    public async Task RunAsync_UrlQueryNameHoldingASpace_PrintsMalformedInputAndReturns3WithoutConnecting()
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

        int exitCode = await RunAsync(["-sS", "--url-query", "a b=c", "http://h/p"], http);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Diff("stderr", "curl: (3) URL rejected: Malformed input to a URL function\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (3) URL rejected: Malformed input to a URL function" + NewLine, StandardErrorText);
        Diagnostics.Assert("http contexts", 0, http.Contexts.Count);
        Assert.IsEmpty(http.Contexts);
    }

    // curl 8.21.0 (Schannel) exits 3 with this line for a 65536-byte host, and tries to
    // resolve a 65535-byte one; the length is the percent-decoded host's (measured
    // 2026-09-27, BL-327; upstream test399).
    [TestMethod]
    [DataRow(65536, "")]
    [DataRow(65534, "%61a")]
    public async Task RunAsync_HostLongerThan65535Bytes_PrintsTooLongHostnameAndReturns3WithoutConnecting(
        int letters,
        string suffix)
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");
        Diagnostics.Arrange("host letters", letters);
        Diagnostics.Arrange("host suffix", suffix);

        int exitCode = await RunAsync(["-sS", $"http://{new string('a', letters)}{suffix}/399"], http);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Diff("stderr", "curl: (3) Too long hostname (maximum is 65535)\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (3) Too long hostname (maximum is 65535)" + NewLine, StandardErrorText);
        Diagnostics.Assert("http contexts", 0, http.Contexts.Count);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_Host65535BytesLong_ReachesTheHandler()
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");
        Diagnostics.Arrange("host letters", 65534);
        Diagnostics.Arrange("host suffix", "%61");

        int exitCode = await RunAsync(["-sS", $"http://{new string('a', 65534)}%61/399"], http);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("http contexts", 1, http.Contexts.Count);
        Assert.HasCount(1, http.Contexts);
    }

    // curl 8.21.0 (Schannel) prints these lines and exits 3 before any transfer (measured
    // 2026-09-27, BL-327; upstream test2092).
    [TestMethod]
    public async Task RunAsync_GlobRangeEndingAtLongMaxValue_PrintsRangeOverflowAndReturns3WithoutConnecting()
    {
        const string Url = "127.0.0.1:8990/[0-1][9223372036854775806-9223372036854775807]/2092";
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

        int exitCode = await RunAsync([Url], http);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: (3) range end/step overflow in position 62:\n" + Url + "\n" + new string(' ', 61) + "^\n",
            Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (3) range end/step overflow in position 62:" + NewLine + Url + NewLine
            + new string(' ', 61) + "^" + NewLine,
            StandardErrorText);
        Diagnostics.Assert("http contexts", 0, http.Contexts.Count);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_FailureThenSuccess_ReturnsLastTransfersExitCode()
    {
        int exitCode = await RunAsync(["foo://x/", "file:///first"], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", "curl: (1) Protocol \"foo\" not supported\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (1) Protocol \"foo\" not supported" + NewLine, StandardErrorText);
        Diagnostics.Diff("stdout", "/first", StandardOutputText);
        Assert.AreEqual("/first", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_SuccessThenFailure_ReturnsLastTransfersExitCode()
    {
        int exitCode = await RunAsync(["file:///first", "foo://x/"], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 1, exitCode);
        Assert.AreEqual(1, exitCode);
        Diagnostics.Diff("stdout", "/first", StandardOutputText);
        Assert.AreEqual("/first", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_TwoFailures_PrintsBothLinesInOrderAndReturnsLast()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.Failing(
            "file", CurlExitCode.FileCouldntReadFile, "Could not open file /nonexist/a");

        int exitCode = await RunAsync(["file:///nonexist/a", "foo://x/"], file);

        Diagnostics.Assert("exit code", 1, exitCode);
        Assert.AreEqual(1, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: (37) Could not open file /nonexist/a\ncurl: (1) Protocol \"foo\" not supported\n",
            Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (37) Could not open file /nonexist/a" + NewLine
            + "curl: (1) Protocol \"foo\" not supported" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TwoOutputFiles_PairsEachWithItsUrlInOrder()
    {
        int exitCode = await RunAsync(
            ["-sS", "-o", "a.txt", "-o", "b.txt", "file:///first", "file:///second"],
            RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("a.txt content", "/first", WrittenText("a.txt"));
        Assert.AreEqual("/first", Encoding.ASCII.GetString(fileSystem.Written["a.txt"].ToArray()));
        Diagnostics.Diff("b.txt content", "/second", WrittenText("b.txt"));
        Assert.AreEqual("/second", Encoding.ASCII.GetString(fileSystem.Written["b.txt"].ToArray()));
        Diagnostics.Assert("stdout length", 0L, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_UrlWithoutMatchingOutputFile_GoesToStandardOutput()
    {
        int exitCode = await RunAsync(
            ["-o", "a.txt", "file:///first", "file:///second"],
            RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("a.txt content", "/first", WrittenText("a.txt"));
        Assert.AreEqual("/first", Encoding.ASCII.GetString(fileSystem.Written["a.txt"].ToArray()));
        Diagnostics.Diff("stdout", "/second", StandardOutputText);
        Assert.AreEqual("/second", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutputFileCannotBeCreated_ReturnsExit23WithCurlsWriteLine()
    {
        InMemoryFileSystem files = new() { ReadContent = new byte[92] };
        files.UnwritablePaths.Add("C:/nonexist/dir/x");
        Diagnostics.Arrange("read content length", 92);
        Diagnostics.Arrange("unwritable paths", "C:/nonexist/dir/x");
        CurlCommandRunner runner = new(
            _ => new TransferDispatch(new ProtocolDispatcher([new FileProtocolHandler(files)])),
            files,
            files,
            standardOutput,
            standardError,
            standardInput,
            runsOnWindows: false);

        int exitCode = await RunRunnerAsync(runner, ["-sS", "-o", "C:/nonexist/dir/x", "file:///Windows/win.ini"], standardOutput);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", "curl: (23) client returned ERROR on write of 92 bytes\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (23) client returned ERROR on write of 92 bytes" + NewLine, StandardErrorText);
        Diagnostics.Assert("stdout length", 0L, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_OutputFileCannotBeCreatedWithoutSilent_PrintsFailedToOpenWarningBeforeWriteLine()
    {
        int exitCode = await RunToUncreatableOutputFileAsync("-o", "Z:/nonexist/x");

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to open the file Z:/nonexist/x: No such file or directory\ncurl: (23) client returned ERROR on write of 92 bytes\n",
            Lf(StandardErrorText));
        Assert.AreEqual(
            "Warning: Failed to open the file Z:/nonexist/x: No such file or directory" + NewLine
            + "curl: (23) client returned ERROR on write of 92 bytes" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_OutputFileOpenWarningAt79Columns_IsWrappedAsCurlWrapsIt()
    {
        InMemoryFileSystem files = new() { ReadContent = new byte[92], UnwritableStatus = FileAccessStatus.AccessDenied };
        files.UnwritablePaths.Add("C:/Windows/System32/bl087.txt");
        Diagnostics.Arrange("read content length", 92);
        Diagnostics.Arrange("unwritable paths", "C:/Windows/System32/bl087.txt");
        Diagnostics.Arrange("unwritable status", FileAccessStatus.AccessDenied);
        Diagnostics.Arrange("terminal columns", 79);
        CurlCommandRunner runner = new(
            _ => new TransferDispatch(new ProtocolDispatcher([new FileProtocolHandler(files)])),
            files,
            files,
            standardOutput,
            standardError,
            standardInput,
            runsOnWindows: false,
            terminalColumns: 79);

        int exitCode = await RunRunnerAsync(runner, ["-o", "C:/Windows/System32/bl087.txt", "file:///Windows/win.ini"], standardOutput);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to open the file C:/Windows/System32/bl087.txt: Permission \nWarning: denied\ncurl: (23) client returned ERROR on write of 92 bytes\n",
            Lf(StandardErrorText));
        Assert.AreEqual(
            "Warning: Failed to open the file C:/Windows/System32/bl087.txt: Permission " + NewLine
            + "Warning: denied" + NewLine
            + "curl: (23) client returned ERROR on write of 92 bytes" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_OutputFileCannotBeCreatedUnderSilent_PrintsNoWarning()
    {
        int exitCode = await RunToUncreatableOutputFileAsync("-s", "-o", "Z:/nonexist/x");

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_OutputFileCannotBeCreatedUnderSilentShowError_PrintsOnlyTheWriteLine()
    {
        int exitCode = await RunToUncreatableOutputFileAsync("-sS", "-o", "Z:/nonexist/x");

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", "curl: (23) client returned ERROR on write of 92 bytes\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (23) client returned ERROR on write of 92 bytes" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_EmptyTransferToUncreatableOutputFileWithoutSilent_PrintsOnlyTheWarning()
    {
        RecordingProtocolHandler empty = new("file", _ => ValueTask.FromResult(TransferResult.Success(0)));
        fileSystem.UnwritablePaths.Add("Z:/nonexist/x");
        Diagnostics.Arrange("unwritable paths", "Z:/nonexist/x");

        int exitCode = await RunAsync(["-o", "Z:/nonexist/x", "file:///empty"], empty);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to open the file Z:/nonexist/x: No such file or directory\n",
            Lf(StandardErrorText));
        Assert.AreEqual(
            "Warning: Failed to open the file Z:/nonexist/x: No such file or directory" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_EmptyTransferToOutputFile_CreatesEmptyFile()
    {
        RecordingProtocolHandler empty = new("file", _ => ValueTask.FromResult(TransferResult.Success(0)));

        int exitCode = await RunAsync(["-o", "empty.txt", "file:///empty"], empty);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Bytes("empty.txt content", fileSystem.Written["empty.txt"].ToArray());
        Diagnostics.Assert("empty.txt length", 0, fileSystem.Written["empty.txt"].ToArray().Length);
        Assert.IsEmpty(fileSystem.Written["empty.txt"].ToArray());
    }

    // curl 8.21.0, curl -z "1 Jan 2030" -o out.txt file:///... (measured 2026-09-26): the
    // unmet condition exits 0 and creates no out.txt.
    [TestMethod]
    public async Task RunAsync_UnmetTimeCondition_CreatesNoOutputFile()
    {
        RecordingProtocolHandler unmet = new("file", _ => ValueTask.FromResult(TransferResult.TimeConditionNotMet()));
        Diagnostics.Arrange("handler result", "time condition not met");

        int exitCode = await RunAsync(["-o", "out.txt", "file:///source"], unmet);

        Diagnostics.Assert("exit code", (int)CurlExitCode.Ok, exitCode);
        Assert.AreEqual((int)CurlExitCode.Ok, exitCode);
        Diagnostics.Assert("out.txt written", false, fileSystem.Written.ContainsKey("out.txt"));
        Assert.IsFalse(fileSystem.Written.ContainsKey("out.txt"));
        Diagnostics.Assert("write modes", 0, fileSystem.WriteModes.Count);
        Assert.IsEmpty(fileSystem.WriteModes);
    }

    // The same with an existing out.txt holding "old": curl leaves its content as it was.
    [TestMethod]
    public async Task RunAsync_UnmetTimeConditionWithExistingOutputFile_KeepsItsContent()
    {
        RecordingProtocolHandler unmet = new("file", _ => ValueTask.FromResult(TransferResult.TimeConditionNotMet()));
        fileSystem.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("old");
        Diagnostics.Arrange("handler result", "time condition not met");
        Diagnostics.Arrange("existing out.txt content", "old");

        int exitCode = await RunAsync(["-o", "out.txt", "file:///source"], unmet);

        Diagnostics.Assert("exit code", (int)CurlExitCode.Ok, exitCode);
        Assert.AreEqual((int)CurlExitCode.Ok, exitCode);
        Diagnostics.Assert("out.txt written", false, fileSystem.Written.ContainsKey("out.txt"));
        Assert.IsFalse(fileSystem.Written.ContainsKey("out.txt"));
        Diagnostics.Diff("existing out.txt content", "old", Encoding.ASCII.GetString(fileSystem.ExistingContent["out.txt"]));
        Assert.AreEqual("old", Encoding.ASCII.GetString(fileSystem.ExistingContent["out.txt"]));
    }

    [TestMethod]
    public async Task RunAsync_EmptyTransferToUncreatableOutputFile_ReturnsExit23WithNoLine()
    {
        RecordingProtocolHandler empty = new("file", _ => ValueTask.FromResult(TransferResult.Success(0)));
        fileSystem.UnwritablePaths.Add("Z:/nonexist/x");
        Diagnostics.Arrange("unwritable paths", "Z:/nonexist/x");

        int exitCode = await RunAsync(["-sS", "-o", "Z:/nonexist/x", "file:///empty"], empty);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferToOutputFile_DoesNotCreateTheFile()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.Failing(
            "file", CurlExitCode.FileCouldntReadFile, "Could not open file /nonexist/a");

        int exitCode = await RunAsync(["-o", "a.txt", "file:///nonexist/a"], file);

        Diagnostics.Assert("exit code", 37, exitCode);
        Assert.AreEqual(37, exitCode);
        Diagnostics.Assert("a.txt written", false, fileSystem.Written.ContainsKey("a.txt"));
        Assert.IsFalse(fileSystem.Written.ContainsKey("a.txt"));
    }

    [TestMethod]
    public async Task RunAsync_ParserRefusal_PrintsEachLineWithNewLineAndRunsNoHandler()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync(["--no-such-option", "file:///a"], file);

        Diagnostics.Assert("exit code", (int)CurlExitCode.FailedInit, exitCode);
        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: option --no-such-option: is unknown\n" + CommandLineRefusal.TryHelpLine + "\n",
            Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: option --no-such-option: is unknown" + NewLine
            + CommandLineRefusal.TryHelpLine + NewLine,
            StandardErrorText);
        Diagnostics.Assert("file contexts", 0, file.Contexts.Count);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_EmptyCommandLine_PrintsOnlyTheTryHelpLineRunsNoHandlerAndReturns2()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync([], file);

        Diagnostics.Assert("exit code", 2, exitCode);
        Assert.AreEqual(2, exitCode);
        Diagnostics.Assert("stdout length", 0L, standardOutput.Length);
        Assert.AreEqual(0, standardOutput.Length);
        Diagnostics.Diff("stderr", CommandLineRefusal.TryHelpLine + "\n", Lf(StandardErrorText));
        Assert.AreEqual(CommandLineRefusal.TryHelpLine + NewLine, StandardErrorText);
        Diagnostics.Assert("file contexts", 0, file.Contexts.Count);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_FailureWithoutMessage_PrintsNoLine()
    {
        RecordingProtocolHandler file = new(
            "file", _ => ValueTask.FromResult(new TransferResult(CurlExitCode.ReadError, 0)));
        Diagnostics.Arrange("handler result", "read error without a message");

        int exitCode = await RunAsync(["file:///a"], file);

        Diagnostics.Assert("exit code", 26, exitCode);
        Assert.AreEqual(26, exitCode);
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
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
        Diagnostics.Diff("post data", "payload", Encoding.UTF8.GetString(context.PostData!.Value.Span));
        Assert.AreEqual("payload", Encoding.UTF8.GetString(context.PostData!.Value.Span));
        Diagnostics.Assert("user name", "user", context.Credentials!.UserName);
        Assert.AreEqual("user", context.Credentials!.UserName);
        Diagnostics.Assert("password", "secret", context.Credentials.Password);
        Assert.AreEqual("secret", context.Credentials.Password);
        Diagnostics.Assert("telnet options", "TTYPE=vt100, XDISPLOC=x:0", string.Join(", ", context.TelnetOptions));
        CollectionAssert.AreEqual(new[] { "TTYPE=vt100", "XDISPLOC=x:0" }, context.TelnetOptions.ToArray());
        Diagnostics.Assert("tftp block size", 1024, context.TftpBlockSize);
        Assert.AreEqual(1024, context.TftpBlockSize);
        Diagnostics.Assert("tftp no options", true, context.TftpNoOptions);
        Assert.IsTrue(context.TftpNoOptions);
        Diagnostics.Assert("create file mode", UnixFileMode.UserRead | UnixFileMode.UserWrite, context.CreateFileMode);
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, context.CreateFileMode);
        Diagnostics.Assert("url", CurlUrl.Parse("file:///a"), context.Url);
        Assert.AreEqual(CurlUrl.Parse("file:///a"), context.Url);
    }

    [TestMethod]
    public async Task RunAsync_NoProtocolOptions_ContextCarriesNotGivenValues()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync(["file:///a"], file);

        ITransferContext context = file.Contexts.Single();
        Diagnostics.Assert("post data is null", true, context.PostData is null);
        Assert.IsNull(context.PostData);
        Diagnostics.Assert("credentials are null", true, context.Credentials is null);
        Assert.IsNull(context.Credentials);
        Diagnostics.Assert("telnet options", string.Empty, string.Join(", ", context.TelnetOptions));
        Assert.IsEmpty(context.TelnetOptions);
        Diagnostics.Assert("tftp block size is null", true, context.TftpBlockSize is null);
        Assert.IsNull(context.TftpBlockSize);
        Diagnostics.Assert("tftp no options", false, context.TftpNoOptions);
        Assert.IsFalse(context.TftpNoOptions);
        Diagnostics.Assert("create file mode", TransferContext.DefaultCreateFileMode, context.CreateFileMode);
        Assert.AreEqual(TransferContext.DefaultCreateFileMode, context.CreateFileMode);
        Diagnostics.Diff("stdout", "/a", StandardOutputText);
        Assert.AreEqual("/a", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ConnectTimeoutAndMaxTime_ReachTheHandlersContext()
    {
        RecordingProtocolHandler tftp = RecordingProtocolHandler.WritingPath("tftp");

        await RunAsync(["--connect-timeout", "10", "-m", "20", "tftp://127.0.0.1/f"], tftp);

        ITransferContext context = tftp.Contexts.Single();
        Diagnostics.Assert("connect timeout", TimeSpan.FromSeconds(10), context.ConnectTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(10), context.ConnectTimeout);
        Diagnostics.Assert("max time", TimeSpan.FromSeconds(20), context.MaxTime);
        Assert.AreEqual(TimeSpan.FromSeconds(20), context.MaxTime);
    }

    [TestMethod]
    public async Task RunAsync_NoConnectTimeoutOrMaxTime_ContextCarriesNull()
    {
        RecordingProtocolHandler tftp = RecordingProtocolHandler.WritingPath("tftp");

        await RunAsync(["tftp://127.0.0.1/f"], tftp);

        ITransferContext context = tftp.Contexts.Single();
        Diagnostics.Assert("connect timeout is null", true, context.ConnectTimeout is null);
        Assert.IsNull(context.ConnectTimeout);
        Diagnostics.Assert("max time is null", true, context.MaxTime is null);
        Assert.IsNull(context.MaxTime);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputWriteThrows_ReturnsExit23WithFailedWritingBodyLine()
    {
        FailingWriteStream closed = new();
        Diagnostics.Arrange("standard output", "a stream whose writes throw");

        int exitCode = await RunWithStandardOutputAsync(closed, ["file:///a"], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", "curl: Failed writing body\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: Failed writing body" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputWriteThrowsUnderSilent_PrintsNothingAndReturns23()
    {
        FailingWriteStream closed = new();
        Diagnostics.Arrange("standard output", "a stream whose writes throw");

        int exitCode = await RunWithStandardOutputAsync(closed, ["-s", "file:///a"], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputWriteThrowsUnderSilentShowError_PrintsFailedWritingBodyLine()
    {
        FailingWriteStream closed = new();
        Diagnostics.Arrange("standard output", "a stream whose writes throw");

        int exitCode = await RunWithStandardOutputAsync(closed, ["-sS", "file:///a"], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", CurlCommandRunner.FailedWritingBodyLine + "\n", Lf(StandardErrorText));
        Assert.AreEqual(CurlCommandRunner.FailedWritingBodyLine + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_HandlerReportsItsOwnFailureForAThrowingStandardOutput_PrintsFailedWritingBodyLine()
    {
        FailingWriteStream closed = new();
        RecordingProtocolHandler telnet = new("telnet", async context =>
        {
            try
            {
                await context.Output.WriteAsync(new byte[] { 1 });
            }
            catch (IOException)
            {
                return TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed 1 returned 0");
            }

            return TransferResult.Success(1);
        });
        Diagnostics.Arrange("standard output", "a stream whose writes throw");
        Diagnostics.Arrange("handler", "writes 1 byte and reports exit 23 when the write throws");

        int exitCode = await RunWithStandardOutputAsync(closed, ["-sS", "telnet://h/"], telnet);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", "curl: Failed writing body\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: Failed writing body" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputFlushThrows_ReturnsExit23WithFailedWritingBodyLine()
    {
        FailingWriteStream closed = new() { WritesToFail = 0, FailsFlush = true };
        Diagnostics.Arrange("standard output", "a stream whose writes succeed and whose flush throws");

        int exitCode = await RunWithStandardOutputAsync(closed, ["file:///a"], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", "curl: Failed writing body\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: Failed writing body" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputFailsOnlyForTheFirstUrl_ReportsItAndSucceedsOnTheSecond()
    {
        FailingWriteStream flaky = new() { WritesToFail = 1 };
        Diagnostics.Arrange("standard output", "a stream whose first write throws");

        int exitCode = await RunWithStandardOutputAsync(
            flaky, ["file:///first", "file:///second"], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", "curl: Failed writing body\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: Failed writing body" + NewLine, StandardErrorText);
        Diagnostics.Diff("stdout", "/second", Encoding.ASCII.GetString(flaky.ToArray()));
        Assert.AreEqual("/second", Encoding.ASCII.GetString(flaky.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputWriteOfAFullStdioBufferThrows_PrintsTheHandlersWriteFailureLine()
    {
        FailingWriteStream closed = new();
        RecordingProtocolHandler file = new("file", async context =>
        {
            try
            {
                await context.Output.WriteAsync(new byte[StandardOutputFailureDeferringStream.StdioBufferSize]);
            }
            catch (IOException)
            {
                return TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed 4096 returned 0");
            }

            return TransferResult.Success(4096);
        });
        Diagnostics.Arrange("standard output", "a stream whose writes throw");
        Diagnostics.Arrange("handler write size", StandardOutputFailureDeferringStream.StdioBufferSize);

        int exitCode = await RunWithStandardOutputAsync(closed, ["-sS", "file:///a"], file);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", "curl: (23) Failure writing output to destination, passed 4096 returned 0\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (23) Failure writing output to destination, passed 4096 returned 0" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TransferFailsAfterAFailedStandardOutputWrite_PrintsTheTransfersOwnLine()
    {
        FailingWriteStream closed = new();
        RecordingProtocolHandler telnet = new("telnet", async context =>
        {
            await context.Output.WriteAsync(new byte[] { 1 });

            return TransferResult.Failure(CurlExitCode.RecvError, "Failure when receiving data from the peer");
        });
        Diagnostics.Arrange("standard output", "a stream whose writes throw");
        Diagnostics.Arrange("handler", "writes 1 byte then fails with exit 56");

        int exitCode = await RunWithStandardOutputAsync(closed, ["-sS", "telnet://h/"], telnet);

        Diagnostics.Assert("exit code", 56, exitCode);
        Assert.AreEqual(56, exitCode);
        Diagnostics.Diff("stderr", "curl: (56) Failure when receiving data from the peer\n", Lf(StandardErrorText));
        Assert.AreEqual("curl: (56) Failure when receiving data from the peer" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TelnetUrl_ReceivesStandardInputAsUpload()
    {
        RecordingProtocolHandler telnet = RecordingProtocolHandler.WritingPath("telnet");

        await RunAsync(["telnet://h/"], telnet);

        Diagnostics.Assert("upload is standard input", true, ReferenceEquals(standardInput, telnet.Contexts.Single().Upload));
        Assert.AreSame(standardInput, telnet.Contexts.Single().Upload);
    }

    [TestMethod]
    public async Task RunAsync_DictUrl_ReceivesNullUpload()
    {
        RecordingProtocolHandler dict = RecordingProtocolHandler.WritingPath("dict");

        await RunAsync(["dict://h/d:x"], dict);

        Diagnostics.Assert("upload is null", true, dict.Contexts.Single().Upload is null);
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
                return new TransferDispatch(new ProtocolDispatcher([]));
            },
            fileSystem,
            fileSystem,
            standardOutput,
            standardError,
            standardInput,
            runsOnWindows: false);
        Diagnostics.Arrange("dispatcher factory", "counts its calls");

        await RunRunnerAsync(runner, ["--no-such-option", "file:///a"], standardOutput);

        Diagnostics.Assert("dispatcher factory calls", 0, calls);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task RunAsync_MoreOutputOptionsThanUrlsAndTheTransferFails_PrintsTheWarningAfterTheErrorLine()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.Failing(
            "file", CurlExitCode.FileCouldntReadFile, "Could not open file /nx");

        int exitCode = await RunAsync(["-o", "f", "-o", "g", "file:///nx"], file);

        Diagnostics.Assert("exit code", 37, exitCode);
        Assert.AreEqual(37, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: (37) Could not open file /nx\nWarning: Got more output options than URLs\n",
            Lf(StandardErrorText));
        Assert.AreEqual(
            "curl: (37) Could not open file /nx" + NewLine
            + "Warning: Got more output options than URLs" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_MoreOutputOptionsThanUrlsAndTheTransferSucceeds_PrintsTheWarningAfterTheTransfer()
    {
        RecordingProtocolHandler file = new("file", async context =>
        {
            Diagnostics.Assert("stderr length during the transfer", 0L, standardError.Length);
            Assert.AreEqual(0, standardError.Length);
            await context.Output.WriteAsync(new byte[] { 1 });

            return TransferResult.Success(1);
        });
        Diagnostics.Arrange("handler", "checks stderr is empty, then writes 1 byte");

        int exitCode = await RunAsync(["-o", "f", "-o", "g", "file:///a"], file);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", "Warning: Got more output options than URLs\n", Lf(StandardErrorText));
        Assert.AreEqual("Warning: Got more output options than URLs" + NewLine, StandardErrorText);
        Diagnostics.Assert("f length", 1, fileSystem.Written["f"].ToArray().Length);
        Assert.HasCount(1, fileSystem.Written["f"].ToArray());
    }

    [TestMethod]
    public async Task RunAsync_OneOutputOptionForOneUrl_PrintsNoWarning()
    {
        int exitCode = await RunAsync(["-o", "f", "file:///a"], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string Shown(string text) =>
        text.Length <= ShownCharacterCap
            ? text
            : string.Create(CultureInfo.InvariantCulture, $"{text[..ShownCharacterCap]}... ({text.Length - ShownCharacterCap} more characters)");

    private static string Schemes(IProtocolHandler[] handlers) =>
        string.Join(", ", handlers.SelectMany(handler => handler.SupportedSchemes));

    private string WrittenText(string path) => Encoding.ASCII.GetString(fileSystem.Written[path].ToArray());

    private Task<int> RunAsync(IReadOnlyList<string> arguments, params IProtocolHandler[] handlers)
    {
        Diagnostics.Arrange("handler schemes", Schemes(handlers));

        return RunRunnerAsync(
            new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher(handlers)), fileSystem, fileSystem, standardOutput, standardError, standardInput, runsOnWindows: false),
            arguments,
            standardOutput);
    }

    private Task<int> RunWithStandardOutputAsync(
        Stream output,
        IReadOnlyList<string> arguments,
        params IProtocolHandler[] handlers)
    {
        Diagnostics.Arrange("handler schemes", Schemes(handlers));

        return RunRunnerAsync(
            new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher(handlers)), fileSystem, fileSystem, output, standardError, standardInput, runsOnWindows: false),
            arguments,
            output);
    }

    private Task<int> RunToUncreatableOutputFileAsync(params string[] options)
    {
        InMemoryFileSystem files = new() { ReadContent = new byte[92] };
        files.UnwritablePaths.Add("Z:/nonexist/x");
        Diagnostics.Arrange("read content length", 92);
        Diagnostics.Arrange("unwritable paths", "Z:/nonexist/x");
        CurlCommandRunner runner = new(
            _ => new TransferDispatch(new ProtocolDispatcher([new FileProtocolHandler(files)])),
            files,
            files,
            standardOutput,
            standardError,
            standardInput,
            runsOnWindows: false);

        return RunRunnerAsync(runner, [.. options, "file:///Windows/win.ini"], standardOutput);
    }

    private async Task<int> RunRunnerAsync(CurlCommandRunner runner, IReadOnlyList<string> arguments, Stream output)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments.Select(Shown)));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await runner.RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        if (output is MemoryStream written)
        {
            Diagnostics.Bytes("stdout", written.ToArray());
        }

        Diagnostics.Act("stderr", Shown(Lf(StandardErrorText)));
        return exitCode;
    }
}
