using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-001 (BL-3530): bytes in the read that ends a response past
/// its Content-Length, or after the head of a 204, are not kept for the next request; curl
/// 8.22.0 (mingw, Schannel) prints <c>Excess found writing body</c> and shuts the connection
/// down, measured on 2026-10-26 with <c>Record-CurlExchange.ps1</c> (BL-3530 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ContentLengthWithExcess = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nheHTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nEVIL";

    private const string NoContentWithExcess = "HTTP/1.1 204 No\r\nContent-Length: 5\r\n\r\nhelloHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthBodyWithBytesAfterItInTheSameRead_ReportsExcessAndShutsTheConnectionDown()
    {
        // curl -v against a Content-Length 2 response followed by a whole second response, in one write.
        RecordingTransferEvents events = new();
        MemoryStream output = new();
        Diagnostics.Arrange("response, chunk size", $"{OneLine(ContentLengthWithExcess)}, 65536");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(ContentLengthWithExcess, 65536), null, connectionNumber: 0)))
            .ExecuteAsync(ReuseContext(events, output));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("he", Encoding.ASCII.GetString(output.ToArray()));
        string[] expected = ["{ heHTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nEVIL", "* Excess found writing body: excess = 42, size = 2, maxdownload = 2, bytecount = 2", "* shutting down connection #0"];
        WriteExpectedLines("last 3 events", expected, events.Events.TakeLast(3));
        CollectionAssert.AreEqual(expected, events.Events.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoContentResponseWithBytesAfterItsHead_ReportsExcessAndShutsTheConnectionDown()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("response, chunk size", $"{OneLine(NoContentWithExcess)}, 65536");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(NoContentWithExcess, 65536), null, connectionNumber: 0)))
            .ExecuteAsync(ReuseContext(events));

        WriteResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string[] expected = ["{ helloHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", "* Excess found writing body: excess = 45, size = 0, maxdownload = 0, bytecount = 0", "* shutting down connection #0"];
        WriteExpectedLines("last 3 events", expected, events.Events.TakeLast(3));
        CollectionAssert.AreEqual(expected, events.Events.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthBodyEndingItsRead_LeavesTheConnectionIntact()
    {
        // Bytes after the body that arrive only in later reads are not seen, as curl does not see them.
        RecordingTransferEvents events = new();
        const string Exact = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhe";
        Diagnostics.Arrange("response, chunk size", $"{OneLine(Exact)}, 65536");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(Exact, 65536), null, connectionNumber: 0)))
            .ExecuteAsync(ReuseContext(events));

        WriteResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(events.Events.Any(line => line.StartsWith("* Excess found", StringComparison.Ordinal)));
        StringAssert.EndsWith(events.Events[^1], "left intact");
    }
}
