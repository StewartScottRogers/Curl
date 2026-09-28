using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// <c>-r</c>, <c>-C</c>, <c>-z</c>, <c>--max-filesize</c> and <c>-R</c>'s source time over HTTP.
/// Every request, output and message here was measured on curl 8.21.0 against a loopback server
/// with <c>Record-CurlExchange.ps1</c> (BL-178 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ConditionDate = "Sun, 06 Nov 1994 08:49:37 GMT";

    private const string Partial = "HTTP/1.1 206 Partial Content\r\nContent-Range: bytes 100-104/105\r\nContent-Length: 5\r\n\r\n";

    private const string WholeHead = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n";

    private static readonly DateTimeOffset ConditionTime = new(1994, 11, 6, 8, 49, 37, TimeSpan.Zero);

    [TestMethod]
    [DataRow(18781, "0-99", DisplayName = "-r 0-99")]
    [DataRow(18782, "100-", DisplayName = "-r 100-")]
    [DataRow(18783, "-500", DisplayName = "-r -500")]
    [DataRow(18837, "0-9,20-29", DisplayName = "-r 0-9,20-29")]
    [DataRow(18838, "1-2abc", DisplayName = "-r 1-2abc")]
    [DataRow(18839, "abc", DisplayName = "-r abc")]
    [DataRow(18840, "-0", DisplayName = "-r -0")]
    public async Task ExecuteAsync_Range_SendsItAfterHostAndWritesTheBody(int port, string range)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new() { Url = ConditionUrl(port), Output = output, RangeText = range };

            TransferResult result = await Handler(QueueConnector.For(Connection(Partial + "hello", chunkSize, RangeRequest(port, range))))
                .ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeAnswered200_WritesTheWholeBody()
    {
        MemoryStream output = new();
        TransferContext context = new TransferContext { Url = ConditionUrl(18784), Output = output, RangeText = "0-99" };

        TransferResult result = await Handler(QueueConnector.For(Connection(WholeHead + "hello", 65536, RangeRequest(18784, "0-99"))))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeOverriddenByHeader_SendsTheHeaderOnly()
    {
        const string expected = "GET /f HTTP/1.1\r\nHost: 127.0.0.1:18832\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nRange: bytes=1-2\r\n\r\n";
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18832),
            Output = new MemoryStream(),
            RangeText = "0-9",
            Http = new HttpRequestOptions { Headers = ["Range: bytes=1-2"] },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(Partial + "hello", 65536, expected))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_EveryHeader_PutsRangeAfterAuthorizationAndTheConditionAfterCookie()
    {
        const string expected = "GET /f HTTP/1.1\r\nHost: 127.0.0.1:18831\r\nAuthorization: Basic dTpw\r\nRange: bytes=0-9\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nAccept-Encoding: deflate, gzip, br\r\nReferer: ref\r\nCookie: a=b\r\n"
            + "If-Modified-Since: Sun, 06 Nov 1994 08:49:37 GMT\r\nX-A: 1\r\n\r\n";
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18831),
            Output = new MemoryStream(),
            RangeText = "0-9",
            TimeCondition = new TimeCondition(ConditionTime, TimeConditionKind.IfModifiedSince),
            Http = new HttpRequestOptions { Headers = ["X-A: 1"], Referer = "ref", Compressed = true },
        };

        TransferResult result = await new HttpProtocolHandler(
            QueueConnector.For(Connection(Partial + "hello", 65536, expected)),
            new ScriptedAuthenticator("Basic dTpw", null),
            new ScriptedCookieStore("a=b")).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeWithARequestBody_SendsContentRangeAfterHost()
    {
        const string expected = "POST /f HTTP/1.1\r\nHost: 127.0.0.1:18834\r\nContent-Range: bytes 0-9/1\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "If-Modified-Since: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Length: 1\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx";
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18834),
            Output = new MemoryStream(),
            RangeText = "0-9",
            TimeCondition = new TimeCondition(ConditionTime, TimeConditionKind.IfModifiedSince),
            Http = new HttpRequestOptions { Body = new BytesBody("x"u8.ToArray(), "application/x-www-form-urlencoded") },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(Partial + "hello", 65536, expected))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow(18841, "0-9,20-29", DisplayName = "-d x -r 0-9,20-29")]
    [DataRow(18842, "1-2abc", DisplayName = "-d x -r 1-2abc")]
    [DataRow(18843, "abc", DisplayName = "-d x -r abc")]
    [DataRow(18844, "-0", DisplayName = "-d x -r -0")]
    public async Task ExecuteAsync_RangeTextWithARequestBody_SendsItAsTypedInContentRange(int port, string range)
    {
        string expected = $"POST /f HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nContent-Range: bytes {range}/1\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 1\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx";
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(port),
            Output = new MemoryStream(),
            RangeText = range,
            Http = new HttpRequestOptions { Body = new BytesBody("x"u8.ToArray(), "application/x-www-form-urlencoded") },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(Partial + "hello", 65536, expected))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeWithAPutBody_SendsContentRangeAfterHost()
    {
        const string expected = "PUT /f HTTP/1.1\r\nHost: 127.0.0.1:18835\r\nContent-Range: bytes 100-/5\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 5\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nhello";
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18835),
            Output = new MemoryStream(),
            RangeText = "100-",
            Http = new HttpRequestOptions { CustomMethod = "PUT", Body = new BytesBody("hello"u8.ToArray(), "application/x-www-form-urlencoded") },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(Partial + "hello", 65536, expected))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeWithABodyAndACustomContentRange_SendsTheCustomOneInItsPlace()
    {
        const string expected = "POST /f HTTP/1.1\r\nHost: 127.0.0.1:18836\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Range: foo\r\n"
            + "Content-Length: 1\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx";
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18836),
            Output = new MemoryStream(),
            RangeText = "0-9",
            Http = new HttpRequestOptions { Headers = ["Content-Range: foo"], Body = new BytesBody("x"u8.ToArray(), "application/x-www-form-urlencoded") },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(Partial + "hello", 65536, expected))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeHonoured_WritesTheBody()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new TransferContext { Url = ConditionUrl(18785), Output = output, ResumeFrom = 100 };

            TransferResult result = await Handler(QueueConnector.For(Connection(Partial + "hello", chunkSize, RangeRequest(18785, "100-"))))
                .ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(206, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(5L, result.Report.DownloadSize, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeAnswered200_WritesTheHeadThenFailsWithExit33()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new TransferContext { Url = ConditionUrl(18842), Output = output, ResumeFrom = 100, HeaderOutput = output };

            TransferResult result = await Handler(QueueConnector.For(Connection(WholeHead + "hello", chunkSize, RangeRequest(18842, "100-"))))
                .ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.RangeError, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP server does not seem to support byte ranges. Cannot resume.", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(WholeHead, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, result.BytesTransferred, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeAnswered416_WritesTheHeadAndNoBody()
    {
        const string head = "HTTP/1.1 416 Range Not Satisfiable\r\nContent-Range: bytes */100\r\nContent-Length: 4\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new TransferContext { Url = ConditionUrl(18841), Output = output, ResumeFrom = 100, HeaderOutput = output };

            TransferResult result = await Handler(QueueConnector.For(Connection(head + "nope", chunkSize, RangeRequest(18841, "100-"))))
                .ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(416, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.IsFalse(result.TimeConditionUnmet, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeAtTheEnd_WritesNoBodyAndSucceeds()
    {
        MemoryStream output = new();
        TransferContext context = new TransferContext { Url = ConditionUrl(18862), Output = output, ResumeFrom = 5 };

        TransferResult result = await Handler(QueueConnector.For(Connection(WholeHead + "hello", 65536, RangeRequest(18862, "5-"))))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeWithNoBody_SendsTheRangeAndWritesTheHead()
    {
        const string expected = "HEAD /f HTTP/1.1\r\nHost: 127.0.0.1:18863\r\nRange: bytes=100-\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";
        MemoryStream output = new();
        TransferContext context = new TransferContext { Url = ConditionUrl(18863), Output = output, ResumeFrom = 100, NoBody = true, HeaderOutput = output };

        TransferResult result = await Handler(QueueConnector.For(Connection(WholeHead, 65536, expected))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(WholeHead, Latin1(output.ToArray()));
    }

    [TestMethod]
    [DataRow(TimeConditionKind.IfModifiedSince, "If-Modified-Since", DisplayName = "-z date")]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, "If-Unmodified-Since", DisplayName = "-z -date")]
    public async Task ExecuteAsync_TimeCondition_SendsItInRfc1123Form(TimeConditionKind kind, string name)
    {
        string expected = $"GET /f HTTP/1.1\r\nHost: 127.0.0.1:18792\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n{name}: {ConditionDate}\r\n\r\n";
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18792),
            Output = new MemoryStream(),
            TimeCondition = new TimeCondition(new DateTimeOffset(1994, 11, 6, 9, 49, 37, TimeSpan.FromHours(1)), kind),
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(WholeHead + "hello", 65536, expected))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimeConditionOfTheLastTimeWindowsFormats_SendsIt()
    {
        const string expected = "GET /f HTTP/1.1\r\nHost: 127.0.0.1:18798\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "If-Modified-Since: Thu, 01 Jan 3001 20:59:59 GMT\r\n\r\n";
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18798),
            Output = new MemoryStream(),
            TimeCondition = new TimeCondition(HttpTimeConditionLimit.LastWindowsTime, TimeConditionKind.IfModifiedSince),
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(WholeHead + "hello", 65536, expected))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow(TimeConditionKind.IfModifiedSince, DisplayName = "-z \"1 Jan 099999999\"")]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, DisplayName = "-z \"-1 Jan 099999999\"")]
    public async Task ExecuteAsync_TimeConditionAfterTheYear9999_FailsWith43OnWindowsAndSendsTheClampedDateElsewhere(TimeConditionKind kind)
    {
        string name = kind == TimeConditionKind.IfModifiedSince ? "If-Modified-Since" : "If-Unmodified-Since";
        string expected = $"GET /f HTTP/1.1\r\nHost: 127.0.0.1:18799\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n{name}: Fri, 31 Dec 9999 23:59:59 GMT\r\n\r\n";
        ScriptedConnection connection = Connection(WholeHead + "hello", 65536, OperatingSystem.IsWindows() ? null : expected);
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18799),
            Output = new MemoryStream(),
            TimeCondition = new TimeCondition(new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero), kind),
        };

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
            Assert.AreEqual("Invalid TIMEVALUE", result.ErrorMessage);
            Assert.IsEmpty(connection.Written);
            Assert.IsTrue(connection.IsMarkedReusable);
        }
        else
        {
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_TimeConditionOverriddenByHeader_SendsTheHeaderOnly()
    {
        const string expected = "GET /f HTTP/1.1\r\nHost: 127.0.0.1:18833\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nIf-Modified-Since: x\r\n\r\n";
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18833),
            Output = new MemoryStream(),
            TimeCondition = new TimeCondition(ConditionTime, TimeConditionKind.IfModifiedSince),
            Http = new HttpRequestOptions { Headers = ["If-Modified-Since: x"] },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(WholeHead + "hello", 65536, expected))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimeConditionAnswered304_WritesTheHeadAndNoBody()
    {
        const string head = "HTTP/1.1 304 Not Modified\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new TransferContext
            {
                Url = ConditionUrl(18797),
                Output = output,
                HeaderOutput = output,
                TimeCondition = new TimeCondition(ConditionTime, TimeConditionKind.IfModifiedSince),
            };

            TransferResult result = await Handler(QueueConnector.For(Connection(head, chunkSize))).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.IsTrue(result.TimeConditionUnmet, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(304, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_LastModifiedFailsTheCondition_WritesTheHeadAndReportsA304()
    {
        const string head = "HTTP/1.1 200 OK\r\nLast-Modified: Sat, 05 Nov 1994 08:49:37 GMT\r\nContent-Length: 5\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new TransferContext
            {
                Url = ConditionUrl(18836),
                Output = output,
                HeaderOutput = output,
                TimeCondition = new TimeCondition(ConditionTime, TimeConditionKind.IfModifiedSince),
            };

            TransferResult result = await Handler(QueueConnector.For(Connection(head + "hello", chunkSize))).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.IsTrue(result.TimeConditionUnmet, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(304, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, result.Report.DownloadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(new DateTimeOffset(1994, 11, 5, 8, 49, 37, TimeSpan.Zero), result.SourceLastWriteTimeUtc, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_LastModifiedMeetsTheCondition_WritesTheBody()
    {
        const string head = "HTTP/1.1 200 OK\r\nLast-Modified: Mon, 07 Nov 1994 08:49:37 GMT\r\nContent-Length: 5\r\n\r\n";
        MemoryStream output = new();
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18795),
            Output = output,
            TimeCondition = new TimeCondition(ConditionTime, TimeConditionKind.IfModifiedSince),
        };

        TransferResult result = await Handler(QueueConnector.For(Connection(head + "hello", 65536))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(result.TimeConditionUnmet);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_LastModified_SetsTheSourceTime()
    {
        const string head = "HTTP/1.1 200 OK\r\nLast-Modified: Sat, 05 Nov 1994 08:49:37 GMT\r\nContent-Length: 5\r\n\r\n";
        MemoryStream output = new();

        TransferResult result = await Handler(QueueConnector.For(Connection(head + "hello", 65536))).ExecuteAsync(ConditionContext(18846, output));

        Assert.AreEqual(new DateTimeOffset(1994, 11, 5, 8, 49, 37, TimeSpan.Zero), result.SourceLastWriteTimeUtc);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoLastModified_LeavesTheSourceTimeUnknown()
    {
        TransferResult result = await Handler(QueueConnector.For(Connection(WholeHead + "hello", 65536)))
            .ExecuteAsync(ConditionContext(18846, new MemoryStream()));

        Assert.IsNull(result.SourceLastWriteTimeUtc);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "GET")]
    [DataRow(true, DisplayName = "-I")]
    public async Task ExecuteAsync_ContentLengthOverMaxFileSize_WritesTheHeadThenFailsWithExit63(bool noBody)
    {
        const string head = "HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new TransferContext { Url = ConditionUrl(18802), Output = output, MaxFileSize = 10, HeaderOutput = output, NoBody = noBody };

            TransferResult result = await Handler(QueueConnector.For(Connection(head + new string('x', 100), chunkSize))).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Maximum file size exceeded", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(200, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, result.Report.DownloadSize, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthAtMaxFileSize_WritesTheBody()
    {
        MemoryStream output = new();
        TransferContext context = new TransferContext { Url = ConditionUrl(18799), Output = output, MaxFileSize = 5 };

        TransferResult result = await Handler(QueueConnector.For(Connection(WholeHead + "hello", 65536))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_FailAndContentLengthOverMaxFileSize_FailsWithExit22First()
    {
        TransferContext context = new TransferContext
        {
            Url = ConditionUrl(18870),
            Output = new MemoryStream(),
            MaxFileSize = 10,
            Http = new HttpRequestOptions { Fail = HttpFailMode.Fail },
        };

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 404 Not Found\r\nContent-Length: 100\r\n\r\n", 65536)))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nf\r\n123456789012345\r\n0\r\n\r\n", "1234567890", DisplayName = "one chunk")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n8\r\n12345678\r\n8\r\nabcdefgh\r\n0\r\n\r\n", "12345678ab", DisplayName = "two chunks")]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\n123456789012345", "1234567890", DisplayName = "read to close")]
    public async Task ExecuteAsync_BodyGrowsPastMaxFileSize_WritesTheLimitThenFailsWithExit63(string response, string written)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferContext context = new TransferContext { Url = ConditionUrl(18800), Output = output, MaxFileSize = 10 };

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize))).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Exceeded the maximum allowed file size (10) with 10 bytes", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(written, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(10L, result.BytesTransferred, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyAtMaxFileSize_Succeeds()
    {
        MemoryStream output = new();
        TransferContext context = new TransferContext { Url = ConditionUrl(18801), Output = output, MaxFileSize = 10 };

        TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\n1234567890", 1)))
            .ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("1234567890", Latin1(output.ToArray()));
    }

    private static string RangeRequest(int port, string range) =>
        $"GET /f HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nRange: bytes={range}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private static TransferContext ConditionContext(int port, Stream output) =>
        new() { Url = ConditionUrl(port), Output = output };

    private static CurlUrl ConditionUrl(int port) => CurlUrl.Parse($"http://127.0.0.1:{port}/f");
}
