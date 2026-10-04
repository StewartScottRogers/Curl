using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>-v</c> lines curl 8.21.0 writes when <c>-C</c> finds nothing left or <c>-z</c> is unmet
/// by a response's <c>Last-Modified</c>, and the connection it then shuts down (BL-1398), measured
/// on 2026-10-03 with <c>Record-CurlExchange.ps1</c> (mingw, Schannel).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string LastModified2001Head = "HTTP/1.1 200 OK\r\nLast-Modified: Mon, 01 Jan 2001 00:00:00 GMT\r\nContent-Length: 5\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_ResumeAtTheContentLength_ReportsTheDocumentAlreadyDownloadedAndShutsTheConnectionDown()
    {
        // curl -sv -C 5 against "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello".
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            TransferContext context = new() { Url = ConditionUrl(18990), Output = output, Events = events, ResumeFrom = 5 };

            TransferResult result = await Handler(QueueConnector.For(Connection(WholeHead + "hello", chunkSize, RangeRequest(18990, "5-"))))
                .ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(
                new[]
                {
                    "< HTTP/1.1 200 OK\r\n",
                    "< Content-Length: 5\r\n",
                    "* The entire document is already downloaded",
                    "< \r\n",
                    "* shutting down connection #0",
                },
                EventsFromStatusLine(events),
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow(TimeConditionKind.IfModifiedSince, 2020, "The requested document is not new enough", DisplayName = "-z \"Jan 1 2020\"")]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, 2000, "The requested document is not old enough", DisplayName = "-z \"-Jan 1 2000\"")]
    public async Task ExecuteAsync_LastModifiedFailsTheCondition_ReportsTheSimulated304AndShutsTheConnectionDown(TimeConditionKind kind, int year, string conditionLine)
    {
        // curl -sv -z "Jan 1 2020" against a 200 last modified in 2001; -z -date pins the other text.
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            TransferContext context = new()
            {
                Url = ConditionUrl(18991),
                Output = output,
                Events = events,
                TimeCondition = new TimeCondition(new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero), kind),
            };

            TransferResult result = await Handler(QueueConnector.For(Connection(LastModified2001Head + "hello", chunkSize))).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(
                new[]
                {
                    "< HTTP/1.1 200 OK\r\n",
                    "< Last-Modified: Mon, 01 Jan 2001 00:00:00 GMT\r\n",
                    "< Content-Length: 5\r\n",
                    "* " + conditionLine,
                    "* Simulate an HTTP 304 response",
                    "< \r\n",
                    "* shutting down connection #0",
                },
                EventsFromStatusLine(events),
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_TimeConditionAnswered304_ReportsNoSimulatedLine()
    {
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = ConditionUrl(18992),
            Output = new MemoryStream(),
            Events = events,
            TimeCondition = new TimeCondition(ConditionTime, TimeConditionKind.IfModifiedSince),
        };

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 304 Not Modified\r\n\r\n", 65536))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "< HTTP/1.1 304 Not Modified\r\n", "< \r\n" }, EventsFromStatusLine(events).Take(2).ToArray());
        AssertNoUndeliveredBodyLine(events);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeHonouredBy206_ReportsNoNewLineAndLeavesTheConnectionIntact()
    {
        RecordingTransferEvents events = new();
        MemoryStream output = new();
        TransferContext context = new() { Url = ConditionUrl(18993), Output = output, Events = events, ResumeFrom = 100 };

        TransferResult result = await Handler(QueueConnector.For(Connection(Partial + "hello", 65536, RangeRequest(18993, "100-"))))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
        AssertNoUndeliveredBodyLine(events);
        Assert.AreEqual("* Connection #0 to host 127.0.0.1:18993 left intact", events.Events[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeAnswered416_ReportsNoUndeliveredBodyLine()
    {
        // curl -sv -C 5 against a 416 with Content-Length: 0 writes none of BL-1398's lines.
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = ConditionUrl(18994), Output = new MemoryStream(), Events = events, ResumeFrom = 5 };

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 416 Range Not Satisfiable\r\nContent-Length: 0\r\n\r\n", 65536, RangeRequest(18994, "5-"))))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        AssertNoUndeliveredBodyLine(events);
    }

    private static string[] EventsFromStatusLine(RecordingTransferEvents events) =>
        events.Events.SkipWhile(line => !line.StartsWith("< HTTP/", StringComparison.Ordinal)).ToArray();

    private static void AssertNoUndeliveredBodyLine(RecordingTransferEvents events)
    {
        string[] lines =
        [
            "The entire document is already downloaded",
            "The requested document is not new enough",
            "The requested document is not old enough",
            "Simulate an HTTP 304 response",
        ];
        foreach (string line in lines)
        {
            CollectionAssert.DoesNotContain(events.Info.ToList(), line);
        }
    }
}
