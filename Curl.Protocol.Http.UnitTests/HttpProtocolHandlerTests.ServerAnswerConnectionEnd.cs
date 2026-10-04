using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// The connection curl 8.21.0 leaves intact after the server's own answer to <c>-C</c> or
/// <c>-z</c> - a 416, whose body it reads and ignores, and a real 304 (BL-1412), measured on
/// 2026-10-03 with <c>Record-CurlExchange.ps1</c> (mingw, Schannel).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string RangeNotSatisfiableWithBody = "HTTP/1.1 416 Range Not Satisfiable\r\nContent-Length: 4\r\n\r\nabcd";

    [TestMethod]
    [DataRow("HTTP/1.1 416 Range Not Satisfiable\r\nContent-Length: 0\r\n\r\n", "< Content-Length: 0\r\n", DisplayName = "Content-Length: 0")]
    [DataRow(RangeNotSatisfiableWithBody, "< Content-Length: 4\r\n", DisplayName = "Content-Length: 4 and a body")]
    public async Task ExecuteAsync_ResumeAnswered416_IgnoresTheBodyAndLeavesTheConnectionIntact(string response, string contentLengthLine)
    {
        // curl -sv -C 5 against the 416 writes "setting size while ignoring" before the empty line.
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            ScriptedConnection connection = Connection(response, chunkSize, RangeRequest(18995, "5-"));
            TransferContext context = new() { Url = ConditionUrl(18995), Output = output, Events = events, ResumeFrom = 5 };

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
            Assert.IsTrue(connection.IsMarkedReusable, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(
                new[]
                {
                    "< HTTP/1.1 416 Range Not Satisfiable\r\n",
                    contentLengthLine,
                    "* setting size while ignoring",
                    "< \r\n",
                    "* Connection #0 to host 127.0.0.1:18995 left intact",
                },
                EventsFromStatusLine(events),
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeAnswered416WithABody_ReadsTheBodySoTheNextRequestReusesTheConnection()
    {
        // One byte per read, so no reader holds bytes past the 416's body: the second response
        // is read whole only when the first transfer consumed "abcd".
        SessionHoldingConnection connection = new(Connection(RangeNotSatisfiableWithBody + "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 1));
        HttpProtocolHandler handler = Handler(new QueueConnector(
            ConnectResult.Connected(connection, null, connectionNumber: 0),
            ConnectResult.Connected(connection, null, isReused: true, connectionNumber: 0)));
        MemoryStream secondOutput = new();

        TransferResult first = await handler.ExecuteAsync(new TransferContext { Url = ConditionUrl(18996), Output = new MemoryStream(), ResumeFrom = 5 });
        TransferResult second = await handler.ExecuteAsync(new TransferContext { Url = ConditionUrl(18996), Output = secondOutput });

        Assert.AreEqual(CurlExitCode.Ok, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual("ok", Latin1(secondOutput.ToArray()));
        Assert.AreEqual(2, connection.ReturnedReusableCount);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 304 Not Modified\r\n\r\n", DisplayName = "No Content-Length")]
    [DataRow("HTTP/1.1 304 Not Modified\r\nContent-Length: 4\r\n\r\n", DisplayName = "Content-Length: 4")]
    public async Task ExecuteAsync_TimeConditionAnswered304_LeavesTheConnectionIntact(string response)
    {
        // curl -sv -z "1 Jan 2020" against a real 304 writes no size line and ends "left intact".
        RecordingTransferEvents events = new();
        ScriptedConnection connection = Connection(response, 65536);
        TransferContext context = new()
        {
            Url = ConditionUrl(18997),
            Output = new MemoryStream(),
            Events = events,
            TimeCondition = new TimeCondition(ConditionTime, TimeConditionKind.IfModifiedSince),
        };

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(connection.IsMarkedReusable);
        CollectionAssert.DoesNotContain(events.Info.ToList(), "setting size while ignoring");
        Assert.AreEqual("< \r\n", EventsFromStatusLine(events)[^2]);
        Assert.AreEqual("* Connection #0 to host 127.0.0.1:18997 left intact", events.Events[^1]);
    }
}
