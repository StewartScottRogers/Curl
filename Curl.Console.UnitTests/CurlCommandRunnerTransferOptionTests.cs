using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how the runner hands <c>-r</c> / <c>--range</c>, <c>-C</c> / <c>--continue-at</c> and
/// <c>--max-filesize</c> to each transfer, against curl 8.21.0 measured on Windows on
/// 2026-09-26 with a ten-byte <c>file://</c> source holding <c>0123456789</c>.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTransferOptionTests
{
    private const string SourceUrl = "file:///source.txt";

    private const string SshUrl = "sftp://127.0.0.1/source.txt";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly FileProtocolHandler fileHandler =
        new(new InMemoryFileSystem { ReadContent = Encoding.ASCII.GetBytes("0123456789") });

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_Range_WritesOnlyThoseBytes()
    {
        int exitCode = await RunAsync(["-r", "0-4", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "01234", StandardOutputText);
        Assert.AreEqual("01234", StandardOutputText);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    [DataRow("3-1")]
    [DataRow("-0")]
    public async Task RunAsync_SshRangeThatNamesNoRange_DispatchesTheTextWithNoRange(string rangeText)
    {
        RecordingProtocolHandler sftp = RecordingProtocolHandler.WritingPath("sftp");

        int exitCode = await RunAsync(["-s", "-k", "-r", rangeText, SshUrl], sftp);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, StandardErrorText);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardErrorText);
        ITransferContext context = sftp.Contexts.Single();
        Diagnostics.Assert("range text", rangeText, context.RangeText);
        Assert.AreEqual(rangeText, context.RangeText);
        Diagnostics.Assert("range", "null", context.Range?.ToString() ?? "null");
        Assert.IsNull(context.Range);
    }

    [TestMethod]
    [DataRow("http", "abc")]
    [DataRow("https", "-0")]
    [DataRow("http", "3-1")]
    public async Task RunAsync_HttpRangeThatNamesNoRange_DispatchesTheTextAsTyped(string scheme, string rangeText)
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath(scheme);

        int exitCode = await RunAsync(["-s", "-r", rangeText, scheme + "://127.0.0.1/f"], http);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardErrorText);
        ITransferContext context = http.Contexts.Single();
        Diagnostics.Assert("range text", rangeText, context.RangeText);
        Assert.AreEqual(rangeText, context.RangeText);
        Diagnostics.Assert("range", "null", context.Range?.ToString() ?? "null");
        Assert.IsNull(context.Range);
    }

    [TestMethod]
    public async Task RunAsync_RangeList_DispatchesTheTextAsTypedAndTheFirstRangeParsed()
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

        int exitCode = await RunAsync(["-r", "0-9,20-29", "http://127.0.0.1/f"], http);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        ITransferContext context = http.Contexts.Single();
        Diagnostics.Assert("range text", "0-9,20-29", context.RangeText);
        Assert.AreEqual("0-9,20-29", context.RangeText);
        Diagnostics.Assert("range", ByteRange.Bounded(0, 9), context.Range);
        Assert.AreEqual(ByteRange.Bounded(0, 9), context.Range);
    }

    [TestMethod]
    public async Task RunAsync_SshRangeWithInvalidCharacter_PrintsTheWarningThenDispatchesTheText()
    {
        RecordingProtocolHandler sftp = RecordingProtocolHandler.WritingPath("sftp");

        int exitCode = await RunAsync(["-k", "-r", "abc", SshUrl], sftp);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode, StandardErrorText);
        Diagnostics.Assert(
            "stderr starts with the invalid-character warning",
            true,
            Normalized(StandardErrorText).StartsWith(
                "Warning: Invalid character is found in given range. A specified range MUST \n"
                + "Warning: have only digits in 'start'-'stop'. The server's response to this \n"
                + "Warning: request is uncertain.\n",
                StringComparison.Ordinal));
        Assert.StartsWith(
            Lines(
                "Warning: Invalid character is found in given range. A specified range MUST ",
                "Warning: have only digits in 'start'-'stop'. The server's response to this ",
                "Warning: request is uncertain."),
            StandardErrorText);
        Diagnostics.Assert("range text", "abc", sftp.Contexts.Single().RangeText);
        Assert.AreEqual("abc", sftp.Contexts.Single().RangeText);
    }

    [TestMethod]
    public async Task RunAsync_RangeThatNamesNoRangeToOutputFile_CreatesNoFile()
    {
        int exitCode = await RunAsync(["-r", "3-1", "-o", "out.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 33, exitCode);
        Assert.AreEqual(33, exitCode);
        Diagnostics.Assert("out.txt written", false, outputFiles.Written.ContainsKey("out.txt"));
        Assert.IsFalse(outputFiles.Written.ContainsKey("out.txt"));
    }

    [TestMethod]
    public async Task RunAsync_RangeWithNoDash_PrintsTheWarningBeforeAnyTransferOutput()
    {
        long standardErrorLengthAtDispatch = -1;
        RecordingProtocolHandler file = new("file", async context =>
        {
            standardErrorLengthAtDispatch = standardError.Length;
            await context.Output.WriteAsync("56789"u8.ToArray(), context.CancellationToken);

            return TransferResult.Success(5);
        });

        int exitCode = await RunAsync(["-r", "5abc", SourceUrl], file);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string warning = Lines(
            "Warning: A specified range MUST include at least one dash (-). Appending one ",
            "Warning: for you");
        Diagnostics.Diff("stderr", Normalized(warning), Normalized(StandardErrorText));
        Assert.AreEqual(warning, StandardErrorText);
        Diagnostics.Assert("stderr bytes at dispatch", Encoding.UTF8.GetByteCount(warning), standardErrorLengthAtDispatch);
        Assert.AreEqual(Encoding.UTF8.GetByteCount(warning), standardErrorLengthAtDispatch);
        Diagnostics.Assert("range", ByteRange.FromOffset(5), file.Contexts.Single().Range);
        Assert.AreEqual(ByteRange.FromOffset(5), file.Contexts.Single().Range);
    }

    [TestMethod]
    public async Task RunAsync_RefusedCommandLine_PrintsTheWarningBeforeTheRefusal()
    {
        int exitCode = await RunAsync(["-r", "abc", "--bogus", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", (int)CurlExitCode.FailedInit, exitCode);
        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Invalid character is found in given range. A specified range MUST \n"
            + "Warning: have only digits in 'start'-'stop'. The server's response to this \n"
            + "Warning: request is uncertain.\n"
            + "curl: option --bogus: is unknown\n"
            + CommandLineRefusal.TryHelpLine + "\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            Lines(
                "Warning: Invalid character is found in given range. A specified range MUST ",
                "Warning: have only digits in 'start'-'stop'. The server's response to this ",
                "Warning: request is uncertain.")
            + "curl: option --bogus: is unknown" + NewLine
            + CommandLineRefusal.TryHelpLine + NewLine,
            StandardErrorText);
    }

    // Measured with COLUMNS=200 and COLUMNS=40 curl -r abc --bogus x and curl -r 5 --bogus x
    // (curl 8.21.0, Windows, 2026-09-27).
    [TestMethod]
    public async Task RunAsync_RangeWarningsAt200Columns_AreEachOneLine()
    {
        int exitCode = await RunAsync(["-r", "abc", "-r", "5", "--bogus", SourceUrl], fileHandler, terminalColumns: 200);

        Diagnostics.Assert("exit code", (int)CurlExitCode.FailedInit, exitCode);
        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Invalid character is found in given range. A specified range MUST have only digits in 'start'-'stop'. The server's response to this request is uncertain.\n"
            + "Warning: A specified range MUST include at least one dash (-). Appending one for you\n"
            + "curl: option --bogus: is unknown\n"
            + CommandLineRefusal.TryHelpLine + "\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            Lines(
                "Warning: Invalid character is found in given range. A specified range MUST have only digits in 'start'-'stop'. The server's response to this request is uncertain.",
                "Warning: A specified range MUST include at least one dash (-). Appending one for you",
                "curl: option --bogus: is unknown",
                CommandLineRefusal.TryHelpLine),
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RangeWarningsAt40Columns_AreWrappedAsCurlWrapsThem()
    {
        int exitCode = await RunAsync(["-r", "abc", "-r", "5", "--bogus", SourceUrl], fileHandler, terminalColumns: 40);

        Diagnostics.Assert("exit code", (int)CurlExitCode.FailedInit, exitCode);
        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Invalid character is found in \n"
            + "Warning: given range. A specified range \n"
            + "Warning: MUST have only digits in \n"
            + "Warning: 'start'-'stop'. The server's \n"
            + "Warning: response to this request is \n"
            + "Warning: uncertain.\n"
            + "Warning: A specified range MUST include \n"
            + "Warning: at least one dash (-). \n"
            + "Warning: Appending one for you\n"
            + "curl: option --bogus: is unknown\n"
            + CommandLineRefusal.TryHelpLine + "\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            Lines(
                "Warning: Invalid character is found in ",
                "Warning: given range. A specified range ",
                "Warning: MUST have only digits in ",
                "Warning: 'start'-'stop'. The server's ",
                "Warning: response to this request is ",
                "Warning: uncertain.",
                "Warning: A specified range MUST include ",
                "Warning: at least one dash (-). ",
                "Warning: Appending one for you",
                "curl: option --bogus: is unknown",
                CommandLineRefusal.TryHelpLine),
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_MaxFileSizeBelowTheBody_WritesTheLimitAndReturnsExit63()
    {
        int exitCode = await RunAsync(["--max-filesize", "9", "-o", "out.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 63, exitCode);
        Assert.AreEqual(63, exitCode);
        Diagnostics.Diff("out.txt", "012345678", WrittenText("out.txt"));
        Assert.AreEqual("012345678", WrittenText("out.txt"));
        Diagnostics.Diff("stderr", "curl: (63) Exceeded the maximum allowed file size (9) with 9 bytes\n", Normalized(StandardErrorText));
        Assert.AreEqual("curl: (63) Exceeded the maximum allowed file size (9) with 9 bytes" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_NoTransferOptions_ContextCarriesNone()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync([SourceUrl], file);

        ITransferContext context = file.Contexts.Single();
        Diagnostics.Assert("range", "null", context.Range?.ToString() ?? "null");
        Assert.IsNull(context.Range);
        Diagnostics.Assert("resume from", "null", context.ResumeFrom?.ToString() ?? "null");
        Assert.IsNull(context.ResumeFrom);
        Diagnostics.Assert("max file size", "null", context.MaxFileSize?.ToString() ?? "null");
        Assert.IsNull(context.MaxFileSize);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtToStandardOutput_WritesFromTheOffset()
    {
        int exitCode = await RunAsync(["-C", "5", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "56789", StandardOutputText);
        Assert.AreEqual("56789", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtToExistingOutputFile_AppendsFromTheOffset()
    {
        outputFiles.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("XYZ");

        int exitCode = await RunAsync(["-C", "5", "-o", "out.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("out.txt", "XYZ56789", WrittenText("out.txt"));
        Assert.AreEqual("XYZ56789", WrittenText("out.txt"));
        Diagnostics.Assert("write modes", FileWriteMode.Append, string.Join(", ", outputFiles.WriteModes));
        CollectionAssert.AreEqual(new[] { FileWriteMode.Append }, outputFiles.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtZeroToExistingOutputFile_ReplacesIt()
    {
        outputFiles.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("XYZ");

        int exitCode = await RunAsync(["-C", "0", "-o", "out.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("out.txt", "0123456789", WrittenText("out.txt"));
        Assert.AreEqual("0123456789", WrittenText("out.txt"));
        Diagnostics.Assert("write modes", FileWriteMode.Truncate, string.Join(", ", outputFiles.WriteModes));
        CollectionAssert.AreEqual(new[] { FileWriteMode.Truncate }, outputFiles.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtOutputSize_ResumesFromTheOutputFilesSize()
    {
        outputFiles.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("ABCD");

        int exitCode = await RunAsync(["-C", "-", "-o", "out.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("out.txt", "ABCD456789", WrittenText("out.txt"));
        Assert.AreEqual("ABCD456789", WrittenText("out.txt"));
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtOutputSizeWithMissingOutputFile_TransfersTheWholeResource()
    {
        outputFiles.UnreadablePaths.Add("out.txt");

        int exitCode = await RunAsync(["-C", "-", "-o", "out.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("out.txt", "0123456789", WrittenText("out.txt"));
        Assert.AreEqual("0123456789", WrittenText("out.txt"));
        Diagnostics.Assert("write modes", FileWriteMode.Truncate, string.Join(", ", outputFiles.WriteModes));
        CollectionAssert.AreEqual(new[] { FileWriteMode.Truncate }, outputFiles.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtOutputSizeLargerThanTheSource_ReturnsExit36()
    {
        outputFiles.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("0123456789AB");

        int exitCode = await RunAsync(["-C", "-", "-o", "out.txt", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 36, exitCode);
        Assert.AreEqual(36, exitCode);
        Diagnostics.Diff("out.txt", "0123456789AB", WrittenText("out.txt"));
        Assert.AreEqual("0123456789AB", WrittenText("out.txt"));
        Diagnostics.Diff("stderr", "curl: (36) failed to resume file:// transfer\n", Normalized(StandardErrorText));
        Assert.AreEqual("curl: (36) failed to resume file:// transfer" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtOutputSizeToStandardOutput_TransfersTheWholeResource()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync(["-C", "-", SourceUrl], file);

        Diagnostics.Assert("resume from", "null", file.Contexts.Single().ResumeFrom?.ToString() ?? "null");
        Assert.IsNull(file.Contexts.Single().ResumeFrom);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtToUnopenableOutputFile_PrintsCannotOpenAndReturnsExit23()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");
        outputFiles.UnwritablePaths.Add("d");

        int exitCode = await RunAsync(["-C", "3", "-o", "d", SourceUrl], file);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: cannot open 'd'\ncurl: (23) Failed writing received data to disk/application\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "curl: cannot open 'd'" + NewLine
            + "curl: (23) Failed writing received data to disk/application" + NewLine,
            StandardErrorText);
        Diagnostics.Assert("dispatched contexts", 0, file.Contexts.Count);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtToUnopenableOutputFile_StopsBeforeTheRemainingUrls()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");
        outputFiles.UnwritablePaths.Add("d");

        int exitCode = await RunAsync(["-C", "3", SourceUrl, SourceUrl, "-o", "d", "-o", "o9.txt"], file);

        Diagnostics.Assert("exit code", (int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: cannot open 'd'\ncurl: (23) Failed writing received data to disk/application\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "curl: cannot open 'd'" + NewLine
            + "curl: (23) Failed writing received data to disk/application" + NewLine,
            StandardErrorText);
        Diagnostics.Assert("dispatched contexts", 0, file.Contexts.Count);
        Assert.IsEmpty(file.Contexts);
        Diagnostics.Assert("o9.txt written", false, outputFiles.Written.ContainsKey("o9.txt"));
        Assert.IsFalse(outputFiles.Written.ContainsKey("o9.txt"));
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtToUnopenableOutputFileUnderSilent_PrintsNothing()
    {
        outputFiles.UnwritablePaths.Add("d");

        int exitCode = await RunAsync(["-s", "-C", "3", "-o", "d", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TimeCond_PassesConditionToHandler()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync(["-z", "-1 Jan 2000", SourceUrl], file);

        Diagnostics.Assert(
            "time condition",
            new TimeCondition(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfUnmodifiedSince),
            file.Contexts.Single().TimeCondition);
        Assert.AreEqual(
            new TimeCondition(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfUnmodifiedSince),
            file.Contexts.Single().TimeCondition);
    }

    [TestMethod]
    public async Task RunAsync_NoTimeCond_PassesNoConditionToHandler()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync([SourceUrl], file);

        Diagnostics.Assert("time condition", "null", file.Contexts.Single().TimeCondition?.ToString() ?? "null");
        Assert.IsNull(file.Contexts.Single().TimeCondition);
    }

    /// <summary>
    /// curl 8.21.0, <c>curl -z notadate -o NUL file:///Z:/repos/Curl.lanes/lane-3/global.json</c>
    /// (Windows, 2026-09-26): the two warning lines on standard error, then the transfer with no
    /// condition, exit 0.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_TimeCondNotADateOnWindows_WarnsAndTransfersUnconditionally()
    {
        int exitCode = await RunAsync(["-z", "notadate", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "0123456789", StandardOutputText);
        Assert.AreEqual("0123456789", StandardOutputText);
        Diagnostics.Diff(
            "stderr",
            "Warning: Illegal date format for -z, --time-cond (and not a filename). \n"
            + "Warning: Disabling time condition. See curl_getdate(3) for valid date syntax.\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "Warning: Illegal date format for -z, --time-cond (and not a filename). " + NewLine
            + "Warning: Disabling time condition. See curl_getdate(3) for valid date syntax." + NewLine,
            StandardErrorText);
    }

    // Measured with COLUMNS=200 and COLUMNS=40 curl -z notadate -o NUL file:///Z:/.../global.json
    // (curl 8.21.0, Windows, 2026-09-27).
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_TimeCondNotADateAt200ColumnsOnWindows_WarnsOnOneLine()
    {
        int exitCode = await RunAsync(["-z", "notadate", SourceUrl], fileHandler, terminalColumns: 200);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Illegal date format for -z, --time-cond (and not a filename). Disabling time condition. See curl_getdate(3) for valid date syntax.\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            Lines("Warning: Illegal date format for -z, --time-cond (and not a filename). Disabling time condition. See curl_getdate(3) for valid date syntax."),
            StandardErrorText);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_TimeCondNotADateAt40ColumnsOnWindows_WrapsTheWarningAsCurlWrapsIt()
    {
        int exitCode = await RunAsync(["-z", "notadate", SourceUrl], fileHandler, terminalColumns: 40);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Illegal date format for -z, \n"
            + "Warning: --time-cond (and not a \n"
            + "Warning: filename). Disabling time \n"
            + "Warning: condition. See curl_getdate(3) \n"
            + "Warning: for valid date syntax.\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            Lines(
                "Warning: Illegal date format for -z, ",
                "Warning: --time-cond (and not a ",
                "Warning: filename). Disabling time ",
                "Warning: condition. See curl_getdate(3) ",
                "Warning: for valid date syntax."),
            StandardErrorText);
    }

    // Off Windows, curl first tries the -z value as a file name: getfiletime in src/tool_filetime.c
    // warns "Failed to get filetime: " + strerror(errno) when stat fails, before src/tool_getparam.c
    // warns the value is not a date either. Texts from CI run 36376508151 (ubuntu-latest, macos-latest).
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_TimeCondNotADateOffWindows_WarnsNoFileTimeThenNotADateAndTransfersUnconditionally()
    {
        int exitCode = await RunAsync(["-z", "notadate", SourceUrl], fileHandler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "0123456789", StandardOutputText);
        Assert.AreEqual("0123456789", StandardOutputText);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to get filetime: No such file or directory\n"
            + "Warning: Illegal date format for -z, --time-cond (and not a filename). \n"
            + "Warning: Disabling time condition. See curl_getdate(3) for valid date syntax.\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            Lines(
                "Warning: Failed to get filetime: No such file or directory",
                "Warning: Illegal date format for -z, --time-cond (and not a filename). ",
                "Warning: Disabling time condition. See curl_getdate(3) for valid date syntax."),
            StandardErrorText);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_TimeCondNotADateAt200ColumnsOffWindows_WarnsNoFileTimeThenNotADateOnOneLine()
    {
        int exitCode = await RunAsync(["-z", "notadate", SourceUrl], fileHandler, terminalColumns: 200);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to get filetime: No such file or directory\n"
            + "Warning: Illegal date format for -z, --time-cond (and not a filename). Disabling time condition. See curl_getdate(3) for valid date syntax.\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            Lines(
                "Warning: Failed to get filetime: No such file or directory",
                "Warning: Illegal date format for -z, --time-cond (and not a filename). Disabling time condition. See curl_getdate(3) for valid date syntax."),
            StandardErrorText);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_TimeCondNotADateAt40ColumnsOffWindows_WrapsBothWarningsAsCurlWrapsThem()
    {
        int exitCode = await RunAsync(["-z", "notadate", SourceUrl], fileHandler, terminalColumns: 40);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: Failed to get filetime: No \n"
            + "Warning: such file or directory\n"
            + "Warning: Illegal date format for -z, \n"
            + "Warning: --time-cond (and not a \n"
            + "Warning: filename). Disabling time \n"
            + "Warning: condition. See curl_getdate(3) \n"
            + "Warning: for valid date syntax.\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            Lines(
                "Warning: Failed to get filetime: No ",
                "Warning: such file or directory",
                "Warning: Illegal date format for -z, ",
                "Warning: --time-cond (and not a ",
                "Warning: filename). Disabling time ",
                "Warning: condition. See curl_getdate(3) ",
                "Warning: for valid date syntax."),
            StandardErrorText);
    }

    [TestMethod]
    [DataRow(false, "a\nb\r\nc\n")]
    [DataRow(true, "a\r\nb\r\nc\r\n")]
    public async Task RunAsync_UploadToFileUrl_ConvertsLineEndingsOnlyUnderCrlf(bool crlf, string expected)
    {
        outputFiles.ExistingContent["up.txt"] = Encoding.ASCII.GetBytes("a\nb\r\nc\n");
        InMemoryFileSystem destination = new();
        string[] arguments = crlf ? ["-T", "up.txt", "--crlf", "file:///dir/x"] : ["-T", "up.txt", "file:///dir/x"];

        int exitCode = await RunAsync(arguments, new FileProtocolHandler(destination));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff(
            "uploaded bytes",
            Encoding.ASCII.GetBytes(expected).AsSpan(),
            destination.Written.Values.Single().ToArray().AsSpan());
        Assert.AreEqual(expected, Encoding.ASCII.GetString(destination.Written.Values.Single().ToArray()));
    }

    private static string Lines(params string[] lines) => string.Concat(lines.Select(line => line + NewLine));

    private string WrittenText(string path) => Encoding.ASCII.GetString(outputFiles.Written[path].ToArray());

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler, int terminalColumns = TerminalColumns.Default)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("handler", handler.GetType().Name);
        Diagnostics.Arrange("terminal columns", terminalColumns);
        Diagnostics.Arrange("unreadable paths", string.Join(", ", outputFiles.UnreadablePaths));
        Diagnostics.Arrange("unwritable paths", string.Join(", ", outputFiles.UnwritablePaths));
        foreach (KeyValuePair<string, byte[]> existing in outputFiles.ExistingContent)
        {
            Diagnostics.Bytes("existing output file " + existing.Key, existing.Value);
        }

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows: false, terminalColumns)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout bytes", standardOutput.ToArray());
        Diagnostics.Act("stderr", Normalized(StandardErrorText));
        foreach (string path in outputFiles.Written.Keys)
        {
            Diagnostics.Bytes("written " + path, outputFiles.Written[path].ToArray());
        }

        return exitCode;
    }
}
