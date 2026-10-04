using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;

namespace Curl.Protocol.File;

/// <summary>
/// Pins the <c>-v</c> line curl 8.21.0 writes for a <c>file://</c> download whose file fails
/// <c>-z</c>, and that <c>-r</c> or a positive <c>-C</c> skips the condition, as curl's
/// <c>file_do</c> does. Measured on 2026-10-03 against a 3-byte file <c>hi\n</c> dated
/// 2001-01-01 (BL-1388).
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerTimeConditionLineTests
{
    private const string ShuttingDown = "* shutting down connection #0";

    private static readonly DateTimeOffset FileDate = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CurlUrl FileUrl => CurlUrl.Parse("file:///dir/f.txt");

    private static string OsPath => "/dir/f.txt".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] Content => Encoding.ASCII.GetBytes("hi\n");

    // curl -sv -z "Jan 1 2020" file:///.../f.txt: stderr "* The requested document is not
    // new enough", "* shutting down connection #0"; nothing on stdout; exit 0.
    [TestMethod]
    public async Task ExecuteAsync_IfModifiedSinceNotMet_ReportsNotNewEnoughAndWritesNothing()
    {
        var condition = new TimeCondition(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfModifiedSince);

        (TransferResult result, MemoryStream output, RecordingTransferEvents events) = await DownloadAsync(condition, FileDate);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(output.ToArray());
        CollectionAssert.AreEqual(new[] { "* The requested document is not new enough", ShuttingDown }, events.Transcript);
    }

    // curl -sv -z "-Jan 1 1999" ...: "* The requested document is not old enough".
    [TestMethod]
    public async Task ExecuteAsync_IfUnmodifiedSinceNotMet_ReportsNotOldEnoughAndWritesNothing()
    {
        var condition = new TimeCondition(new DateTimeOffset(1999, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfUnmodifiedSince);

        (TransferResult result, MemoryStream output, RecordingTransferEvents events) = await DownloadAsync(condition, FileDate);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(output.ToArray());
        CollectionAssert.AreEqual(new[] { "* The requested document is not old enough", ShuttingDown }, events.Transcript);
    }

    // curl -sv -r 0-0 -z "Jan 1 2020" ...: stdout "h", no time condition line.
    [TestMethod]
    public async Task ExecuteAsync_RangeWithUnmetCondition_IgnoresTheCondition()
    {
        (TransferResult result, MemoryStream output, RecordingTransferEvents events) = await DownloadAsync(
            UnmetCondition,
            FileDate,
            range: ByteRange.Bounded(0, 0), rangeText: "0-0");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(result.TimeConditionUnmet);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("h"), output.ToArray());
        CollectionAssert.AreEqual(new[] { "<= h", ShuttingDown }, events.Transcript);
    }

    // curl -sv -C 1 -z "Jan 1 2020" ...: stdout "i\n", no time condition line.
    [TestMethod]
    public async Task ExecuteAsync_ResumeWithUnmetCondition_IgnoresTheCondition()
    {
        (TransferResult result, MemoryStream output, RecordingTransferEvents events) = await DownloadAsync(
            UnmetCondition,
            FileDate,
            resumeFrom: 1);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(result.TimeConditionUnmet);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("i\n"), output.ToArray());
        CollectionAssert.AreEqual(new[] { "<= i\n", ShuttingDown }, events.Transcript);
    }

    // -C 0 sets no range in curl, so the condition still applies.
    [TestMethod]
    public async Task ExecuteAsync_ResumeFromZeroWithUnmetCondition_ReportsNotNewEnough()
    {
        (TransferResult result, MemoryStream output, RecordingTransferEvents events) = await DownloadAsync(
            UnmetCondition,
            FileDate,
            resumeFrom: 0);

        Assert.IsTrue(result.TimeConditionUnmet);
        Assert.IsEmpty(output.ToArray());
        CollectionAssert.AreEqual(new[] { "* The requested document is not new enough", ShuttingDown }, events.Transcript);
    }

    // -r 5-2 names no range but still sets curl's state.range, so the condition is skipped
    // and the exit 33 after the open follows.
    [TestMethod]
    public async Task ExecuteAsync_RangeTextNamingNoRangeWithUnmetCondition_FailsWithExit33()
    {
        (TransferResult result, MemoryStream output, RecordingTransferEvents events) = await DownloadAsync(
            UnmetCondition,
            FileDate,
            rangeText: "5-2");

        Assert.AreEqual(CurlExitCode.RangeError, result.ExitCode);
        Assert.IsEmpty(output.ToArray());
        CollectionAssert.AreEqual(new[] { ShuttingDown }, events.Transcript);
    }

    [TestMethod]
    [DataRow(TimeConditionKind.IfModifiedSince, 2000)]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, 2020)]
    public async Task ExecuteAsync_MetCondition_ReportsNoConditionLine(TimeConditionKind kind, int year)
    {
        var condition = new TimeCondition(new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero), kind);

        (TransferResult result, MemoryStream output, RecordingTransferEvents events) = await DownloadAsync(condition, FileDate);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Content, output.ToArray());
        CollectionAssert.AreEqual(new[] { "<= hi\n", ShuttingDown }, events.Transcript);
    }

    // Curl_meets_timecondition reads a time of 0 on either side as unknown, and met.
    [TestMethod]
    public async Task ExecuteAsync_FileAtTheUnixEpoch_ReportsNoConditionLine()
    {
        (TransferResult result, MemoryStream output, RecordingTransferEvents events) = await DownloadAsync(
            UnmetCondition,
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Content, output.ToArray());
        CollectionAssert.AreEqual(new[] { "<= hi\n", ShuttingDown }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConditionAtTheUnixEpoch_ReportsNoConditionLine()
    {
        var condition = new TimeCondition(DateTimeOffset.UnixEpoch, TimeConditionKind.IfUnmodifiedSince);

        (TransferResult result, MemoryStream output, RecordingTransferEvents events) = await DownloadAsync(condition, FileDate);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Content, output.ToArray());
        CollectionAssert.AreEqual(new[] { "<= hi\n", ShuttingDown }, events.Transcript);
    }

    private static TimeCondition UnmetCondition =>
        new(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfModifiedSince);

    private static async Task<(TransferResult Result, MemoryStream Output, RecordingTransferEvents Events)> DownloadAsync(
        TimeCondition condition,
        DateTimeOffset fileDate,
        ByteRange? range = null,
        string? rangeText = null,
        long? resumeFrom = null)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content, fileDate);
        var output = new MemoryStream();
        var events = new RecordingTransferEvents();
        var context = new TransferContext { Url = FileUrl, Output = output, Events = events, TimeCondition = condition, Range = range, RangeText = rangeText, ResumeFrom = resumeFrom };

        TransferResult result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        return (result, output, events);
    }
}
