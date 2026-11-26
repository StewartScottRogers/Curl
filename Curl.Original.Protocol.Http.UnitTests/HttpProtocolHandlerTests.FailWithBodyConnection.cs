using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Fitness finding STD-210 (BL-5891): an error status <c>--fail-with-body</c> fails once its body is
/// read ends the connection as a success does - curl 8.22.0 leaves a persisting one intact, for
/// <c>--retry</c> to reuse, and writes no <c>-v</c> line for the error.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 500 Oops\r\nContent-Length: 4\r\n\r\noops", true, "Connection #4 to host 127.0.0.1:18977 left intact", DisplayName = "Persisting response")]
    [DataRow("HTTP/1.1 500 Oops\r\nConnection: close\r\nContent-Length: 4\r\n\r\noops", false, "shutting down connection #4", DisplayName = "Connection: close")]
    public async Task ExecuteAsync_FailWithBodyOnErrorStatus_EndsTheConnectionAsASuccessDoes(string response, bool reusable, string connectionEnd)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(response, chunkSize);
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("url, chunk size", $"{ReuseUrl}, {chunkSize}");
            Diagnostics.Arrange("response", OneLine(response));

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 4)))
                .ExecuteAsync(ReuseContext(events, http: new HttpRequestOptions { Fail = HttpFailMode.FailWithBody }));

            WriteResult(result);
            WriteEvents("info lines", events.Info);
            Diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
            Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("connection marked reusable", reusable, connection.IsMarkedReusable);
            Assert.AreEqual(reusable, connection.IsMarkedReusable, $"Chunk size {chunkSize}");
            string[] expectedInfo = InfoLines(string.Empty, connectionEnd);
            WriteExpectedLines("info lines", expectedInfo, events.Info);
            CollectionAssert.AreEqual(expectedInfo, events.Info, $"Chunk size {chunkSize}");
        }
    }
}
