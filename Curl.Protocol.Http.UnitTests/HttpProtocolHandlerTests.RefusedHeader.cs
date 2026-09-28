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
