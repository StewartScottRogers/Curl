using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;

namespace Curl.Console;

/// <summary>
/// Pins how the runner hands <c>-r</c> / <c>--range</c>, <c>-C</c> / <c>--continue-at</c> and
/// <c>--max-filesize</c> to each transfer, against curl 8.21.0 measured on Windows on
/// 2026-09-26 with a ten-byte <c>file://</c> source holding <c>0123456789</c>.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTransferOptionTests
{
    private const string SourceUrl = "file:///C:/source.txt";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string NotDeliveredLine = "curl: (33) " + ByteRangeParser.NotDeliveredMessage + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly FileProtocolHandler fileHandler =
        new(new InMemoryFileSystem { ReadContent = Encoding.ASCII.GetBytes("0123456789") });

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_Range_WritesOnlyThoseBytes()
    {
        int exitCode = await RunAsync(["-r", "0-4", SourceUrl], fileHandler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("01234", StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    [DataRow("3-1")]
    [DataRow("-0")]
    public async Task RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching(string rangeText)
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync(["-r", rangeText, SourceUrl], file);

        Assert.AreEqual(33, exitCode);
        Assert.AreEqual(NotDeliveredLine, StandardErrorText);
        Assert.IsEmpty(file.Contexts);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_RangeWithInvalidCharacter_PrintsTheWarningThenExit33()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        int exitCode = await RunAsync(["-r", "abc", SourceUrl], file);

        Assert.AreEqual(33, exitCode);
        Assert.AreEqual(Lines(CommandLineWarning.RangeHasInvalidCharacter) + NotDeliveredLine, StandardErrorText);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_RangeThatNamesNoRangeToOutputFile_CreatesNoFile()
    {
        int exitCode = await RunAsync(["-r", "3-1", "-o", "out.txt", SourceUrl], fileHandler);

        Assert.AreEqual(33, exitCode);
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

        Assert.AreEqual(0, exitCode);
        string warning = Lines(CommandLineWarning.RangeHasNoDash);
        Assert.AreEqual(warning, StandardErrorText);
        Assert.AreEqual(Encoding.UTF8.GetByteCount(warning), standardErrorLengthAtDispatch);
        Assert.AreEqual(ByteRange.FromOffset(5), file.Contexts.Single().Range);
    }

    [TestMethod]
    public async Task RunAsync_RefusedCommandLine_PrintsTheWarningBeforeTheRefusal()
    {
        int exitCode = await RunAsync(["-r", "abc", "--bogus", SourceUrl], fileHandler);

        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
        Assert.AreEqual(
            Lines(CommandLineWarning.RangeHasInvalidCharacter)
            + "curl: option --bogus: is unknown" + NewLine
            + CommandLineRefusal.TryHelpLine + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_MaxFileSizeBelowTheBody_WritesTheLimitAndReturnsExit63()
    {
        int exitCode = await RunAsync(["--max-filesize", "9", "-o", "out.txt", SourceUrl], fileHandler);

        Assert.AreEqual(63, exitCode);
        Assert.AreEqual("012345678", WrittenText("out.txt"));
        Assert.AreEqual("curl: (63) Exceeded the maximum allowed file size (9) with 9 bytes" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_NoTransferOptions_ContextCarriesNone()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync([SourceUrl], file);

        ITransferContext context = file.Contexts.Single();
        Assert.IsNull(context.Range);
        Assert.IsNull(context.ResumeFrom);
        Assert.IsNull(context.MaxFileSize);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtToStandardOutput_WritesFromTheOffset()
    {
        int exitCode = await RunAsync(["-C", "5", SourceUrl], fileHandler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("56789", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtToExistingOutputFile_AppendsFromTheOffset()
    {
        outputFiles.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("XYZ");

        int exitCode = await RunAsync(["-C", "5", "-o", "out.txt", SourceUrl], fileHandler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("XYZ56789", WrittenText("out.txt"));
        CollectionAssert.AreEqual(new[] { FileWriteMode.Append }, outputFiles.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtZeroToExistingOutputFile_ReplacesIt()
    {
        outputFiles.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("XYZ");

        int exitCode = await RunAsync(["-C", "0", "-o", "out.txt", SourceUrl], fileHandler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0123456789", WrittenText("out.txt"));
        CollectionAssert.AreEqual(new[] { FileWriteMode.Truncate }, outputFiles.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtOutputSize_ResumesFromTheOutputFilesSize()
    {
        outputFiles.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("ABCD");

        int exitCode = await RunAsync(["-C", "-", "-o", "out.txt", SourceUrl], fileHandler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ABCD456789", WrittenText("out.txt"));
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtOutputSizeWithMissingOutputFile_TransfersTheWholeResource()
    {
        outputFiles.UnreadablePaths.Add("out.txt");

        int exitCode = await RunAsync(["-C", "-", "-o", "out.txt", SourceUrl], fileHandler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0123456789", WrittenText("out.txt"));
        CollectionAssert.AreEqual(new[] { FileWriteMode.Truncate }, outputFiles.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtOutputSizeLargerThanTheSource_ReturnsExit36()
    {
        outputFiles.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("0123456789AB");

        int exitCode = await RunAsync(["-C", "-", "-o", "out.txt", SourceUrl], fileHandler);

        Assert.AreEqual(36, exitCode);
        Assert.AreEqual("0123456789AB", WrittenText("out.txt"));
        Assert.AreEqual("curl: (36) failed to resume file:// transfer" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtOutputSizeToStandardOutput_TransfersTheWholeResource()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync(["-C", "-", SourceUrl], file);

        Assert.IsNull(file.Contexts.Single().ResumeFrom);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtToUnopenableOutputFile_PrintsCannotOpenAndReturnsExit23()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");
        outputFiles.UnwritablePaths.Add("d");

        int exitCode = await RunAsync(["-C", "3", "-o", "d", SourceUrl], file);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(
            "curl: cannot open 'd'" + NewLine
            + "curl: (23) Failed writing received data to disk/application" + NewLine,
            StandardErrorText);
        Assert.IsEmpty(file.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtToUnopenableOutputFile_StopsBeforeTheRemainingUrls()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");
        outputFiles.UnwritablePaths.Add("d");

        int exitCode = await RunAsync(["-C", "3", SourceUrl, SourceUrl, "-o", "d", "-o", "o9.txt"], file);

        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(
            "curl: cannot open 'd'" + NewLine
            + "curl: (23) Failed writing received data to disk/application" + NewLine,
            StandardErrorText);
        Assert.IsEmpty(file.Contexts);
        Assert.IsFalse(outputFiles.Written.ContainsKey("o9.txt"));
    }

    [TestMethod]
    public async Task RunAsync_ContinueAtToUnopenableOutputFileUnderSilent_PrintsNothing()
    {
        outputFiles.UnwritablePaths.Add("d");

        int exitCode = await RunAsync(["-s", "-C", "3", "-o", "d", SourceUrl], fileHandler);

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TimeCond_PassesConditionToHandler()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync(["-z", "-1 Jan 2000", SourceUrl], file);

        Assert.AreEqual(
            new TimeCondition(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfUnmodifiedSince),
            file.Contexts.Single().TimeCondition);
    }

    [TestMethod]
    public async Task RunAsync_NoTimeCond_PassesNoConditionToHandler()
    {
        RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

        await RunAsync([SourceUrl], file);

        Assert.IsNull(file.Contexts.Single().TimeCondition);
    }

    /// <summary>
    /// curl 8.21.0, <c>curl -z notadate -o NUL file:///Z:/repos/Curl.lanes/lane-3/global.json</c>
    /// (Windows, 2026-09-26): the two warning lines on standard error, then the transfer with no
    /// condition, exit 0.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task RunAsync_TimeCondNotADate_WarnsAndTransfersUnconditionally()
    {
        int exitCode = await RunAsync(["-z", "notadate", SourceUrl], fileHandler);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0123456789", StandardOutputText);
        Assert.AreEqual(
            "Warning: Illegal date format for -z, --time-cond (and not a filename). " + NewLine
            + "Warning: Disabling time condition. See curl_getdate(3) for valid date syntax." + NewLine,
            StandardErrorText);
    }

    private static string Lines(IReadOnlyList<string> lines) => string.Concat(lines.Select(line => line + NewLine));

    private string WrittenText(string path) => Encoding.ASCII.GetString(outputFiles.Written[path].ToArray());

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler) =>
        new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows: false)
            .RunAsync(arguments);
}
