using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpResponseHeadReader" /> against curl 8.21.0 (mingw, Schannel), measured
/// against a loopback server that sent each response below and closed (BL-169 Notes). Every
/// case is replayed with 1-byte reads, 7-byte reads and one read, and must come out the same.
/// </summary>
[TestClass]
public sealed class HttpResponseHeadReaderTests
{
    private static readonly int[] ChunkSizes = [1, 7, 65536];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadAsync_Http11StatusLineAndHeaders_Parse()
    {
        const string Head = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 2\r\n\r\n";

        foreach (HttpResponseHead head in await ReadEveryWayAsync(Head + "hi"))
        {
            AssertStatus(head, HttpVersion.Version11, 200, "OK");
            AssertHeaders(head, "Content-Type", "text/plain", "Content-Length", "2");
            Assert.AreEqual(Head, Latin1(head.HeadBytes));
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi", DisplayName = "CRLF head")]
    [DataRow("HTTP/1.0 200 OK\nContent-Length: 2\n\nhi", DisplayName = "LF head")]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\n\r\nhi", DisplayName = "After 100 Continue")]
    public async Task ReadAsync_BytesAfterTheHead_AreLeftForTheBodyUnconsumed(string response)
    {
        foreach (string body in await ReadBodiesEveryWayAsync(response))
        {
            Diagnostics.Assert("body", "hi", body);
            Assert.AreEqual("hi", body);
        }
    }

    [TestMethod]
    public async Task ReadAsync_Http10StatusLine_Parses()
    {
        foreach (HttpResponseHead head in await ReadEveryWayAsync("HTTP/1.0 404 Not Found\r\n\r\n"))
        {
            AssertStatus(head, HttpVersion.Version10, 404, "Not Found");
            Assert.IsEmpty(head.Headers);
            Assert.AreEqual(0, head.BodyPrefix.Length);
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200\r\n", "", DisplayName = "No reason phrase")]
    [DataRow("HTTP/1.1 200OK\r\n", "OK", DisplayName = "Reason phrase straight after the code")]
    [DataRow("HTTP/1.1\t404 X\r\n", "X", DisplayName = "Tab before the code")]
    [DataRow("HTTP/1.1 204  \tNo Content\r\n", "No Content", DisplayName = "Blanks before the reason phrase")]
    public async Task ReadAsync_StatusLineVariantsCurlAccepts_Parse(string statusLine, string reasonPhrase)
    {
        foreach (HttpResponseHead head in await ReadEveryWayAsync(statusLine + "\r\n"))
        {
            Diagnostics.Assert("reason phrase", reasonPhrase, head.StatusLine.ReasonPhrase);
            Assert.AreEqual(reasonPhrase, head.StatusLine.ReasonPhrase);
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 999 X\r\n", 999, DisplayName = "Code 999")]
    [DataRow("HTTP/1.1 100 X\r\n\r\nHTTP/1.1 204 X\r\n", 204, DisplayName = "Code after 100 Continue")]
    public async Task ReadAsync_ThreeDigitStatusCodes_Parse(string statusLines, int statusCode)
    {
        foreach (HttpResponseHead head in await ReadEveryWayAsync(statusLines + "\r\n"))
        {
            Diagnostics.Assert("status code", statusCode, head.StatusLine.StatusCode);
            Assert.AreEqual(statusCode, head.StatusLine.StatusCode);
        }
    }

    [TestMethod]
    [DataRow("http/1.1 404 x", DisplayName = "Lower-case http/")]
    [DataRow("HTTP/2 404", DisplayName = "HTTP/2 over HTTP/1.x")]
    [DataRow("HTTP/3.0 404 OK", DisplayName = "HTTP/3.0 over HTTP/1.x")]
    public async Task ReadAsync_StatusLinesCurlTakesAsHttp10Ok_ParseAs200(string statusLine)
    {
        foreach (HttpResponseHead head in await ReadEveryWayAsync(statusLine + "\r\nContent-Length: 0\r\n\r\n"))
        {
            AssertStatus(head, HttpVersion.Version10, 200, string.Empty);
            Assert.AreEqual(statusLine + "\r\nContent-Length: 0\r\n\r\n", Latin1(head.HeadBytes));
        }
    }

    [TestMethod]
    public async Task ReadAsync_Continue100_IsSkippedWithItsHeadKeptInTheHeadBytes()
    {
        const string Response = "HTTP/1.1 100 Continue\r\nX-Early: 1\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi";

        foreach (HttpResponseHead head in await ReadEveryWayAsync(Response))
        {
            AssertStatus(head, HttpVersion.Version11, 200, "OK");
            AssertHeaders(head, "Content-Length", "2");
            Assert.AreEqual(Response[..^2], Latin1(head.HeadBytes));
        }
    }

    [TestMethod]
    public async Task ReadAsync_SeveralInformationalResponses_AreAllSkipped()
    {
        const string Response = "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 102 Processing\r\n\r\nHTTP/1.1 103 Early Hints\r\nLink: </a>\r\n\r\nHTTP/1.1 201 Created\r\n\r\n";

        foreach (HttpResponseHead head in await ReadEveryWayAsync(Response))
        {
            Diagnostics.Assert("status code", 201, head.StatusLine.StatusCode);
            Assert.AreEqual(201, head.StatusLine.StatusCode);
            Assert.IsEmpty(head.Headers);
            Assert.AreEqual(Response, Latin1(head.HeadBytes));
        }
    }

    [TestMethod]
    public async Task ReadAsync_LineFeedOnlyLines_ParseAndKeepTheirTerminators()
    {
        const string Head = "HTTP/1.0 200 OK\nX-A: 1\nContent-Length: 2\n\n";

        foreach (HttpResponseHead head in await ReadEveryWayAsync(Head + "hi"))
        {
            AssertStatus(head, HttpVersion.Version10, 200, "OK");
            AssertHeaders(head, "X-A", "1", "Content-Length", "2");
            Assert.AreEqual(Head, Latin1(head.HeadBytes));
        }
    }

    [TestMethod]
    public async Task ReadAsync_FoldedHeader_JoinsItsContinuationLinesWithOneSpace()
    {
        // Measured: curl -D wrote "X-A: 1 b c\r\n" and %{size_header} was 50.
        const string Response = "HTTP/1.1 200 OK\r\nX-A: 1  \r\n   b  \r\n \t c\r\nContent-Length: 0\r\n\r\n";

        foreach (HttpResponseHead head in await ReadEveryWayAsync(Response))
        {
            AssertHeaders(head, "X-A", "1 b c", "Content-Length", "0");
            Assert.AreEqual("HTTP/1.1 200 OK\r\nX-A: 1 b c\r\nContent-Length: 0\r\n\r\n", Latin1(head.HeadBytes));
            Assert.AreEqual(50, head.HeadBytes.Length);
        }
    }

    [TestMethod]
    public async Task ReadAsync_FoldedHeaderWithLineFeedOnlyLines_Joins()
    {
        // Measured: curl -D wrote "X-A: 1 b\n" and %{size_header} was 26.
        foreach (HttpResponseHead head in await ReadEveryWayAsync("HTTP/1.1 200 OK\nX-A: 1\n b\n\n"))
        {
            AssertHeaders(head, "X-A", "1 b");
            Assert.AreEqual("HTTP/1.1 200 OK\nX-A: 1 b\n\n", Latin1(head.HeadBytes));
        }
    }

    [TestMethod]
    public async Task ReadAsync_BlankContinuationLine_LeavesOneTrailingSpace()
    {
        // Measured: curl -D wrote "X-A: 1 \r\n" and %{size_header} was 28.
        foreach (HttpResponseHead head in await ReadEveryWayAsync("HTTP/1.1 200 OK\r\nX-A: 1\r\n \r\n\r\n"))
        {
            AssertHeaders(head, "X-A", "1");
            Assert.AreEqual("HTTP/1.1 200 OK\r\nX-A: 1 \r\n\r\n", Latin1(head.HeadBytes));
        }
    }

    [TestMethod]
    [DataRow(": x", "", "x", DisplayName = "Empty name")]
    [DataRow("X-A : 1", "X-A ", "1", DisplayName = "Blank before the colon")]
    [DataRow("X-A:", "X-A", "", DisplayName = "Empty value")]
    [DataRow("X-A: a:b ", "X-A", "a:b", DisplayName = "Colon in the value")]
    public async Task ReadAsync_HeaderLinesCurlAccepts_SplitAtTheFirstColon(string line, string name, string value)
    {
        foreach (HttpResponseHead head in await ReadEveryWayAsync($"HTTP/1.1 200 OK\r\n{line}\r\n\r\n"))
        {
            AssertHeaders(head, name, value);
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\n", 17, DisplayName = "Right after the status line")]
    [DataRow("HTTP/1.1 200 OK\r\nX-A: 1\r\nX-B", 25, DisplayName = "Inside a header line")]
    [DataRow("HTTP/1.1 200 OK\r\nX-A: 1\r\n \r\n", 26, DisplayName = "After a continuation line")]
    public async Task ReadAsync_PeerClosingAmongTheFinalHeaders_EndsTheHead(string response, int headSize)
    {
        // Measured: curl 8.21.0 exits 0 with %{http_code} 200 and this %{size_header}.
        foreach (HttpResponseHead head in await ReadEveryWayAsync(response))
        {
            Diagnostics.Assert("status code", 200, head.StatusLine.StatusCode);
            Diagnostics.Assert("head size", headSize, head.HeadBytes.Length);
            Assert.AreEqual(200, head.StatusLine.StatusCode);
            Assert.AreEqual(headSize, head.HeadBytes.Length);
            Assert.AreEqual(0, head.BodyPrefix.Length);
        }
    }

    [TestMethod]
    public async Task ReadAsync_PeerClosingInsideAHeaderLine_KeepsTheHeadersBeforeItAndNoBody()
    {
        foreach (HttpResponseHead head in await ReadEveryWayAsync("HTTP/1.1 200 OK\r\nX-A: 1\r\nX-B"))
        {
            AssertHeaders(head, "X-A", "1");
        }
    }

    [TestMethod]
    [DataRow("hello world\r\n\r\n", DisplayName = "Plain text")]
    [DataRow("ICY 200 OK\r\n\r\n", DisplayName = "ICY status line")]
    [DataRow(" HTTP/1.1 404 X\r\n\r\n", DisplayName = "Blank before HTTP/")]
    [DataRow("HTTP\r\n\r\n", DisplayName = "HTTP without a slash")]
    public async Task ReadAsync_NoStatusLine_ReturnsUnsupportedProtocol(string response)
    {
        await AssertFailsEveryWayAsync(response, CurlExitCode.UnsupportedProtocol, "Received HTTP/0.9 when not allowed");
    }

    [TestMethod]
    [DataRow("hello world\r\n\r\n", DisplayName = "Plain text")]
    [DataRow("HTTP\r\nX: y\r\n\r\n", DisplayName = "HTTP without a slash")]
    [DataRow("ICY", DisplayName = "Fewer bytes than HTTP/")]
    public async Task ReadAsync_NoStatusLineWithHttp09Accepted_GivesAnHttp09HeadWithEveryByteAsTheBody(string response)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            Diagnostics.Arrange("scripted response", Describe(response));
            Diagnostics.Arrange("chunk size", chunkSize);
            ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), chunkSize);
            HttpResponseHeadReader reader = new(connection) { AcceptsHttp09 = true };
            HttpResponseHead head = await reader.ReadAsync(CancellationToken.None);
            byte[] rest = new byte[65536];
            int restLength = 0;
            int read;
            while ((read = await connection.ReadAsync(rest.AsMemory(restLength), CancellationToken.None)) > 0)
            {
                restLength += read;
            }

            Diagnostics.Act($"head and rest with chunk size {chunkSize}", $"version {head.StatusLine.Version}, body prefix {head.BodyPrefix.Length}, rest {restLength}, ended at empty line {reader.EndedAtEmptyLine}");
            AssertStatus(head, new Version(0, 9), 0, string.Empty);
            Assert.IsEmpty(head.Headers);
            Assert.AreEqual(0, head.HeadBytes.Length);
            Assert.IsTrue(reader.EndedAtEmptyLine);
            Assert.AreEqual(response, Latin1(head.BodyPrefix) + Encoding.Latin1.GetString(rest, 0, restLength), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ReadAsync_StatusLineWithHttp09Accepted_ParsesTheStatusLine()
    {
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK\\r\\n\\r\\nhi, 1-byte reads, HTTP/0.9 accepted");
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n\r\nhi"), 1);

        HttpResponseHead head = await new HttpResponseHeadReader(connection) { AcceptsHttp09 = true }.ReadAsync(CancellationToken.None);
        Diagnostics.Act("head", $"version {head.StatusLine.Version}, status {head.StatusLine.StatusCode}, reason '{head.StatusLine.ReasonPhrase}'");

        AssertStatus(head, HttpVersion.Version11, 200, "OK");
    }

    [TestMethod]
    public async Task ReadAsync_ReaderWithoutDefersFrom_TellsEveryFinalHeaderBeforeReturning()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            Diagnostics.Arrange("scripted response", $"HTTP/1.1 200 OK, headers A and B, chunk size {chunkSize}, no DefersFrom");
            ScriptedConnection connection = new(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nA: 1\r\nB: 2\r\n\r\nhi"), chunkSize);
            List<string> told = [];
            HttpResponseHeadReader reader = new(connection) { HeaderReceived = (_, header) => told.Add(header.Name) };

            await reader.ReadAsync(CancellationToken.None);
            Diagnostics.Act("headers told before ReportHeldLines", string.Join(",", told));

            Diagnostics.Assert("headers told", "A,B", string.Join(",", told));
            CollectionAssert.AreEqual(new[] { "A", "B" }, told, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ReadAsync_Http2StreamWithHttp09Accepted_ParsesTheHttp2StatusLine()
    {
        Diagnostics.Arrange("scripted response", "HTTP/2 200 \\r\\n\\r\\n, one read, HTTP/0.9 accepted, HTTP/2 or HTTP/3 stream");
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes("HTTP/2 200 \r\n\r\n"), 65536);

        HttpResponseHead head = await new HttpResponseHeadReader(connection) { AcceptsHttp09 = true, IsHttp2OrHttp3 = true }.ReadAsync(CancellationToken.None);
        Diagnostics.Act("head", $"version {head.StatusLine.Version}, status {head.StatusLine.StatusCode}, reason '{head.StatusLine.ReasonPhrase}'");

        AssertStatus(head, HttpVersion.Version20, 200, string.Empty);
    }

    [TestMethod]
    public async Task ReadAsync_NoBytesWithHttp09Accepted_IsAnEmptyReply()
    {
        Diagnostics.Arrange("scripted response", "no bytes, HTTP/0.9 accepted");
        ScriptedConnection connection = new([], 65536);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await new HttpResponseHeadReader(connection) { AcceptsHttp09 = true }.ReadAsync(CancellationToken.None));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name}, exit code {thrown.ExitCode}, message '{thrown.Message}'");
        Diagnostics.Assert("exit code", CurlExitCode.GotNothing, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.GotNothing, thrown.ExitCode);
    }

    [TestMethod]
    public async Task ReadAsync_NoStatusLine_IsRejectedBeforeTheLineIsWhole()
    {
        // The read after "ICY" would fail; curl gives up on HTTP/0.9 bytes before reading it.
        await AssertFailsEveryWayAsync(
            "ICY",
            CurlExitCode.UnsupportedProtocol,
            "Received HTTP/0.9 when not allowed",
            new IOException("never reached"));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 abc OK", DisplayName = "Letters for a code")]
    [DataRow("HTTP/1.1 20 OK", DisplayName = "Two-digit code")]
    [DataRow("HTTP/1.2 200 OK", DisplayName = "HTTP/1.2")]
    [DataRow("HTTP/1.1  200 OK", DisplayName = "Two blanks before the code")]
    [DataRow("HTTP/1x1 200 OK", DisplayName = "No dot after the major version")]
    [DataRow("HTTP/1.1 20", DisplayName = "Line too short for a code")]
    public async Task ReadAsync_MalformedHttp1StatusLine_ReturnsUnsupportedProtocol(string statusLine)
    {
        await AssertFailsEveryWayAsync(statusLine + "\r\n\r\n", CurlExitCode.UnsupportedProtocol, "Unsupported HTTP/1 subversion in response");
    }

    [TestMethod]
    [DataRow("HTTP/9 500", DisplayName = "HTTP/9")]
    [DataRow("HTTP/", DisplayName = "No version at all")]
    public async Task ReadAsync_UnsupportedHttpVersion_ReturnsUnsupportedProtocol(string statusLine)
    {
        await AssertFailsEveryWayAsync(statusLine + "\r\n\r\n", CurlExitCode.UnsupportedProtocol, "Unsupported HTTP version in response");
    }

    [TestMethod]
    public async Task ReadAsync_StatusCodeBelow100_ReturnsUnsupportedProtocol()
    {
        await AssertFailsEveryWayAsync("HTTP/1.0 099 X\r\n\r\n", CurlExitCode.UnsupportedProtocol, "Unsupported response code in HTTP response");
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nBadHeader\r\nContent-Length: 0\r\n\r\n", DisplayName = "Final head")]
    [DataRow("HTTP/1.1 100 Continue\r\nBad\r\n\r\nHTTP/1.1 200 OK\r\n\r\n", DisplayName = "1xx head")]
    [DataRow("HTTP/1.1 200 OK\r\n  folded\r\nContent-Length: 0\r\n\r\n", DisplayName = "Continuation with no header before it")]
    public async Task ReadAsync_HeaderWithoutColon_ReturnsWeirdServerReply(string response)
    {
        await AssertFailsEveryWayAsync(response, CurlExitCode.WeirdServerReply, "Header without colon");
    }

    [TestMethod]
    public async Task ReadAsync_FirstHeaderStartsWithBlanks_TakesItWithoutTheBlanks()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            Diagnostics.Arrange("scripted response", $"HTTP/1.1 200 OK, first header '  X-A: 1', then B, chunk size {chunkSize}");
            ScriptedConnection connection = new(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n  X-A: 1\r\nB: 2\r\n\r\n"), chunkSize);

            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            string names = string.Join(",", head.Headers.Select(header => $"{header.Name}={header.Value}"));
            Diagnostics.Act("headers", names);

            Diagnostics.Assert("headers", "X-A=1,B=2", names);
            Assert.AreEqual("X-A=1,B=2", names, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\nnot a status line\r\n", DisplayName = "Body after a 100")]
    [DataRow("HTTP/1.1 100 Continue\r\nA: 1\r\n\r\nxyz", DisplayName = "Body after a 100 with a header")]
    public async Task ReadAsync_NoStatusLineAfterInformationalHead_ReturnsWeirdServerReply(string response)
    {
        await AssertFailsEveryWayAsync(response, CurlExitCode.WeirdServerReply, "Invalid status line");
    }

    [TestMethod]
    public async Task ReadAsync_Http09AnswerToHeadRequest_ReturnsWeirdServerReply()
    {
        Diagnostics.Arrange("scripted response", "body only, HTTP/0.9 accepted, HEAD request");
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes("just a body\n"), 65536);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await new HttpResponseHeadReader(connection) { AcceptsHttp09 = true, IsHeadRequest = true }.ReadAsync(CancellationToken.None));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name}, exit code {thrown.ExitCode}, message '{thrown.Message}'");
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual("Weird server reply", thrown.Message);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\rX-A: 1\r\n\r\n", DisplayName = "In the status line")]
    [DataRow("HTTP/1.1 200 OK\r\nX-A: 1\rfoo\r\n\r\n", DisplayName = "In a header line")]
    public async Task ReadAsync_CarriageReturnInsideALine_ReturnsWeirdServerReply(string response)
    {
        await AssertFailsEveryWayAsync(response, CurlExitCode.WeirdServerReply, "Carriage return found in header");
    }

    [TestMethod]
    [DataRow("", DisplayName = "Nothing at all")]
    [DataRow("HT", DisplayName = "Part of HTTP/")]
    [DataRow("HTTP/1.1 20", DisplayName = "Part of a status line")]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\n", DisplayName = "Only 100 Continue")]
    [DataRow("HTTP/1.1 101 Switching\r\nContent-Length: 0\r\n\r\n", DisplayName = "101 with no upgrade asked for")]
    [DataRow("HTTP/1.1 199 X\r\n\r\n", DisplayName = "199")]
    [DataRow("HTTP/1.1 100 Continue\r\nX-A: 1\r\n", DisplayName = "Inside a 1xx head")]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 20", DisplayName = "Part of the status line after a 1xx")]
    public async Task ReadAsync_PeerClosingBeforeAFinalStatusLine_ReturnsGotNothing(string response)
    {
        await AssertFailsEveryWayAsync(response, CurlExitCode.GotNothing, "Empty reply from server");
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Type: te", DisplayName = "Inside the headers")]
    [DataRow("", DisplayName = "Before the status line")]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvErrorWithTheWinsockWords(string response)
    {
        // Measured inside the headers: curl: (56) Recv failure: Connection was reset.
        await AssertFailsEveryWayAsync(
            response,
            CurlExitCode.RecvError,
            "Recv failure: Connection was reset",
            new IOException("reset", new SocketException((int)SocketError.ConnectionReset)));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Type: te", DisplayName = "Inside the headers")]
    [DataRow("", DisplayName = "Before the status line")]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvErrorWithTheSocketErrorsOwnWords(string response)
    {
        SocketException socketError = new((int)SocketError.ConnectionReset);

        await AssertFailsEveryWayAsync(
            response,
            CurlExitCode.RecvError,
            "Recv failure: " + socketError.Message,
            new IOException("reset", socketError));
    }

    [TestMethod]
    public async Task ReadAsync_OtherReadFailure_ReturnsRecvErrorWithCurlsGenericMessage()
    {
        await AssertFailsEveryWayAsync(
            "HTTP/1.1 200 OK\r\n",
            CurlExitCode.RecvError,
            "Failure when receiving data from the peer",
            new IOException("broken pipe"));
    }

    [TestMethod]
    public async Task ReadAsync_HeaderLineOf102399Bytes_IsAccepted()
    {
        foreach (HttpResponseHead head in await ReadEveryWayAsync(Response(HeaderLine('a', 102399))))
        {
            Diagnostics.Assert("head size", 17 + 102399 + 2, head.HeadBytes.Length);
            Assert.AreEqual(17 + 102399 + 2, head.HeadBytes.Length);
        }
    }

    [TestMethod]
    [DataRow(102400, DisplayName = "102400 bytes")]
    [DataRow(102401, DisplayName = "102401 bytes")]
    public async Task ReadAsync_HeaderLineOf102400BytesOrMore_ReturnsTooLarge(int lineLength)
    {
        await AssertFailsEveryWayAsync(Response(HeaderLine('a', lineLength)), CurlExitCode.TooLarge, "A value or data field grew larger than allowed");
    }

    [TestMethod]
    public async Task ReadAsync_FoldedHeaderOf102399Bytes_IsAccepted()
    {
        // Measured: "X-A: 1" and 9308 continuations of " bbbbbbbbbb" and one of " bb";
        // exit 0 and %{size_header} 102418.
        foreach (HttpResponseHead head in await ReadEveryWayAsync(Response(FoldedHeader("bb"))))
        {
            Diagnostics.Assert("head size", 102418, head.HeadBytes.Length);
            Assert.AreEqual(102418, head.HeadBytes.Length);
        }
    }

    [TestMethod]
    public async Task ReadAsync_FoldedHeaderOf102400Bytes_ReturnsTooLarge()
    {
        // Measured: the same with a last continuation of " bbb" exits 100.
        await AssertFailsEveryWayAsync(Response(FoldedHeader("bbb")), CurlExitCode.TooLarge, "A value or data field grew larger than allowed");
    }

    [TestMethod]
    public async Task ReadAsync_UnterminatedLineOf102400Bytes_ReturnsTooLarge()
    {
        await AssertFailsEveryWayAsync(
            "HTTP/1.1 200 OK\r\n" + new string('a', 102400),
            CurlExitCode.TooLarge,
            "A value or data field grew larger than allowed",
            new IOException("never reached"));
    }

    [TestMethod]
    public async Task ReadAsync_HeadsOf307200Bytes_AreAccepted()
    {
        // Measured: 17 + 3 x 102007 + 1160 + 2 = 307200 bytes; exit 0, %{size_header} 307200.
        foreach (HttpResponseHead head in await ReadEveryWayAsync(Response(ThreeBigHeaders() + HeaderLine('b', 1160))))
        {
            Diagnostics.Assert("head size", 307200, head.HeadBytes.Length);
            Assert.AreEqual(307200, head.HeadBytes.Length);
        }
    }

    [TestMethod]
    [DataRow(1162, 307202, DisplayName = "Passed by the empty line")]
    [DataRow(1163, 307201, DisplayName = "Passed by a header line")]
    public async Task ReadAsync_HeadsOverThe307200ByteLimit_ReturnRecvError(int lastLineLength, int reportedSize)
    {
        // Measured: curl: (56) Too large response headers: 307202 > 307200, and 307201 > 307200.
        await AssertFailsEveryWayAsync(
            Response(ThreeBigHeaders() + HeaderLine('b', lastLineLength)),
            CurlExitCode.RecvError,
            $"Too large response headers: {reportedSize} > 307200");
    }

    [TestMethod]
    public async Task ReadAsync_InformationalHeads_CountTowardsTheLimit()
    {
        // Measured: curl: (56) Too large response headers: 307223 > 307200.
        await AssertFailsEveryWayAsync(
            "HTTP/1.1 100 Continue\r\n\r\n" + Response(ThreeBigHeaders() + HeaderLine('b', 1160)),
            CurlExitCode.RecvError,
            "Too large response headers: 307223 > 307200");
    }

    /// <summary>"X-A: 1" folded with 9308 continuations of " bbbbbbbbbb" and a last one of " " + <paramref name="last" />.</summary>
    private static string FoldedHeader(string last) =>
        "X-A: 1\r\n" + string.Concat(Enumerable.Repeat(" bbbbbbbbbb\r\n", 9308)) + " " + last + "\r\n";

    private static string ThreeBigHeaders() => HeaderLine('a', 102007) + HeaderLine('a', 102007) + HeaderLine('a', 102007);

    /// <summary>A header line of exactly <paramref name="length" /> bytes, its CRLF included.</summary>
    private static string HeaderLine(char filler, int length) => "X-A: " + new string(filler, length - 7) + "\r\n";

    private static string Response(string headerLines) => "HTTP/1.1 200 OK\r\n" + headerLines + "\r\n";

    private static string Describe(string text)
    {
        const int Shown = 200;
        string head = text.Length > Shown ? text[..Shown] : text;
        return $"{text.Length} chars: {head.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)}";
    }

    private async Task<List<HttpResponseHead>> ReadEveryWayAsync(string response)
    {
        Diagnostics.Arrange("scripted response", Describe(response));
        List<HttpResponseHead> heads = [];
        foreach (int chunkSize in ChunkSizes)
        {
            Diagnostics.Arrange("chunk size", chunkSize);
            ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            Diagnostics.Act(
                $"head read with chunk size {chunkSize}",
                $"version {head.StatusLine.Version}, status {head.StatusLine.StatusCode}, reason '{head.StatusLine.ReasonPhrase}', {head.Headers.Count} headers, head bytes {head.HeadBytes.Length}, body prefix {head.BodyPrefix.Length}");
            heads.Add(head);
        }

        return heads;
    }

    private async Task<List<string>> ReadBodiesEveryWayAsync(string response)
    {
        Diagnostics.Arrange("scripted response", Describe(response));
        List<string> bodies = [];
        foreach (int chunkSize in ChunkSizes)
        {
            Diagnostics.Arrange("chunk size", chunkSize);
            ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            List<byte> body = [.. head.BodyPrefix.Span];
            byte[] buffer = new byte[65536];
            int read;
            while ((read = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
            {
                body.AddRange(buffer.AsSpan(0, read));
            }

            string bodyText = Encoding.Latin1.GetString([.. body]);
            Diagnostics.Act($"body left after the head with chunk size {chunkSize}", bodyText);
            bodies.Add(bodyText);
        }

        return bodies;
    }

    private async Task AssertFailsEveryWayAsync(
        string response,
        CurlExitCode exitCode,
        string message,
        Exception? failureAfterResponse = null)
    {
        Diagnostics.Arrange("scripted response", Describe(response));
        Diagnostics.Arrange("failure after response", failureAfterResponse?.GetType().Name ?? "none");
        foreach (int chunkSize in ChunkSizes)
        {
            Diagnostics.Arrange("chunk size", chunkSize);
            ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), chunkSize, failureAfterResponse: failureAfterResponse);
            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None));

            Diagnostics.Act($"thrown with chunk size {chunkSize}", $"{thrown.GetType().Name}, exit code {thrown.ExitCode}, message '{thrown.Message}'");
            Diagnostics.Assert($"exit code with chunk size {chunkSize}", exitCode, thrown.ExitCode);
            Diagnostics.Assert($"message with chunk size {chunkSize}", message, thrown.Message);
            Assert.AreEqual(exitCode, thrown.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, thrown.Message, $"Chunk size {chunkSize}");
        }
    }

    private void AssertStatus(HttpResponseHead head, Version version, int statusCode, string reasonPhrase)
    {
        Diagnostics.Assert("version", version, head.StatusLine.Version);
        Diagnostics.Assert("status code", statusCode, head.StatusLine.StatusCode);
        Diagnostics.Assert("reason phrase", reasonPhrase, head.StatusLine.ReasonPhrase);
        Assert.AreEqual(version, head.StatusLine.Version);
        Assert.AreEqual(statusCode, head.StatusLine.StatusCode);
        Assert.AreEqual(reasonPhrase, head.StatusLine.ReasonPhrase);
    }

    private void AssertHeaders(HttpResponseHead head, params string[] namesAndValues)
    {
        string[] actual = head.Headers.SelectMany(header => new[] { header.Name, header.Value }).ToArray();
        Diagnostics.Assert("header names and values", string.Join("|", namesAndValues), string.Join("|", actual));
        CollectionAssert.AreEqual(
            namesAndValues,
            actual);
    }

    private static string Latin1(ReadOnlyMemory<byte> bytes) => Encoding.Latin1.GetString(bytes.Span);
}
