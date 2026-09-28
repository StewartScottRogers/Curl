using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Header output when curl refuses a response header while it reads the head. Every case
/// here was measured on curl 8.21.0 with <c>-sS -D -</c> (or <c>-I</c>) and the options named,
/// against a loopback server with <c>Record-CurlExchange.ps1</c>, which sent each response and
/// closed (BL-412 Notes): curl writes the head lines before the refused header and nothing
/// after, not even the line that ends the head.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string InvalidContentLength = "Invalid Content-Length: value";

    private const string ContentEncodingIdentity = "Content-Encoding: identity\r\n";

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Encoding: identity,identity,identity,identity,identity,identity\r\nX-After: 1\r\n\r\nhello", "--compressed", "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n", DisplayName = "Six codings in one header")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n" + ContentEncodingIdentity + ContentEncodingIdentity + ContentEncodingIdentity + ContentEncodingIdentity + ContentEncodingIdentity + ContentEncodingIdentity + "X-After: 1\r\n\r\nhello", "--compressed", "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n" + ContentEncodingIdentity + ContentEncodingIdentity + ContentEncodingIdentity + ContentEncodingIdentity + ContentEncodingIdentity, DisplayName = "Six codings in six headers")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Encoding: identity,identity,identity,identity,identity,identity\r\n\r\n", "--compressed -I", "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n", DisplayName = "Six codings with -I")]
    public async Task ExecuteAsync_ContentCodingsPastTheLimit_WritesTheHeadLinesBeforeThem(string response, string options, string headerOutput)
    {
        await AssertRefusedHeaderAsync(response, options, headerOutput, CurlExitCode.BadContentEncoding, TooManyContentCodings);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: x\r\nContent-Encoding: identity,identity,identity,identity,identity,identity\r\n\r\nhello", "--compressed -I", "HTTP/1.1 200 OK\r\n", DisplayName = "Before six codings, with -I")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nContent-Length: x\r\nX-After: 1\r\n\r\nhello", "", "HTTP/1.1 200 OK\r\nX-Before: 1\r\n", DisplayName = "Invalid value")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nContent-Length: x\r\nX-After: 1\r\n\r\n", "-I", "HTTP/1.1 200 OK\r\nX-Before: 1\r\n", DisplayName = "With -I")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nContent-Length: x\r\nX-After: 1\r\n\r\n", "-X HEAD", "HTTP/1.1 200 OK\r\nX-Before: 1\r\n", DisplayName = "With -X HEAD")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-Mid: 1\r\nContent-Length: 6\r\nX-After: 1\r\n\r\nhello", "", "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-Mid: 1\r\n", DisplayName = "Second header disagrees")]
    [DataRow("HTTP/1.1 404 Not Found\r\nContent-Length: x\r\n\r\nhello", "-f", "HTTP/1.1 404 Not Found\r\n", DisplayName = "Before -f")]
    [DataRow("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: x\r\n\r\n", "-L", "HTTP/1.1 302 Found\r\nLocation: /b\r\n", DisplayName = "Before a redirect is followed")]
    [DataRow("HTTP/1.1 100 Continue\r\nX-C: 1\r\n\r\nHTTP/1.1 200 OK\r\nX-Before: 1\r\nContent-Length: x\r\n\r\nhello", "", "HTTP/1.1 100 Continue\r\nX-C: 1\r\n\r\nHTTP/1.1 200 OK\r\nX-Before: 1\r\n", DisplayName = "After a 100 head")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Fold: a\r\n b\r\nContent-Length: x\r\n\r\nhello", "", "HTTP/1.1 200 OK\r\nX-Fold: a b\r\n", DisplayName = "After a folded header")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: x\r\nX-After: 1\r\n\r\n0\r\n\r\n", "", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n", DisplayName = "After chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: x\r\nX-After: 1\r\n\r\n0\r\n\r\n", "--raw", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n", DisplayName = "After chunked, with --raw")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: x\r\nX-After: 1\r\n\r\n0\r\n\r\n", "--tr-encoding", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n", DisplayName = "After chunked, with --tr-encoding")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: x\r\nX-After: 1\r\n\r\n0\r\n\r\n", "--tr-encoding --raw", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n", DisplayName = "After chunked, with --tr-encoding --raw")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: identity\r\nContent-Length: 5,x\r\nX-After: 1\r\n\r\nhello", "--tr-encoding", "HTTP/1.1 200 OK\r\nTransfer-Encoding: identity\r\n", DisplayName = "After identity, with --tr-encoding")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: 3\r\nContent-Length: 4\r\nX-After: 1\r\n\r\n5\r\nhello\r\n0\r\n\r\n", "--tr-encoding", "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: 3\r\n", DisplayName = "Two disagreeing after chunked, with --tr-encoding")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nTransfer-Encoding: chunked\r\nContent-Length: 6\r\nX-After: 1\r\n\r\n5\r\nhello\r\n0\r\n\r\n", "--tr-encoding", "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nTransfer-Encoding: chunked\r\n", DisplayName = "Disagreeing before and after chunked, with --tr-encoding")]
    public async Task ExecuteAsync_InvalidContentLength_WritesTheHeadLinesBeforeIt(string response, string options, string headerOutput)
    {
        await AssertRefusedHeaderAsync(response, options, headerOutput, CurlExitCode.WeirdServerReply, InvalidContentLength);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nTransfer-Encoding: foo\r\nX-After: 1\r\n\r\nhello", "", "Unsolicited Transfer-Encoding (foo) found", DisplayName = "Unsolicited")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nTransfer-Encoding: foo\r\nX-After: 1\r\n\r\n", "-X HEAD", "Unsolicited Transfer-Encoding (foo) found", DisplayName = "Unsolicited, with -X HEAD")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nTransfer-Encoding: chunked, identity\r\nX-After: 1\r\n\r\n0\r\n\r\n", "", "A Transfer-Encoding (identity) was listed after chunked", DisplayName = "Listed after chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nTransfer-Encoding: chunked, gzip\r\nX-After: 1\r\n\r\n0\r\n\r\n", "--tr-encoding", "Reject response due to 'chunked' not being the last Transfer-Encoding", DisplayName = "Chunked not last, with --tr-encoding")]
    public async Task ExecuteAsync_RefusedTransferEncoding_WritesTheHeadLinesBeforeIt(string response, string options, string message)
    {
        await AssertRefusedHeaderAsync(response, options, "HTTP/1.1 200 OK\r\nX-Before: 1\r\n", CurlExitCode.BadContentEncoding, message);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferCodingsPastTheLimit_WritesTheHeadLinesBeforeTheHeaderPastIt()
    {
        const string accepted = "HTTP/1.1 200 OK\r\nX-Before: 1\r\nTransfer-Encoding: identity,identity,identity\r\n";
        await AssertRefusedHeaderAsync(
            accepted + "Transfer-Encoding: identity,identity,identity\r\nX-After: 1\r\n\r\nhello",
            "--tr-encoding",
            accepted,
            CurlExitCode.BadContentEncoding,
            "Reject response exceeding limit of 5 transfer encodings");
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nTransfer-Encoding: foo\r\nX-After: 1\r\n\r\n", "-I", DisplayName = "Unsolicited Transfer-Encoding, with -I")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nTransfer-Encoding: chunked, identity\r\nX-After: 1\r\n\r\n", "-I", DisplayName = "Listed after chunked, with -I")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nTransfer-Encoding: chunked, gzip\r\nX-After: 1\r\n\r\n", "--tr-encoding -I", DisplayName = "Chunked not last, with --tr-encoding -I")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Before: 1\r\nTransfer-Encoding: identity,identity,identity\r\nTransfer-Encoding: identity,identity,identity\r\nX-After: 1\r\n\r\n", "--tr-encoding -I", DisplayName = "Six transfer codings, with --tr-encoding -I")]
    [DataRow("HTTP/1.1 204 No Content\r\nX-Before: 1\r\nTransfer-Encoding: foo\r\nX-After: 1\r\n\r\n", "", DisplayName = "Unsolicited Transfer-Encoding in a 204")]
    [DataRow("HTTP/1.1 304 Not Modified\r\nX-Before: 1\r\nTransfer-Encoding: foo\r\nX-After: 1\r\n\r\n", "", DisplayName = "Unsolicited Transfer-Encoding in a 304")]
    [DataRow("HTTP/1.1 204 No Content\r\nX-Before: 1\r\nContent-Length: x\r\nX-After: 1\r\n\r\n", "-I", DisplayName = "Invalid Content-Length in a 204, with -I")]
    [DataRow("HTTP/1.1 304 Not Modified\r\nX-Before: 1\r\nContent-Length: x\r\nX-After: 1\r\n\r\n", "", DisplayName = "Invalid Content-Length in a 304")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: x\r\nX-After: 1\r\n\r\n", "--ignore-content-length -I", DisplayName = "Invalid Content-Length, with --ignore-content-length -I")]
    public async Task ExecuteAsync_HeaderRefusedOnlyWithABody_WritesTheWholeHead(string response, string options)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(new MemoryStream(), headerOutput, options));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Assert.AreEqual(response, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: x\r\nX-After: 1\r\n\r\n", "--ignore-content-length", DisplayName = "Invalid Content-Length, with --ignore-content-length")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: foo\r\nX-After: 1\r\n\r\n", "--raw", DisplayName = "Unsolicited Transfer-Encoding, with --raw")]
    public async Task ExecuteAsync_HeaderAcceptedByTheOptions_WritesTheWholeHeadAndTheBody(string head, string options)
    {
        await AssertAcceptedHeadAsync(head, "hello", "hello", options);
    }

    /// <summary>
    /// Measured with <c>-sS --tr-encoding -D -</c> (BL-454 Notes): a Content-Length after the
    /// Transfer-Encoding header that is valid, too large to hold, or ignored is not used, and the
    /// chunked body is decoded.
    /// </summary>
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: 3\r\nX-After: 1\r\n\r\n", "--tr-encoding", DisplayName = "Valid, with --tr-encoding")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: 99999999999999999999\r\nX-After: 1\r\n\r\n", "--tr-encoding", DisplayName = "Too large, with --tr-encoding")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: x\r\nX-After: 1\r\n\r\n", "--tr-encoding --ignore-content-length", DisplayName = "Invalid, with --tr-encoding --ignore-content-length")]
    public async Task ExecuteAsync_ContentLengthAfterChunkedAccepted_WritesTheWholeHeadAndTheDecodedBody(string head, string options)
    {
        await AssertAcceptedHeadAsync(head, "5\r\nhello\r\n0\r\n\r\n", "hello", options);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputIsTheOutput_WritesOnlyTheHeadLinesBeforeTheRefusedHeader()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nX-Before: 1\r\nContent-Length: x\r\nX-After: 1\r\n\r\nhello", chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, output, string.Empty));

            Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/1.1 200 OK\r\nX-Before: 1\r\n", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with <c>-w "%{size_header} %{num_headers} %header{x-b2}|%header{content-length}"</c>:
    /// <c>39 2 2|</c>, so the report holds only the head before the refused header.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_RefusedHeader_ReportsOnlyTheHeadBeforeIt()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nX-Before: 1\r\nX-B2: 2\r\nContent-Length: x\r\n\r\nhello", chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(new MemoryStream(), null, string.Empty));

            Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(39L, result.Report!.HeaderSize, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(
                new[] { KeyValuePair.Create("X-Before", "1"), KeyValuePair.Create("X-B2", "2") },
                result.Report.ResponseHeaders.ToArray(),
                $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with <c>-s -v</c> and <c>-sS -D -</c> (BL-479 Notes): curl 8.21.0 stops reading
    /// the head at the header it refuses, so a line after it that would fail the head is never
    /// read. It fails with the refused header's error and reports and writes only the head
    /// lines before that header.
    /// </summary>
    [TestMethod]
    [DataRow("Content-Length: x\r\n", CurlExitCode.WeirdServerReply, InvalidContentLength, DisplayName = "Invalid Content-Length")]
    [DataRow("Transfer-Encoding: bogus\r\n", CurlExitCode.BadContentEncoding, "Unsolicited Transfer-Encoding (bogus) found", DisplayName = "Unsolicited Transfer-Encoding")]
    public async Task ExecuteAsync_HeadFailsAfterTheRefusedHeader_FailsWithTheRefusedHeadersError(string refusedHeader, CurlExitCode exitCode, string message)
    {
        string response = "HTTP/1.1 200 OK\r\nX-Before: 1\r\n" + refusedHeader + "X-After: 1\r\nno colon\r\n\r\n";
        string[] expected = ["< HTTP/1.1 200 OK\r\n", "< X-Before: 1\r\n"];
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            MemoryStream headers = new();
            TransferContext context = RefusedHeaderContext(new MemoryStream(), headers, string.Empty, events);

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize))).ExecuteAsync(context);

            Assert.AreEqual(exitCode, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, result.ErrorMessage, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(expected, HeadEvents(events), $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/1.1 200 OK\r\nX-Before: 1\r\n", Latin1(headers.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with <c>-s -v</c> against a server that sent this and held the connection open
    /// (<c>Record-CurlExchange.ps1 -HoldOpenMilliseconds</c>, BL-480 Notes): curl 8.21.0 exited 8
    /// within 60 ms, once a byte of the line after the refused header showed it whole, with
    /// only the head lines before it.
    /// </summary>
    [TestMethod]
    [DataRow("X-After: 1\r\n", DisplayName = "Whole line after it")]
    [DataRow("X", DisplayName = "One byte after it")]
    public async Task ExecuteAsync_PeerHoldsTheHeadOpenAfterTheRefusedHeader_FailsWithoutWaiting(string after)
    {
        string response = "HTTP/1.1 200 OK\r\nX-Before: 1\r\nContent-Length: x\r\n" + after;
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            MemoryStream headers = new();
            TransferContext context = RefusedHeaderContext(new MemoryStream(), headers, string.Empty, events);

            TransferResult result = await Handler(QueueConnector.For(new StalledConnection(Encoding.Latin1.GetBytes(response), chunkSize)))
                .ExecuteAsync(context).AsTask().WaitAsync(TimeSpan.FromSeconds(30));

            Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(InvalidContentLength, result.ErrorMessage, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new[] { "< HTTP/1.1 200 OK\r\n", "< X-Before: 1\r\n" }, HeadEvents(events), $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/1.1 200 OK\r\nX-Before: 1\r\n", Latin1(headers.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with <c>-s -v -m 3</c> the same way (BL-480 Notes): with nothing after the
    /// refused header's line, or only a blank that could fold the next line into it, curl
    /// 8.21.0 cannot tell the header is whole and waits, here until <c>-m</c> fails it with
    /// <c>Operation timed out after 3010 milliseconds with 0 bytes received</c>.
    /// </summary>
    [TestMethod]
    [DataRow("", DisplayName = "Nothing after it")]
    [DataRow(" ", DisplayName = "A blank after it")]
    public async Task ExecuteAsync_PeerHoldsTheHeadOpenRightAfterTheRefusedHeader_WaitsForMaxTime(string after)
    {
        string response = "HTTP/1.1 200 OK\r\nX-Before: 1\r\nContent-Length: x\r\n" + after;
        foreach (int chunkSize in ChunkSizes)
        {
            FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
            StalledConnection connection = new(Encoding.Latin1.GetBytes(response), chunkSize);
            TransferContext context = new()
            {
                Url = CurlUrl.Parse("http://example.com/"),
                Output = new MemoryStream(),
                TimeProvider = time,
                MaxTime = TimeSpan.FromSeconds(1),
            };

            Task<TransferResult> transfer = Handler(QueueConnector.For(connection)).ExecuteAsync(context).AsTask();
            await connection.Stalled;
            Assert.IsFalse(transfer.IsCompleted, $"Chunk size {chunkSize}: ended before -m passed.");
            time.Advance(TimeSpan.FromSeconds(1));
            TransferResult result = await transfer;

            Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Operation timed out after 1000 milliseconds with 0 bytes received", result.ErrorMessage, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with <c>-s -v</c>, <c>-sS -D -</c> and <c>-s -w %{num_headers}</c> against a
    /// server that sent this and closed (BL-483 Notes): curl 8.21.0 acts on a header only once a
    /// byte of the next line shows it whole, so it never acts on the header the head ends on at
    /// close, refused or not. It exits 0, reports and writes every head line, counts that
    /// header, and leaves the connection intact.
    /// </summary>
    [TestMethod]
    [DataRow("Content-Length: x\r\n", DisplayName = "Invalid Content-Length")]
    [DataRow("Transfer-Encoding: bogus\r\n", DisplayName = "Unsolicited Transfer-Encoding")]
    [DataRow("Content-Length: 5\r\n", DisplayName = "Content-Length the body falls short of")]
    [DataRow("X-After: 1\r\n", DisplayName = "No framing header")]
    public async Task ExecuteAsync_PeerClosesRightAfterAHeader_NeverActsOnIt(string lastHeader)
    {
        string response = "HTTP/1.1 200 OK\r\nX-Before: 1\r\n" + lastHeader;
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            MemoryStream headers = new();
            TransferContext context = new() { Url = CurlUrl.Parse(ReuseUrl), Output = output, HeaderOutput = headers, Events = events };

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(response, chunkSize), null, connectionNumber: 0)))
                .ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            CollectionAssert.AreEqual(new[] { "< HTTP/1.1 200 OK\r\n", "< X-Before: 1\r\n", "< " + lastHeader }, HeadEvents(events), $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(InfoLines(string.Empty, "Connection #0 to host 127.0.0.1:18977 left intact"), events.Info, $"Chunk size {chunkSize}");
            Assert.AreEqual(response, Latin1(headers.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(2, result.Report!.ResponseHeaders, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with <c>-s -v</c>, <c>-s -v -f</c> and <c>-sS -D -</c> against a server that sent
    /// this and closed (BL-485 Notes): curl 8.21.0 acted on the Content-Length before the last
    /// header, so it fails with exit 18, prints the failure before the last header's line -
    /// ahead of any <c>-f</c> failure - writes every head line, and closes the connection.
    /// </summary>
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\n", DisplayName = "200")]
    [DataRow("HTTP/1.1 404 Not Found\r\n", DisplayName = "404 with -f")]
    public async Task ExecuteAsync_PeerClosesAmongHeadersAfterAContentLength_FailsWithTransferClosed(string statusLine)
    {
        string response = statusLine + "Content-Length: 5\r\nX-Before: 1\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            MemoryStream headers = new();
            TransferContext context = new()
            {
                Url = CurlUrl.Parse(ReuseUrl),
                Output = new MemoryStream(),
                HeaderOutput = headers,
                Events = events,
                Http = new HttpRequestOptions { Fail = HttpFailMode.Fail },
            };

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(response, chunkSize), null, connectionNumber: 0)))
                .ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("transfer closed with 5 bytes remaining to read", result.ErrorMessage, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(
                new[]
                {
                    "* using HTTP/1.x",
                    "* Request completely sent off",
                    "< " + statusLine,
                    "< Content-Length: 5\r\n",
                    "* transfer closed with 5 bytes remaining to read",
                    "< X-Before: 1\r\n",
                    "* closing connection #0",
                },
                events.Events.Where(line => line[0] is '*' or '<').ToArray(),
                $"Chunk size {chunkSize}");
            Assert.AreEqual(response, Latin1(headers.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with <c>-s -v</c>, <c>-s -v -I</c> and <c>-s -v --ignore-content-length</c>
    /// against a server that sent this and closed (BL-485 Notes): exit 0, the connection left
    /// intact - a Content-Length of zero, or one curl does not frame a body by, leaves nothing
    /// to read.
    /// </summary>
    [TestMethod]
    [DataRow("Content-Length: 0\r\n", false, false, DisplayName = "Content-Length: 0")]
    [DataRow("Content-Length: 5\r\n", true, false, DisplayName = "-I")]
    [DataRow("Content-Length: 5\r\n", false, true, DisplayName = "--ignore-content-length")]
    public async Task ExecuteAsync_PeerClosesAmongHeadersAfterAContentLengthItReadsNothingBy_Succeeds(string contentLength, bool noBody, bool ignoreContentLength)
    {
        string response = "HTTP/1.1 200 OK\r\n" + contentLength + "X-Before: 1\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            TransferContext context = new()
            {
                Url = CurlUrl.Parse(ReuseUrl),
                Output = new MemoryStream(),
                Events = events,
                NoBody = noBody,
                Http = new HttpRequestOptions { IgnoreContentLength = ignoreContentLength },
            };

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(response, chunkSize), null, connectionNumber: 0)))
                .ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Assert.AreEqual("< X-Before: 1\r\n", HeadEvents(events)[^1], $"Chunk size {chunkSize}");
            Assert.AreEqual("Connection #0 to host 127.0.0.1:18977 left intact", events.Info[^1], $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with <c>-s -v</c> against a server that sent only the status line and closed
    /// (BL-483 Notes): exit 0, the connection left intact.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PeerClosesRightAfterTheStatusLine_LeavesTheConnectionIntact()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection("HTTP/1.1 200 OK\r\n", chunkSize), null, connectionNumber: 0)))
                .ExecuteAsync(ReuseContext(events));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            CollectionAssert.AreEqual(InfoLines(string.Empty, "Connection #0 to host 127.0.0.1:18977 left intact"), events.Info, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured with <c>-s -L -w "[%{redirect_url}][%{num_redirects}]"</c> against a server that
    /// sent this and closed (BL-483 Notes): <c>[][0]</c>, exit 0 - curl never acts on the
    /// Location the head ends on at close, so it neither follows nor reports it.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PeerClosesRightAfterALocation_NeitherFollowsNorReportsIt()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 302 Found\r\nX-Before: 1\r\nLocation: /b\r\n", chunkSize)))
                .ExecuteAsync(FollowContext("http://example.com/", new MemoryStream()));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Assert.IsNull(result.Report!.RedirectUrl, $"Chunk size {chunkSize}");
            Assert.AreEqual(0, result.Report.RedirectCount, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadFailsWithNoHeaderRefused_FailsWithTheHeadsError()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            TransferContext context = RefusedHeaderContext(new MemoryStream(), null, string.Empty, events);

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nX-Before: 1\r\nno colon\r\n\r\n", chunkSize))).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Header without colon", result.ErrorMessage, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new[] { "< HTTP/1.1 200 OK\r\n", "< X-Before: 1\r\n" }, HeadEvents(events), $"Chunk size {chunkSize}");
        }
    }

    private static async Task AssertAcceptedHeadAsync(string head, string body, string decodedBody, string options)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(head + body, chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, output, options));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Assert.AreEqual(head + decodedBody, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    private static async Task AssertRefusedHeaderAsync(string response, string options, string headerOutput, CurlExitCode exitCode, string message)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headers = new();

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, headers, options));

            Assert.AreEqual(exitCode, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(headerOutput, Latin1(headers.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
        }
    }

    private static TransferContext RefusedHeaderContext(Stream output, Stream? headerOutput, string options, ITransferEvents? events = null)
    {
        string[] flags = options.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = output,
            HeaderOutput = headerOutput,
            Events = events ?? NoTransferEvents.Instance,
            NoBody = flags.Contains("-I"),
            Http = new HttpRequestOptions
            {
                Compressed = flags.Contains("--compressed"),
                Raw = flags.Contains("--raw"),
                TransferEncoding = flags.Contains("--tr-encoding"),
                IgnoreContentLength = flags.Contains("--ignore-content-length"),
                Fail = flags.Contains("-f") ? HttpFailMode.Fail : HttpFailMode.None,
                FollowRedirects = flags.Contains("-L"),
                CustomMethod = flags.Contains("HEAD") ? "HEAD" : null,
            },
        };
    }
}
