using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// <c>-v -I</c> against a server that sends body bytes with its head: curl 8.22.0 shuts the
/// connection down rather than keep it for the next request, measured on 2026-11-25 with
/// <c>Record-CurlExchange.ps1</c> (BL-5948 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nno\n", DisplayName = "Content-Length")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nno\n", DisplayName = "no Content-Length")]
    [DataRow("HTTP/1.1 204 No Content\r\n\r\nno\n", DisplayName = "204")]
    [DataRow("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nno\n", DisplayName = "HTTP/1.0 keep-alive")]
    public async Task ExecuteAsync_NoBodyResponseWithBodyBytes_ShutsTheConnectionDownAndDoesNotReuseIt(string response)
    {
        RecordingTransferEvents events = new();
        ScriptedConnection connection = Connection(response, 65536);
        Diagnostics.Arrange("response, chunk size", $"{OneLine(response)}, 65536");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
            .ExecuteAsync(NoBodyContext(events));

        WriteResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string[] expected = ["{ no\n", "* shutting down connection #0"];
        WriteExpectedLines("last 2 events", expected, events.Events.TakeLast(2));
        CollectionAssert.AreEqual(expected, events.Events.TakeLast(2).ToArray());
        Assert.IsFalse(connection.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBodyResponseWithoutBodyBytes_LeavesTheConnectionIntactForReuse()
    {
        const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\n";
        RecordingTransferEvents events = new();
        ScriptedConnection connection = Connection(Response, 65536);
        Diagnostics.Arrange("response, chunk size", $"{OneLine(Response)}, 65536");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
            .ExecuteAsync(NoBodyContext(events));

        WriteResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.EndsWith(events.Events[^1], "left intact");
        Assert.IsTrue(connection.IsMarkedReusable);
    }

    private static TransferContext NoBodyContext(ITransferEvents events) =>
        new() { Url = CurlUrl.Parse(ReuseUrl), Output = new MemoryStream(), Events = events, NoBody = true };
}
