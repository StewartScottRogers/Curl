using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-088 (BL-3948) on <see cref="HttpProtocolHandler" />: curl 8.22.0's <c>failf</c> writes
/// the <c>--max-redirs</c>, <c>--max-filesize</c> and <c>-m</c> failure messages as <c>-v</c> lines
/// (measured: <c>curl -sv -L --max-redirs 0</c> prints <c>* Maximum (0) redirects followed</c>;
/// <c>curl -sv --max-filesize 5</c> against a Content-Length of 100 prints
/// <c>* Maximum file size exceeded</c>; <c>-m 1</c> prints <c>* Operation timed out after ...</c>).
/// </summary>
[TestClass]
public sealed class Cgr088LimitTests
{
    [TestMethod]
    public async Task ExecuteAsync_AuthRetryWithMaxRedirsZero_EchoesTheLimitAsAnInfoLine()
    {
        RecordingTransferEvents events = new();
        TurnTakingConnection connection = new(65536, "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"n\"\r\nContent-Length: 4\r\n\r\nnope", "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Digest x"))
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:20700/a"),
                Output = new MemoryStream(),
                Events = events,
                Credentials = new System.Net.NetworkCredential("a", "b"),
                Http = new HttpRequestOptions { UserAgent = "curl/8.22.0", AuthSchemes = HttpAuthSchemes.Digest, MaxRedirects = 0 },
            });

        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        CollectionAssert.Contains(events.Info, "Maximum (0) redirects followed");
    }
    [TestMethod]
    public async Task ExecuteAsync_ContentLengthOverMaxFilesize_EchoesTheLimitAsAnInfoLine()
    {
        RecordingTransferEvents events = new();

        TransferResult result = await Execute(
            "HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\n0123456789",
            events,
            new HttpRequestOptions { UserAgent = "curl/8.22.0" },
            maxFileSize: 5);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        CollectionAssert.Contains(events.Info, "Maximum file size exceeded");
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerStallsPastMaxTime_EchoesTheTimeoutAsAnInfoLine()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        StalledConnection connection = new(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\nhello"), 65536);
        RecordingTransferEvents events = new();
        Task<TransferResult> transfer = new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:20700/hang/5000"),
                Output = new MemoryStream(),
                Events = events,
                TimeProvider = time,
                MaxTime = TimeSpan.FromSeconds(1),
                Http = new HttpRequestOptions { UserAgent = "curl/8.22.0" },
            }).AsTask();
        await connection.Stalled;
        time.Advance(TimeSpan.FromMilliseconds(1000));

        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(1, events.Info.Count(line => line == "Operation timed out after 1000 milliseconds with 5 out of 100 bytes received"));
    }

    private static ValueTask<TransferResult> Execute(string response, RecordingTransferEvents events, HttpRequestOptions http, long? maxFileSize) =>
        new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(Encoding.Latin1.GetBytes(response), 65536)), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:20700/a"),
                Output = new MemoryStream(),
                Events = events,
                MaxFileSize = maxFileSize,
                Http = http,
            });
}
