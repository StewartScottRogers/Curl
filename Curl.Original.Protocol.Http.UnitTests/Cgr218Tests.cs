using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// CGR-218: the progress meter counts the body of a redirect response <c>-L</c> follows, so
/// the hop's row reads its Content-Length and bytes, as curl 8.22.0 does.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_RedirectWhileFollowing_ReportsTheDiscardedBodyCounts()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferProgress progress = new();
            ScriptedConnection connection = Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 5\r\n\r\nmoved", chunkSize);
            TransferContext context = ProgressContext("http://example.com/", progress, new HttpRequestOptions { FollowRedirects = true });
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", "302 Found, Location: /b, body moved, following");

            await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

            WriteReports(progress);
            AssertRunningTotals(progress.Downloads, 5, "5", chunkSize);
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 5\r\n\r\nmoved", false, "down 0/5, down 5/5", DisplayName = "302 not followed is not discarded")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", true, "down 0/5, down 5/5", DisplayName = "200 followed is the final body")]
    [DataRow("HTTP/1.1 404 Not Found\r\nContent-Length: 5\r\n\r\nnope!", true, "down 0/5, down 5/5", DisplayName = "404 followed is the final body")]
    public async Task ExecuteAsync_NonRedirectOrNotFollowing_ReportsTheBodyAsTheFinalOne(string response, bool follow, string expected)
    {
        RecordingTransferProgress progress = new();
        ScriptedConnection connection = Connection(response, 65536);
        TransferContext context = ProgressContext("http://example.com/", progress, new HttpRequestOptions { FollowRedirects = follow });

        await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        CollectionAssert.AreEqual(expected.Split(", "), progress.Downloads.ToArray());
    }}
