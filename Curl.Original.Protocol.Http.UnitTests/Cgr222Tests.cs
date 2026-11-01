using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-222 (BL-3751): <c>-v -I</c> against a server that wrongly
/// sends a body after its HEAD response; curl 8.22.0 reports the bytes read along with the head
/// as received data (<c>{ [5 bytes data]</c>) but leaves the body unread, so no excess line.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_HeadResponseWithBodyBytesInTheHeadRead_ReportsThemAsReceivedData()
    {
        const string Response = "HTTP/1.1 302 Found\r\nContent-Length: 5\r\n\r\nhello";
        RecordingTransferEvents events = new();
        TransferContext context = ReuseContext(events);
        context = new TransferContext { Url = context.Url, Output = context.Output, Events = events, NoBody = true };
        Diagnostics.Arrange("response, chunk size", $"{OneLine(Response)}, 65536");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(Response, 65536), null, connectionNumber: 0)))
            .ExecuteAsync(context);

        WriteResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string[] expected = ["{ hello"];
        WriteExpectedLines("events with data", expected, events.Events.Where(line => line.StartsWith("{", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(expected, events.Events.Where(line => line.StartsWith("{", StringComparison.Ordinal)).ToArray());
        Assert.IsFalse(events.Events.Any(line => line.StartsWith("* Excess found", StringComparison.Ordinal)));
    }
}
