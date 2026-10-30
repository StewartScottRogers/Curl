using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-212 (BL-3741): curl 8.22.0's <c>-v</c> ends
/// <c>Connection #0 to host ex.test:80 left intact</c> naming the URL's host and port, not the
/// <c>--connect-to</c> target.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_ConnectToMappedHost_ReportsTheUrlHostAndPortLeftIntact()
    {
        ScriptedConnection connection = Connection("HTTP/1.0 200 OK\r\nConnection: keep-alive\r\n\r\nhi", 65536);
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, mapped host", $"{ReuseUrl}, 10.9.8.7:20600");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, mappedHost: "10.9.8.7", mappedPort: 20600)))
            .ExecuteAsync(ReuseContext(events));

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Assert.AreEqual("Connection #0 to host 127.0.0.1:18977 left intact", events.Info[^1]);
    }
}
