using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <c>Need to rewind upload for next request</c>: curl 8.21.0 writes it under plain <c>-v</c> right
/// after the status line of a <c>3xx</c> that <c>-L</c> may follow when the request sent a body that is not
/// empty, whether the redirect then sends the body again (<c>307</c>) or drops it (<c>302</c>), and not
/// without <c>-L</c>, after a <c>404</c>, or for <c>-d ''</c> (measured 2026-10-02, BL-1213 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string RewindLine = "Need to rewind upload for next request";

    [TestMethod]
    [DataRow("HTTP/1.1 302 Found")]
    [DataRow("HTTP/1.1 307 Temporary Redirect")]
    public async Task ExecuteAsync_FollowedRedirectAfterABody_ReportsTheRewindRightAfterTheStatusLine(string statusLine)
    {
        RecordingTransferEvents events = await PostToRedirectAsync(statusLine, "ab", followsRedirects: true);

        List<string> all = events.Events;
        int status = all.IndexOf($"< {statusLine}\r\n");
        Assert.AreEqual("* " + RewindLine, all[status + 1], string.Join('\n', all));
        Assert.AreEqual("< Location: /b\r\n", all[status + 2]);
        WriteRewindCount(events, 1);
        Assert.AreEqual(1, events.Info.Count(line => line == RewindLine));
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectAfterABodyNotFollowed_ReportsNoRewind()
    {
        RecordingTransferEvents events = await PostToRedirectAsync("HTTP/1.1 302 Found", "ab", followsRedirects: false);

        WriteRewindCount(events, 0);
        CollectionAssert.DoesNotContain(events.Info.ToList(), RewindLine);
    }

    [TestMethod]
    public async Task ExecuteAsync_FollowedRedirectAfterAnEmptyBody_ReportsNoRewind()
    {
        RecordingTransferEvents events = await PostToRedirectAsync("HTTP/1.1 302 Found", string.Empty, followsRedirects: true);

        WriteRewindCount(events, 0);
        CollectionAssert.DoesNotContain(events.Info.ToList(), RewindLine);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 404 Not Found")]
    [DataRow("HTTP/1.1 200 OK")]
    public async Task ExecuteAsync_ResponseThatIsNotARedirectAfterABody_ReportsNoRewind(string statusLine)
    {
        RecordingTransferEvents events = await PostToRedirectAsync(statusLine, "ab", followsRedirects: true);

        WriteRewindCount(events, 0);
        CollectionAssert.DoesNotContain(events.Info.ToList(), RewindLine);
    }

    [TestMethod]
    public async Task ExecuteAsync_FollowedRedirectWithoutABody_ReportsNoRewind()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("scripted response", "302 Found, Location: /b, Content-Length: 0; no request body, following");

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 0\r\n\r\n", 65536)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:47811/a", events, new HttpRequestOptions { FollowRedirects = true }));

        WriteResult(result);
        WriteRewindCount(events, 0);
        CollectionAssert.DoesNotContain(events.Info.ToList(), RewindLine);
    }

    private async Task<RecordingTransferEvents> PostToRedirectAsync(string statusLine, string body, bool followsRedirects)
    {
        RecordingTransferEvents events = new();
        HttpRequestOptions options = new()
        {
            Body = new BytesBody(System.Text.Encoding.ASCII.GetBytes(body), "application/x-www-form-urlencoded"),
            FollowRedirects = followsRedirects,
        };
        Diagnostics.Arrange("scripted response", $"{statusLine}, Location: /b, Content-Length: 0");
        Diagnostics.Arrange("request body", body.Length == 0 ? "(empty)" : body);
        Diagnostics.Arrange("follows redirects", followsRedirects);

        TransferResult result = await Handler(QueueConnector.For(Connection($"{statusLine}\r\nLocation: /b\r\nContent-Length: 0\r\n\r\n", 65536)))
            .ExecuteAsync(EventsContext("http://127.0.0.1:47811/a", events, options));

        WriteResult(result);
        WriteEvents("events", events.Events);
        return events;
    }

    private void WriteRewindCount(RecordingTransferEvents events, int expected) =>
        Diagnostics.Assert("rewind lines", expected, events.Info.Count(line => line == RewindLine));
}
