using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// The limit of 5000 response headers across a whole transfer: the hops <c>-L</c> follows and
/// a chunked body's trailers count toward it as well as the heads of one exchange. Every case
/// was measured on curl 8.21.0 with <c>-sv -i</c> against a loopback server with
/// <c>Record-CurlExchange.ps1</c> (BL-1448 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_RedirectHopOf3000Headers_ReportsEveryHeaderItStored()
    {
        string response = "HTTP/1.1 302 Found\r\nLocation: /next\r\nContent-Length: 0\r\n" + ValuedHeaders("X-H", 1, 3000) + "\r\n";

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536)))
            .ExecuteAsync(StoredHeadersContext(new MemoryStream(), new MemoryStream(), NoTransferEvents.Instance, storedBefore: 0));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(3002, result.Report!.ResponseHeadersStored);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondRedirectHopOf3000Headers_FailsWithTooLargeAtTheTransfers5001stHeader()
    {
        string accepted = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n" + ValuedHeaders("X-H", 1, 1997);
        string response = accepted + ValuedHeaders("X-H", 1998, 3000) + "\r\nok";

        foreach (int chunkSize in ManyHeaderChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headers = new();
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(StoredHeadersContext(output, headers, events, storedBefore: 3002));

            Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(TooManyResponseHeaders, result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(accepted, Latin1(headers.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
            int lastReceived = events.Events.FindLastIndex(line => line[0] == '<');
            Assert.AreEqual("< X-H1998: v\r\n", events.Events[lastReceived], $"Chunk size {chunkSize}");
            Assert.AreEqual("* " + TooManyResponseHeaders, events.Events[lastReceived + 1], $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_TrailersThatTakeTheCountTo5000_Succeed()
    {
        string response = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nX-A: 1\r\n\r\n2\r\nok\r\n0\r\n" + ValuedHeaders("X-T", 1, 4998) + "\r\n";
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536)))
            .ExecuteAsync(StoredHeadersContext(output, new MemoryStream(), NoTransferEvents.Instance, storedBefore: 0));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        Assert.AreEqual(5000, result.Report!.ResponseHeadersStored);
    }

    [TestMethod]
    public async Task ExecuteAsync_TrailerPastTheTransfers5000thHeader_FailsWithTooLargeAfterWritingTheTrailersBeforeIt()
    {
        string head = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nX-A: 1\r\n\r\n";
        string acceptedTrailers = ValuedHeaders("X-T", 1, 4998);
        string response = head + "2\r\nok\r\n0\r\n" + acceptedTrailers + "X-T4999: v\r\n\r\n";

        foreach (int chunkSize in ManyHeaderChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headers = new();
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(StoredHeadersContext(output, headers, events, storedBefore: 0));

            Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(TooManyResponseHeaders, result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(head + acceptedTrailers, Latin1(headers.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("< X-A: 1\r\n", events.Events.Last(line => line[0] == '<' && line != "< \r\n"), $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(
                new[] { TooManyResponseHeaders, "Failed reading the chunked-encoded stream" },
                events.Info.Skip(events.Info.IndexOf(TooManyResponseHeaders)).Take(2).ToArray(),
                $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_TrailersAfterEarlierHopsHeaders_CountOnFromThem()
    {
        string head = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n";
        string response = head + "2\r\nok\r\n0\r\nX-T1: v\r\nX-T2: v\r\n\r\n";
        MemoryStream headers = new();

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536)))
            .ExecuteAsync(StoredHeadersContext(new MemoryStream(), headers, NoTransferEvents.Instance, storedBefore: 4998));

        Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode);
        Assert.AreEqual(head + "X-T1: v\r\n", Latin1(headers.ToArray()));
    }

    private static TransferContext StoredHeadersContext(Stream output, Stream headerOutput, ITransferEvents events, int storedBefore) =>
        new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = output,
            HeaderOutput = headerOutput,
            Events = events,
            Http = new Abstractions.HttpRequestOptions { FollowRedirects = true, ResponseHeadersStored = storedBefore },
        };

    private static string ValuedHeaders(string prefix, int first, int last)
    {
        StringBuilder headers = new();
        for (int number = first; number <= last; number++)
        {
            headers.Append(prefix).Append(number).Append(": v\r\n");
        }

        return headers.ToString();
    }
}
