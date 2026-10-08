using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;
using Curl.Testing;

namespace Curl.Protocol.File;

/// <summary>
/// Pins the events a <c>file://</c> transfer reports for <c>-v</c> and <c>--trace</c>,
/// against curl 8.21.0 as measured in BL-936: the body as received data, each chunk as
/// read; a failure's message as an information line; and, once the transfer got past its
/// open, <c>shutting down connection #N</c> or, after exit 23 or 26,
/// <c>closing connection #N</c>. Nothing else is reported: no headers, no sent data.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerTransferEventTests
{
    private const string ShuttingDown = "* shutting down connection #0";

    private static CurlUrl FileUrl => CurlUrl.Parse("file:///dir/a.txt");

    private static CurlUrl MissingUrl => CurlUrl.Parse("file:///dir/nope.txt");

    private static string OsPath => "/dir/a.txt".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] Content => Encoding.ASCII.GetBytes("hello\n");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // curl --trace-ascii - file:///<dir>/a.txt:
    //   <= Recv data, 6 bytes (0x6)
    //   0000: hello.
    //   * shutting down connection #0
    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsTheBodyThenShuttingDown()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("file /dir/a.txt", "hello\\n");

        var result = await DownloadAsync(new TransferContext { Url = FileUrl, Output = new MemoryStream(), Events = events });

        string[] expected = ["<= hello\n", ShuttingDown];
        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("transcript", string.Join(" | ", expected), string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "<= hello\n", ShuttingDown }, events.Transcript);
    }

    // curl --trace-ascii tr.txt -o out file:///<dir>/big.txt on a 1000000-byte file traces
    // nine "<= Recv data, 102399 bytes (0x18fff)" and one "<= Recv data, 78409 bytes"
    // (BL-936); the output still takes writes of at most 16384 bytes (BL-976).
    [TestMethod]
    public async Task ExecuteAsync_DownloadPastTwoReads_ReportsReceivedDataInReadSizedChunksAndWritesInSixteenKilobytes()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Encoding.ASCII.GetBytes(new string('a', 250000)));
        var events = new RecordingTransferEvents();
        var output = new ChunkRecordingStream();
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("file /dir/a.txt length", 250000);

        var result = await new FileProtocolHandler(fileSystem)
            .ExecuteAsync(new TransferContext { Url = FileUrl, Output = output, Events = events });

        int[] receivedLengths = events.Transcript.Where(line => line.StartsWith("<= ", StringComparison.Ordinal)).Select(line => line.Length - 3).ToArray();
        int[] oneRead = [16384, 16384, 16384, 16384, 16384, 16384, 4095];
        int[] expectedWrites = oneRead.Concat(oneRead).Concat([16384, 16384, 12434]).ToArray();
        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("received chunk lengths", string.Join(",", receivedLengths));
        Diagnostics.Act("write lengths", string.Join(",", output.WriteLengths));
        Diagnostics.Assert("ExitCode", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("received chunk lengths", "102399,102399,45202", string.Join(",", receivedLengths));
        Diagnostics.Assert("write lengths", string.Join(",", expectedWrites), string.Join(",", output.WriteLengths));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { 102399, 102399, 45202 },
            events.Transcript.Where(line => line.StartsWith("<= ", StringComparison.Ordinal)).Select(line => line.Length - 3).ToArray());
        CollectionAssert.AreEqual(
            oneRead.Concat(oneRead).Concat([16384, 16384, 12434]).ToArray(),
            output.WriteLengths.ToArray());
    }

    // curl -v writes the meter's line end before "* shutting down connection #0", after a
    // failure's own line: done falls between the two.
    [TestMethod]
    public async Task ExecuteAsync_TransferEnds_ReportsDoneBetweenTheFailureAndTheConnectionLine()
    {
        var events = new RecordingTransferEvents();
        var progress = new RecordingTransferProgress { Transcript = events.Transcript };
        var context = new TransferContext { Url = FileUrl, Output = new MemoryStream(), MaxFileSize = 3, Events = events, Progress = progress };
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("MaxFileSize", 3);

        TransferResult result = await DownloadAsync(context);

        string[] expected = ["<= hello\n", "* Exceeded the maximum allowed file size (3) with 3 bytes", "ReportTransferDone", ShuttingDown];
        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("transcript", string.Join(" | ", expected), string.Join(" | ", events.Transcript));
        CollectionAssert.AreEqual(
            new[] { "<= hello\n", "* Exceeded the maximum allowed file size (3) with 3 bytes", "ReportTransferDone", ShuttingDown },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadEnds_ReportsDoneBeforeTheConnectionLine()
    {
        var events = new RecordingTransferEvents();
        var progress = new RecordingTransferProgress { Transcript = events.Transcript };
        var context = new TransferContext { Url = FileUrl, Output = new MemoryStream(), Upload = new MemoryStream(Content), Events = events, Progress = progress };
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Bytes("upload", Content);

        TransferResult result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        string[] expected = ["ReportTransferDone", ShuttingDown];
        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("transcript", string.Join(" | ", expected), string.Join(" | ", events.Transcript));
        CollectionAssert.AreEqual(new[] { "ReportTransferDone", ShuttingDown }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_SourceWillNotOpen_NeverReportsDone()
    {
        var events = new RecordingTransferEvents();
        var progress = new RecordingTransferProgress { Transcript = events.Transcript };
        Diagnostics.Arrange("url", MissingUrl);
        Diagnostics.Arrange("file system", "empty");

        TransferResult result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(
            new TransferContext { Url = MissingUrl, Output = new MemoryStream(), Events = events, Progress = progress });

        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("transcript contains ReportTransferDone", false, events.Transcript.Contains("ReportTransferDone"));
        CollectionAssert.DoesNotContain(events.Transcript, "ReportTransferDone");
    }

    // curl --trace-ascii - -T a.txt file:///<dir>/b.txt writes no "=> Send data".
    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsOnlyShuttingDown()
    {
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = FileUrl,
            Output = new MemoryStream(),
            Upload = new MemoryStream(Content),
            Events = events,
        };
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Bytes("upload", Content);

        var result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("transcript", ShuttingDown, string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { ShuttingDown }, events.Transcript);
    }

    // curl --trace-ascii - -I file:///<dir>/a.txt writes the pseudo-headers to stdout and
    // traces none of them: no "<= Recv header".
    [TestMethod]
    public async Task ExecuteAsync_Head_ReportsOnlyShuttingDown()
    {
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = FileUrl,
            Output = new MemoryStream(),
            HeaderOutput = new MemoryStream(),
            NoBody = true,
            Events = events,
        };
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("NoBody", true);

        var result = await DownloadAsync(context);

        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("transcript", ShuttingDown, string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { ShuttingDown }, events.Transcript);
    }

    // curl -v file:///<dir>/nope.txt: "* Could not open file ..." and no connection line.
    [TestMethod]
    public async Task ExecuteAsync_MissingFile_ReportsOnlyTheFailure()
    {
        var events = new RecordingTransferEvents();
        var handler = new FileProtocolHandler(new FakeFileSystem());
        Diagnostics.Arrange("url", MissingUrl);
        Diagnostics.Arrange("file system", "empty");

        var result = await handler.ExecuteAsync(new TransferContext { Url = MissingUrl, Output = new MemoryStream(), Events = events });

        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("ErrorMessage", result.ErrorMessage);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Diagnostics.Assert("transcript", "* Could not open file /dir/nope.txt", string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "* Could not open file /dir/nope.txt" }, events.Transcript);
    }

    // curl -v a.txt a.txt nope.txt a.txt: #0, #1, the failure with no number, then #2.
    [TestMethod]
    public async Task ExecuteAsync_SeveralTransfers_NumberOnlyThoseThatOpened()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var handler = new FileProtocolHandler(fileSystem);
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("urls", "a.txt, a.txt, nope.txt, a.txt");
        Diagnostics.Arrange("file /dir/a.txt", "hello\\n");

        foreach (CurlUrl url in new[] { FileUrl, FileUrl, MissingUrl, FileUrl })
        {
            await handler.ExecuteAsync(new TransferContext { Url = url, Output = new MemoryStream(), Events = events });
        }

        string[] expected = ["shutting down connection #0", "shutting down connection #1", "Could not open file /dir/nope.txt", "shutting down connection #2"];
        Diagnostics.Act("info", string.Join(" | ", events.Info));
        Diagnostics.Assert("info", string.Join(" | ", expected), string.Join(" | ", events.Info));
        CollectionAssert.AreEqual(
            new[]
            {
                "shutting down connection #0",
                "shutting down connection #1",
                "Could not open file /dir/nope.txt",
                "shutting down connection #2",
            },
            events.Info);
    }

    // curl -v http://127.0.0.1:<port>/ file:///C:/Windows/win.ini: the HTTP connection is #0,
    // so the file transfer shuts down #1 (measured, BL-977).
    [TestMethod]
    public async Task ExecuteAsync_DownloadAfterANetworkedConnection_ShutsDownTheSharedNextNumber()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var connectionNumbers = new ConnectionNumberSequence();
        connectionNumbers.NumberNextConnection();
        var handler = new FileProtocolHandler(fileSystem, connectionNumbers);
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("connections already numbered", 1);

        await handler.ExecuteAsync(new TransferContext { Url = FileUrl, Output = new MemoryStream(), Events = events });

        long nextNumber = connectionNumbers.NumberNextConnection();
        Diagnostics.Act("info", string.Join(" | ", events.Info));
        Diagnostics.Act("next connection number", nextNumber);
        Diagnostics.Assert("info", "shutting down connection #1", string.Join(" | ", events.Info));
        Diagnostics.Assert("next connection number", 2L, nextNumber);
        CollectionAssert.AreEqual(new[] { "shutting down connection #1" }, events.Info);
        Assert.AreEqual(2L, nextNumber);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadAfterANetworkedConnection_ShutsDownTheSharedNextNumber()
    {
        var connectionNumbers = new ConnectionNumberSequence();
        connectionNumbers.NumberNextConnection();
        var handler = new FileProtocolHandler(new FakeFileSystem(), connectionNumbers);
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("connections already numbered", 1);

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = FileUrl, Output = new MemoryStream(), Upload = new MemoryStream(Content), Events = events });

        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("info", string.Join(" | ", events.Info));
        Diagnostics.Assert("info", "shutting down connection #1", string.Join(" | ", events.Info));
        CollectionAssert.AreEqual(new[] { "shutting down connection #1" }, events.Info);
    }

    // curl --trace-ascii - --max-filesize 3: all six bytes traced, three written.
    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeCutsTheBody_ReportsTheWholeChunkReadThenTheFailure()
    {
        var events = new RecordingTransferEvents();
        var output = new MemoryStream();
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("MaxFileSize", 3);

        var result = await DownloadAsync(new TransferContext { Url = FileUrl, Output = output, MaxFileSize = 3, Events = events });

        string[] expected = ["<= hello\n", "* Exceeded the maximum allowed file size (3) with 3 bytes", ShuttingDown];
        string written = Encoding.ASCII.GetString(output.ToArray());
        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("ErrorMessage", result.ErrorMessage);
        Diagnostics.Bytes("output", output.ToArray());
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Diagnostics.Diff("written", "hel", written);
        Diagnostics.Assert("transcript", string.Join(" | ", expected), string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("hel", Encoding.ASCII.GetString(output.ToArray()));
        CollectionAssert.AreEqual(
            new[] { "<= hello\n", "* Exceeded the maximum allowed file size (3) with 3 bytes", ShuttingDown },
            events.Transcript);
    }

    // curl --trace-ascii - -o <unwritable>: the chunk is traced, then the write fails.
    [TestMethod]
    public async Task ExecuteAsync_OutputRefusesTheBody_ReportsTheChunkThenClosing()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("output", "stream failing on write 1");

        var result = await DownloadAsync(new TransferContext { Url = FileUrl, Output = FaultingStream.FailingOnWrite(1), Events = events });

        string[] expected = ["<= hello\n", "* " + result.ErrorMessage, "* closing connection #0"];
        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("ErrorMessage", result.ErrorMessage);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.WriteError, result.ExitCode);
        Diagnostics.Assert("transcript", string.Join(" | ", expected), string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "<= hello\n", "* " + result.ErrorMessage, "* closing connection #0" },
            events.Transcript);
    }

    // curl --trace-ascii - -C 100: "* failed to resume file:// transfer", shutting down.
    [TestMethod]
    public async Task ExecuteAsync_ResumePastTheEnd_ReportsTheFailureThenShuttingDown()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("ResumeFrom", 100);

        var result = await DownloadAsync(new TransferContext { Url = FileUrl, Output = new MemoryStream(), ResumeFrom = 100, Events = events });

        string[] expected = ["* failed to resume file:// transfer", ShuttingDown];
        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("ErrorMessage", result.ErrorMessage);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.BadDownloadResume, result.ExitCode);
        Diagnostics.Assert("transcript", string.Join(" | ", expected), string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "* failed to resume file:// transfer", ShuttingDown },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyFile_ReportsNoDataOnlyShuttingDown()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, []);
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("file /dir/a.txt length", 0);

        TransferResult result = await new FileProtocolHandler(fileSystem).ExecuteAsync(new TransferContext { Url = FileUrl, Output = new MemoryStream(), Events = events });

        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("transcript", ShuttingDown, string.Join(" | ", events.Transcript));
        CollectionAssert.AreEqual(new[] { ShuttingDown }, events.Transcript);
    }

    // curl -v -T a.txt file:///<missing dir>/b.txt: the failure, then "closing".
    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationWillNotOpen_ReportsTheFailureThenClosing()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForWrite(OsPath, FileAccessStatus.NotFound);
        var events = new RecordingTransferEvents();
        var context = new TransferContext { Url = FileUrl, Output = new MemoryStream(), Upload = new MemoryStream(Content), Events = events };
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("open for write of /dir/a.txt fails with", FileAccessStatus.NotFound);

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        string[] expected = [$"* cannot open {OsPath} for writing", "* closing connection #0"];
        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript).Replace(Path.DirectorySeparatorChar, '/'));
        Diagnostics.Assert("ExitCode", CurlExitCode.WriteError, result.ExitCode);
        Diagnostics.Assert("transcript", string.Join(" | ", expected).Replace(Path.DirectorySeparatorChar, '/'), string.Join(" | ", events.Transcript).Replace(Path.DirectorySeparatorChar, '/'));
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"* cannot open {OsPath} for writing", "* closing connection #0" },
            events.Transcript);
    }

    // curl -T <source that reads short>: "* client read function EOF fail, ...", closing.
    [TestMethod]
    public async Task ExecuteAsync_UploadSourceReadFails_ReportsTheFailureThenClosing()
    {
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = FileUrl,
            Output = new MemoryStream(),
            Upload = FaultingStream.FailingOnRead(Content, 1),
            Events = events,
        };
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Bytes("upload source", Content);

        var result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        string[] expected = ["* client read function EOF fail, only 0/6 of needed bytes read", "* closing connection #0"];
        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("ErrorMessage", result.ErrorMessage);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.ReadError, result.ExitCode);
        Diagnostics.Assert("transcript", string.Join(" | ", expected), string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "* client read function EOF fail, only 0/6 of needed bytes read", "* closing connection #0" },
            events.Transcript);
    }

    // curl --trace-ascii - -T src file:///<locked dest>: exit 55 prints no "* " line of its
    // own, only "* shutting down connection #0".
    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationWriteFails_ReportsOnlyShuttingDown()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.WriteInto(OsPath, FaultingStream.FailingOnWrite(1));
        var events = new RecordingTransferEvents();
        var context = new TransferContext { Url = FileUrl, Output = new MemoryStream(), Upload = new MemoryStream(Content), Events = events };
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("destination /dir/a.txt", "stream failing on write 1");

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.SendError, result.ExitCode);
        Diagnostics.Assert("transcript", ShuttingDown, string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        CollectionAssert.AreEqual(new[] { ShuttingDown }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegativeResume_ReportsOnlyTheFailure()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("ResumeFrom", -1);

        var result = await DownloadAsync(new TransferContext { Url = FileUrl, Output = new MemoryStream(), ResumeFrom = -1, Events = events });

        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.BadDownloadResume, result.ExitCode);
        Diagnostics.Assert("transcript", "* failed to resume file:// transfer", string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "* failed to resume file:// transfer" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlOfAnotherScheme_ReportsOnlyTheFailure()
    {
        var events = new RecordingTransferEvents();
        var handler = new FileProtocolHandler(new FakeFileSystem());
        CurlUrl url = CurlUrl.Parse("http://example.com/a.txt");
        Diagnostics.Arrange("url", url);

        var result = await handler.ExecuteAsync(new TransferContext { Url = url, Output = new MemoryStream(), Events = events });

        Diagnostics.Act("ExitCode", result.ExitCode);
        Diagnostics.Act("ErrorMessage", result.ErrorMessage);
        Diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        Diagnostics.Assert("ExitCode", CurlExitCode.UrlMalformat, result.ExitCode);
        Diagnostics.Assert("transcript", "* " + result.ErrorMessage, string.Join(" | ", events.Transcript));
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "* " + result.ErrorMessage }, events.Transcript);
    }

    private static async Task<TransferResult> DownloadAsync(TransferContext context)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);

        return await new FileProtocolHandler(fileSystem).ExecuteAsync(context);
    }
}
