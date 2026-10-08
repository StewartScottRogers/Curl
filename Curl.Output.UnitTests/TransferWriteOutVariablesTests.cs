using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="TransferWriteOutVariables"/> to what curl 8.21.0 (mingw, Schannel)
/// printed for the same transfers on 2026-09-26; the commands are in BL-225's Notes.
/// </summary>
[TestClass]
public sealed class TransferWriteOutVariablesTests
{
    private const string LoopbackUrl = "http://127.0.0.1:18225/a?b";

    /// <summary>A clock whose timestamps are microseconds, so a test writes curl's microsecond values directly.</summary>
    private static readonly TimeProvider Clock = new MicrosecondTimeProvider();

    private static readonly string[] TimeAndSpeedVariableOrder =
    [
        "time_namelookup", "time_connect", "time_appconnect", "time_pretransfer", "time_posttransfer",
        "time_starttransfer", "time_redirect", "time_total", "speed_download", "speed_upload",
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TryGetVariableText_RedirectResponseNotFollowed_MatchesCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o NUL -w "..." http://127.0.0.1:18225/a?b against a 302 with a relative Location.
        TransferReport report = new()
        {
            ResponseCode = 302,
            HttpVersion = new Version(1, 1),
            Method = "GET",
            ContentType = "text/plain; charset=utf-8",
            RedirectUrl = "http://127.0.0.1:18225/next?q=1",
            HeaderSize = 111,
            RequestSize = 82,
            DownloadSize = 5,
            ConnectionCount = 1,
            ResponseHeaders =
            [
                new("Location", "/next?q=1"),
                new("Content-Type", "text/plain; charset=utf-8"),
                new("X-A", "1"),
                new("Content-Length", "5"),
            ],
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50123),
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 18225),
        };
        diagnostics.Arrange("response code", report.ResponseCode);
        diagnostics.Arrange("url", LoopbackUrl);
        TransferWriteOutVariables variables = new(TransferResult.Success(5) with { Report = report }, LoopbackUrl, 0, LoopbackUrl, "http", Clock);
        const string expected = "302|302|000|1.1|GET|text/plain; charset=utf-8|http://127.0.0.1:18225/next?q=1|http://127.0.0.1:18225/a?b|0|111|82|5|0|1|127.0.0.1|50123|127.0.0.1|18225|0||http://127.0.0.1:18225/a?b|0|http";

        Show(diagnostics, "all variables", expected, RenderAll(variables));

        Assert.AreEqual(
            "302|302|000|1.1|GET|text/plain; charset=utf-8|http://127.0.0.1:18225/next?q=1|http://127.0.0.1:18225/a?b|0|111|82|5|0|1|127.0.0.1|50123|127.0.0.1|18225|0||http://127.0.0.1:18225/a?b|0|http",
            RenderAll(variables));
    }

    [TestMethod]
    public void TryGetVariableText_FileTransferWithoutReport_MatchesCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o NUL -w "..." file:///C:/Windows/win.ini: nothing learned beyond the bytes.
        diagnostics.Arrange("url", "file:///C:/Windows/win.ini");
        TransferWriteOutVariables variables = new(
            TransferResult.Success(92), "file:///C:/Windows/win.ini", 0, "file://C:/Windows/win.ini", "file", Clock);
        const string expected = "000|000|000|0|GET|||file://C:/Windows/win.ini|0|0|0|92|0|0||-1||-1|0||file:///C:/Windows/win.ini|0|file";

        Show(diagnostics, "all variables", expected, RenderAll(variables));
        diagnostics.Act("transfer failed", variables.TransferFailed);
        diagnostics.Assert("transfer failed", false, variables.TransferFailed);

        Assert.AreEqual(
            "000|000|000|0|GET|||file://C:/Windows/win.ini|0|0|0|92|0|0||-1||-1|0||file:///C:/Windows/win.ini|0|file",
            RenderAll(variables));
        Assert.IsFalse(variables.TransferFailed);
    }

    [TestMethod]
    public void TryGetVariableText_ConnectionRefused_MatchesCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o NUL -w "..." http://127.0.0.1:1/x exited 7.
        const string message = "Failed to connect to 127.0.0.1:1 after 2043 ms: Could not connect to server";
        diagnostics.Arrange("exit code", CurlExitCode.CouldntConnect);
        diagnostics.Arrange("message", message);
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.CouldntConnect, message), "http://127.0.0.1:1/x", 0, "http://127.0.0.1:1/x", "http", Clock);
        const string expected = "000|000|000|0|GET|||http://127.0.0.1:1/x|0|0|0|0|0|0||-1||-1|7|" + message + "|http://127.0.0.1:1/x|0|http";

        Show(diagnostics, "all variables", expected, RenderAll(variables));
        Variable(diagnostics, variables, "num_headers", "0");
        diagnostics.Act("transfer failed", variables.TransferFailed);
        diagnostics.Assert("transfer failed", true, variables.TransferFailed);

        Assert.AreEqual(
            "000|000|000|0|GET|||http://127.0.0.1:1/x|0|0|0|0|0|0||-1||-1|7|" + message + "|http://127.0.0.1:1/x|0|http",
            RenderAll(variables));
        Assert.AreEqual("0", Get(variables, "num_headers"));
        Assert.IsTrue(variables.TransferFailed);
    }

    [TestMethod]
    public void TryGetVariableText_UnsupportedScheme_PrintsAnEmptyScheme()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "%{scheme}" nope://x/ printed nothing and exited 1.
        diagnostics.Arrange("url", "nope://x/");
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Protocol \"nope\" not supported"), "nope://x/", 0, "nope://x/", null, Clock);

        Variable(diagnostics, variables, "scheme", string.Empty);
        Variable(diagnostics, variables, "exitcode", "1");
        Variable(diagnostics, variables, "errormsg", "Protocol \"nope\" not supported");

        Assert.AreEqual(string.Empty, Get(variables, "scheme"));
        Assert.AreEqual("1", Get(variables, "exitcode"));
        Assert.AreEqual("Protocol \"nope\" not supported", Get(variables, "errormsg"));
    }

    [TestMethod]
    public void TryGetVariableText_SecondUrl_PrintsItsNumberAndTheUrlAsGiven()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl ... file:///C:/Windows/win.ini FILE:///C:/Windows/nosuch.ini: the second line had urlnum 1 and url as typed.
        diagnostics.Arrange("url", "FILE:///C:/Windows/nosuch.ini");
        diagnostics.Arrange("url number", 1);
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.FileCouldntReadFile, "Could not open file C:/Windows/nosuch.ini"),
            "FILE:///C:/Windows/nosuch.ini",
            1,
            "file://C:/Windows/nosuch.ini",
            "file", Clock);

        Variable(diagnostics, variables, "urlnum", "1");
        Variable(diagnostics, variables, "url", "FILE:///C:/Windows/nosuch.ini");
        Variable(diagnostics, variables, "url_effective", "file://C:/Windows/nosuch.ini");
        Variable(diagnostics, variables, "exitcode", "37");

        Assert.AreEqual("1", Get(variables, "urlnum"));
        Assert.AreEqual("FILE:///C:/Windows/nosuch.ini", Get(variables, "url"));
        Assert.AreEqual("file://C:/Windows/nosuch.ini", Get(variables, "url_effective"));
        Assert.AreEqual("37", Get(variables, "exitcode"));
    }

    [TestMethod]
    public void TryGetVariableText_FailedPostUnderFail_KeepsTheReportsCodesAndSizes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -f -o NUL -d abcdef -w "..." against a 404: exit 22, code 404, method POST, size_upload 6.
        TransferReport report = new() { ResponseCode = 404, HttpVersion = new Version(1, 1), Method = "POST", UploadSize = 6, RequestSize = 155, HeaderSize = 45 };
        diagnostics.Arrange("response code", report.ResponseCode);
        diagnostics.Arrange("method", report.Method);
        TransferResult result = TransferResult.Failure(CurlExitCode.HttpReturnedError, "The requested URL returned error: 404") with { Report = report };
        TransferWriteOutVariables variables = new(result, "http://127.0.0.1:18225/p", 0, "http://127.0.0.1:18225/p", "http", Clock);

        Variable(diagnostics, variables, "http_code", "404");
        Variable(diagnostics, variables, "method", "POST");
        Variable(diagnostics, variables, "size_upload", "6");
        Variable(diagnostics, variables, "size_download", "0");
        Variable(diagnostics, variables, "size_request", "155");
        Variable(diagnostics, variables, "exitcode", "22");
        Variable(diagnostics, variables, "errormsg", "The requested URL returned error: 404");

        Assert.AreEqual("404", Get(variables, "http_code"));
        Assert.AreEqual("POST", Get(variables, "method"));
        Assert.AreEqual("6", Get(variables, "size_upload"));
        Assert.AreEqual("0", Get(variables, "size_download"));
        Assert.AreEqual("155", Get(variables, "size_request"));
        Assert.AreEqual("22", Get(variables, "exitcode"));
        Assert.AreEqual("The requested URL returned error: 404", Get(variables, "errormsg"));
    }

    [TestMethod]
    public void TryGetVariableText_RefusedTunnel_PrintsTheConnectCode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -p -x http://127.0.0.1:18225 -w "%{http_code}|%{http_connect}" against a 407 CONNECT reply.
        TransferReport report = new() { ProxyConnectResponseCode = 407 };
        diagnostics.Arrange("proxy connect response code", report.ProxyConnectResponseCode);
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 407") with { Report = report },
            "http://example.invalid/",
            0,
            "http://example.invalid/",
            "http", Clock);

        Variable(diagnostics, variables, "http_code", "000");
        Variable(diagnostics, variables, "http_connect", "407");

        Assert.AreEqual("000", Get(variables, "http_code"));
        Assert.AreEqual("407", Get(variables, "http_connect"));
    }

    [TestMethod]
    [DataRow(1, 0, "1")]
    [DataRow(1, 1, "1.1")]
    [DataRow(2, 0, "2")]
    [DataRow(3, 0, "3")]
    [DataRow(0, 9, "0")]
    public void TryGetVariableText_HttpVersion_PrintsAsCurl(int major, int minor, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // An HTTP/1.0 status line printed "1" and HTTP/1.1 printed "1.1".
        diagnostics.Arrange("major", major);
        diagnostics.Arrange("minor", minor);
        TransferWriteOutVariables variables = WithReport(new TransferReport { HttpVersion = new Version(major, minor) });

        Variable(diagnostics, variables, "http_version", expected);

        Assert.AreEqual(expected, Get(variables, "http_version"));
    }

    [TestMethod]
    public void TryGetVariableText_ExplicitMethod_PrintsItAsSent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -X PATCH -w "%{method}" printed PATCH.
        diagnostics.Arrange("method", "PATCH");
        TransferWriteOutVariables variables = WithReport(new TransferReport { Method = "PATCH" });

        Variable(diagnostics, variables, "method", "PATCH");

        Assert.AreEqual("PATCH", Get(variables, "method"));
    }

    [TestMethod]
    public void TryGetVariableText_IPv6Endpoints_PrintWithoutBrackets()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "%{local_ip}|%{remote_ip}|%{remote_port}" http://[::1]:18226/ printed "::1|::1|18226".
        TransferReport report = new()
        {
            LocalEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 50200),
            RemoteEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 18226),
        };
        diagnostics.Arrange("local port", 50200);
        diagnostics.Arrange("remote port", 18226);
        TransferWriteOutVariables variables = WithReport(report);

        Variable(diagnostics, variables, "local_ip", "::1");
        Variable(diagnostics, variables, "remote_ip", "::1");
        Variable(diagnostics, variables, "remote_port", "18226");

        Assert.AreEqual("::1", Get(variables, "local_ip"));
        Assert.AreEqual("::1", Get(variables, "remote_ip"));
        Assert.AreEqual("18226", Get(variables, "remote_port"));
    }

    [TestMethod]
    public void TryGetVariableText_RemoteEndPointWithoutLocal_PrintsLocalPortZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -w "%{local_ip} %{local_port} %{remote_ip} %{remote_port}" tftp://127.0.0.1:47519/f
        // printed " 0 127.0.0.1 47519": a connection whose local end is not known (BL-515 Notes).
        diagnostics.Arrange("remote end point", "127.0.0.1:47519");
        TransferWriteOutVariables variables = WithReport(new TransferReport { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 47519) });

        Variable(diagnostics, variables, "local_ip", string.Empty);
        Variable(diagnostics, variables, "local_port", "0");
        Variable(diagnostics, variables, "remote_ip", "127.0.0.1");
        Variable(diagnostics, variables, "remote_port", "47519");

        Assert.AreEqual(string.Empty, Get(variables, "local_ip"));
        Assert.AreEqual("0", Get(variables, "local_port"));
        Assert.AreEqual("127.0.0.1", Get(variables, "remote_ip"));
        Assert.AreEqual("47519", Get(variables, "remote_port"));
    }

    [TestMethod]
    public void TryGetVariableText_UnixSocketConnection_PrintsItsRemoteIpAndNoPorts()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl --unix-socket "C:\Users\Stewart Rogers\AppData\Local\Temp\bl793.sock"
        // -w "[%{remote_ip}|%{remote_port}|%{local_ip}|%{local_port}]" http://x/ printed
        // "[C:\Users\Stewart Rogers\AppData\Local\Temp\bl|-1||-1]" (BL-793 Notes).
        diagnostics.Arrange("unix socket remote ip", @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl");
        TransferWriteOutVariables variables = WithReport(new TransferReport { UnixSocketRemoteIp = @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl" });

        Variable(diagnostics, variables, "remote_ip", @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl");
        Variable(diagnostics, variables, "remote_port", "-1");
        Variable(diagnostics, variables, "local_ip", string.Empty);
        Variable(diagnostics, variables, "local_port", "-1");

        Assert.AreEqual(@"C:\Users\Stewart Rogers\AppData\Local\Temp\bl", Get(variables, "remote_ip"));
        Assert.AreEqual("-1", Get(variables, "remote_port"));
        Assert.AreEqual(string.Empty, Get(variables, "local_ip"));
        Assert.AreEqual("-1", Get(variables, "local_port"));
    }

    [TestMethod]
    public void TryGetVariableText_NoEndPoints_PrintsLocalPortMinusOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -w "..." ftp://127.0.0.1:47518/f against a closed port printed " -1  -1" (BL-515 Notes).
        diagnostics.Arrange("end points", "none");
        TransferWriteOutVariables variables = WithReport(new TransferReport());

        Variable(diagnostics, variables, "local_port", "-1");
        Variable(diagnostics, variables, "remote_port", "-1");

        Assert.AreEqual("-1", Get(variables, "local_port"));
        Assert.AreEqual("-1", Get(variables, "remote_port"));
    }

    [TestMethod]
    public void TryGetVariableText_FollowedRedirect_PrintsTheFollowersUrlAndCount()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferReport report = new() { EffectiveUrl = "http://127.0.0.1:18225/next", RedirectCount = 2 };
        diagnostics.Arrange("effective url", report.EffectiveUrl);
        diagnostics.Arrange("redirect count", report.RedirectCount);
        TransferWriteOutVariables variables = WithReport(report);

        Variable(diagnostics, variables, "url_effective", "http://127.0.0.1:18225/next");
        Variable(diagnostics, variables, "num_redirects", "2");

        Assert.AreEqual("http://127.0.0.1:18225/next", Get(variables, "url_effective"));
        Assert.AreEqual("2", Get(variables, "num_redirects"));
    }

    [TestMethod]
    public void TryGetVariableText_ReportSizes_WinOverBytesTransferred()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("bytes transferred", 999);
        diagnostics.Arrange("report download size", 7);
        TransferWriteOutVariables variables = new(
            TransferResult.Success(999) with { Report = new TransferReport { DownloadSize = 7 } }, LoopbackUrl, 0, LoopbackUrl, "http", Clock);

        Variable(diagnostics, variables, "size_download", "7");

        Assert.AreEqual("7", Get(variables, "size_download"));
    }

    [TestMethod]
    [DataRow("nosuch")]
    [DataRow("url.bogus")]
    [DataRow("urle.bogus")]
    [DataRow("url.")]
    [DataRow("filename")]
    [DataRow("connid")]
    [DataRow("num_cert")]
    [DataRow("HTTP_CODE")]
    [DataRow(" http_code")]
    [DataRow("")]
    public void TryGetVariableText_UnknownName_IsNotKnown(string name)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("name", name);
        TransferWriteOutVariables variables = WithReport(new TransferReport());

        var known = variables.TryGetVariableText(name, out string? text);

        diagnostics.Act("known", known);
        diagnostics.Act("text", text ?? "(null)");
        diagnostics.Assert("known", false, known);
        diagnostics.Assert("text", null, text);
        Assert.IsFalse(known);
        Assert.IsNull(text);
    }

    [TestMethod]
    public void FindFirstHeaderValue_DuplicatesAndPadding_FirstValueTrimmedCaseInsensitively()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -w "[%header{x-dup}][%header{X-DUP}][%header{x-lf}][%header{ x-dup}]" printed "[one][one][a b][]".
        TransferReport report = new()
        {
            ResponseHeaders = [new("X-Dup", "one"), new("X-Dup", "two"), new("X-Lf", " \ta b  ")],
        };
        diagnostics.Arrange("response header count", report.ResponseHeaders.Count);
        TransferWriteOutVariables variables = WithReport(report);

        Header(diagnostics, variables, "x-dup", "one");
        Header(diagnostics, variables, "X-DUP", "one");
        Header(diagnostics, variables, "x-lf", "a b");
        Header(diagnostics, variables, " x-dup", null);

        Assert.AreEqual("one", variables.FindFirstHeaderValue("x-dup"));
        Assert.AreEqual("one", variables.FindFirstHeaderValue("X-DUP"));
        Assert.AreEqual("a b", variables.FindFirstHeaderValue("x-lf"));
        Assert.IsNull(variables.FindFirstHeaderValue(" x-dup"));
    }

    [TestMethod]
    public void TryGetVariableText_PseudoHeaders_CountTowardsNumHeadersButAreNeverFound()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o NUL -w "[%header{Content-Length}][%{num_headers}]" file:///C:/bl285tmp/a.txt
        // printed [][3] (BL-285); a response header alongside them is counted too.
        TransferReport report = new()
        {
            ResponseHeaders = [new("X-A", "1")],
            PseudoHeaders = [new("Content-Length", "12"), new("Accept-ranges", "bytes"), new("Last-Modified", "Wed, 24 Jun 2026 12:34:56 GMT")],
        };
        diagnostics.Arrange("response headers", 1);
        diagnostics.Arrange("pseudo headers", 3);
        TransferWriteOutVariables variables = WithReport(report);

        Variable(diagnostics, variables, "num_headers", "4");
        Header(diagnostics, variables, "Content-Length", null);
        Header(diagnostics, variables, "X-A", "1");

        Assert.AreEqual("4", Get(variables, "num_headers"));
        Assert.IsNull(variables.FindFirstHeaderValue("Content-Length"));
        Assert.AreEqual("1", variables.FindFirstHeaderValue("X-A"));
    }

    [TestMethod]
    public void FindFirstHeaderValue_NoReport_FindsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("report", "none");
        TransferWriteOutVariables variables = new(TransferResult.Success(0), LoopbackUrl, 0, LoopbackUrl, "http", Clock);

        Header(diagnostics, variables, "content-length", null);

        Assert.IsNull(variables.FindFirstHeaderValue("content-length"));
    }

    [TestMethod]
    public void TryGetVariableText_TimedPost_MatchesCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o NUL -d <5000 bytes> -w "..." http://127.0.0.1:18226/ against a 200 with a
        // 20000-byte body printed ns=0.000065 c=0.005721 a=0.000000 pre=0.006038 post=0.006038
        // st=0.057008 r=0.000000 t=0.057123 sd=350170 su=87542. curl divides by the time of its
        // last progress update, 0.057115 here, which is not reported; time_total is used instead.
        TransferReport report = new()
        {
            DownloadSize = 20000,
            UploadSize = 5000,
            Timings = new TransferTimings(
                Started: 1_000_000,
                Connect: new ConnectTimings(Started: 1_000_010, NameResolved: 1_000_065, Connected: 1_005_721, TlsHandshakeCompleted: null),
                RequestReady: 1_006_038,
                RequestSent: 1_006_038,
                FirstByteReceived: 1_057_008,
                Completed: 1_057_115),
        };
        diagnostics.Arrange("download size", report.DownloadSize);
        diagnostics.Arrange("upload size", report.UploadSize);
        diagnostics.Arrange("completed (microseconds)", 1_057_115);
        const string expected = "0.000065|0.005721|0.000000|0.006038|0.006038|0.057008|0.000000|0.057115|350170|87542";

        Show(diagnostics, "times and speeds", expected, RenderTimesAndSpeeds(WithReport(report)));

        Assert.AreEqual(
            "0.000065|0.005721|0.000000|0.006038|0.006038|0.057008|0.000000|0.057115|350170|87542",
            RenderTimesAndSpeeds(WithReport(report)));
    }

    [TestMethod]
    public void TryGetVariableText_NoTimings_PrintsZeroes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o NUL -w "..." file:///tmp/bl226.bin (100000 bytes) printed zero for both speeds.
        diagnostics.Arrange("bytes transferred", 100000);
        TransferWriteOutVariables variables = new(TransferResult.Success(100000), "file:///tmp/bl226.bin", 0, "file:///tmp/bl226.bin", "file", Clock);
        const string expected = "0.000000|0.000000|0.000000|0.000000|0.000000|0.000000|0.000000|0.000000|0|0";

        Show(diagnostics, "times and speeds", expected, RenderTimesAndSpeeds(variables));

        Assert.AreEqual(
            "0.000000|0.000000|0.000000|0.000000|0.000000|0.000000|0.000000|0.000000|0|0",
            RenderTimesAndSpeeds(variables));
    }

    [TestMethod]
    public void TryGetVariableText_TlsAfterRedirect_PrintsSecondsPastOneAndTheRedirectTime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferReport report = new()
        {
            DownloadSize = 3_000_000,
            Timings = new TransferTimings(
                Started: 0,
                Connect: new ConnectTimings(Started: 1_500_000, NameResolved: null, Connected: 1_600_000, TlsHandshakeCompleted: 1_700_001),
                RequestReady: 1_700_002,
                RequestSent: 1_700_003,
                FirstByteReceived: 1_800_000,
                Completed: 2_000_000)
            {
                RedirectDuration = TimeSpan.FromMicroseconds(1_500_000),
            },
        };
        diagnostics.Arrange("download size", report.DownloadSize);
        diagnostics.Arrange("redirect duration (microseconds)", 1_500_000);
        const string expected = "0.000000|1.600000|1.700001|1.700002|1.700003|1.800000|1.500000|2.000000|1500000|0";

        Show(diagnostics, "times and speeds", expected, RenderTimesAndSpeeds(WithReport(report)));

        Assert.AreEqual(
            "0.000000|1.600000|1.700001|1.700002|1.700003|1.800000|1.500000|2.000000|1500000|0",
            RenderTimesAndSpeeds(WithReport(report)));
    }

    [TestMethod]
    public void TryGetVariableText_EventAtTheStart_PrintsOneMicrosecond()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // Curl_pgrsTime makes every event that happened at least one microsecond after the start.
        TransferReport report = new()
        {
            DownloadSize = 5,
            Timings = new TransferTimings(Started: 42, Connect: null, RequestReady: 42, RequestSent: null, FirstByteReceived: null, Completed: 42),
        };
        diagnostics.Arrange("download size", report.DownloadSize);
        diagnostics.Arrange("started and completed (microseconds)", 42);
        const string expected = "0.000000|0.000000|0.000000|0.000001|0.000000|0.000000|0.000000|0.000001|5000000|0";

        Show(diagnostics, "times and speeds", expected, RenderTimesAndSpeeds(WithReport(report)));

        Assert.AreEqual(
            "0.000000|0.000000|0.000000|0.000001|0.000000|0.000000|0.000000|0.000001|5000000|0",
            RenderTimesAndSpeeds(WithReport(report)));
    }

    [TestMethod]
    [DataRow(2_000_000L, "4611686018427387")]
    [DataRow(999_999L, "9223372036854775807")]
    public void TryGetVariableText_SizeTooLargeToScale_DividesAsTrspeed(long totalMicroseconds, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // trspeed: size / whole seconds from one second on, the largest value below it.
        TransferReport report = new()
        {
            DownloadSize = long.MaxValue / 1000,
            Timings = new TransferTimings(Started: 0, Connect: null, RequestReady: null, RequestSent: null, FirstByteReceived: null, Completed: totalMicroseconds),
        };
        diagnostics.Arrange("download size", report.DownloadSize);
        diagnostics.Arrange("total (microseconds)", totalMicroseconds);

        Variable(diagnostics, WithReport(report), "speed_download", expected);

        Assert.AreEqual(expected, Get(WithReport(report), "speed_download"));
    }

    [TestMethod]
    public void TryGetVariableText_FileTransfer_PrintsTheFixedVariablesAsCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o out.bin -w "%{<name>}" file:///Z:/bl284tmp/wo.txt, one name at a time; see BL-284's Notes.
        diagnostics.Arrange("url", "file:///Z:/bl284tmp/wo.txt");
        TransferWriteOutVariables variables = new(
            TransferResult.Success(3), "file:///Z:/bl284tmp/wo.txt", 0, "file:///Z:/bl284tmp/wo.txt", "file", Clock);

        Show(diagnostics, "fixed variables", "0|0|0|0|", RenderFixed(variables));

        Assert.AreEqual("0|0|0|0|", RenderFixed(variables));
    }

    [TestMethod]
    public void TryGetVariableText_HttpTransfer_PrintsTheFixedVariablesAsCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o out.bin -w "%{<name>}" "http://u:p@127.0.0.1:18284/wo.txt?q=1#frag", one name at a time.
        diagnostics.Arrange("response code", 200);
        TransferWriteOutVariables variables = WithReport(new TransferReport { ResponseCode = 200, ConnectionCount = 1 });

        Show(diagnostics, "fixed variables", "0|0|0|0|", RenderFixed(variables));

        Assert.AreEqual("0|0|0|0|", RenderFixed(variables));
    }

    [TestMethod]
    [DataRow("/", "/", "\"/\"", DisplayName = "257 \"/\"")]
    [DataRow("/home/u", "/home/u", "\"/home/u\"", DisplayName = "257 \"/home/u\"")]
    [DataRow("/a \"b\"", "/a \"b\"", "\"/a \\\"b\\\"\"", DisplayName = "257 \"/a \"\"b\"\"\"")]
    [DataRow(null, "", "null", DisplayName = "257 with no quoted directory")]
    public void TryGetVariableText_FtpTransfer_PrintsTheEntryPathAsCurl(string? entryPath, string expectedText, string expectedJson)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // Record-CurlExchange.ps1 -Ftp -FtpReply 'PWD=...' -CurlArgs -sS,-o,NUL,-w,%{ftp_entry_path} (and %{json}); see BL-514's Notes.
        diagnostics.Arrange("entry path", entryPath ?? "(null)");
        diagnostics.Arrange("expected json", expectedJson);
        TransferWriteOutVariables variables = WithReport(new TransferReport { ResponseCode = 226, FtpEntryPath = entryPath });

        Variable(diagnostics, variables, "ftp_entry_path", expectedText);
        var json = Get(variables, "json");
        diagnostics.Act("json", json);
        diagnostics.Assert("json contains entry path", true, json.Contains("\"ftp_entry_path\":" + expectedJson + ",", StringComparison.Ordinal));

        Assert.AreEqual(expectedText, Get(variables, "ftp_entry_path"));
        Assert.Contains("\"ftp_entry_path\":" + expectedJson + ",", Get(variables, "json"));
    }

    [TestMethod]
    public void TryGetVariableText_FailedTransfer_PrintsTheFixedVariablesAsCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o out.bin -w "..." https://self-signed.badssl.com/ exited 60 with ssl_verify_result 0 under Schannel.
        diagnostics.Arrange("exit code", CurlExitCode.PeerFailedVerification);
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.PeerFailedVerification, "SSL certificate problem"),
            "https://self-signed.badssl.com/", 0, "https://self-signed.badssl.com/", "https", Clock);

        Show(diagnostics, "fixed variables", "0|0|0|0|", RenderFixed(variables));

        Assert.AreEqual("0|0|0|0|", RenderFixed(variables));
    }

    [TestMethod]
    public void TryGetVariableText_VerifyResultsGiven_PrintsThemAsTheOpenSslBuild()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl 8.18.0 (OpenSSL) -k -x https://localhost:18443 --proxy-insecure against self-signed
        // certificates printed ssl_verify_result 18 and proxy_ssl_verify_result 18 (BL-661 Notes).
        diagnostics.Arrange("ssl verify result", 18);
        diagnostics.Arrange("proxy ssl verify result", 20);
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.PeerFailedVerification, "SSL certificate problem"),
            "https://localhost/", 0, "https://localhost/", "https", Clock)
        {
            SslVerifyResult = 18,
            ProxySslVerifyResult = 20,
        };

        Variable(diagnostics, variables, "ssl_verify_result", "18");
        Variable(diagnostics, variables, "proxy_ssl_verify_result", "20");
        JsonContains(diagnostics, variables, "\"ssl_verify_result\":18,");
        JsonContains(diagnostics, variables, "\"proxy_ssl_verify_result\":20,");

        Assert.AreEqual("18", Get(variables, "ssl_verify_result"));
        Assert.AreEqual("20", Get(variables, "proxy_ssl_verify_result"));
        Assert.Contains("\"ssl_verify_result\":18,", Get(variables, "json"));
        Assert.Contains("\"proxy_ssl_verify_result\":20,", Get(variables, "json"));
    }

    [TestMethod]
    public void TryGetVariableText_TlsEarlyDataNotGiven_PrintsZeroAsTheSchannelBuild()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl 8.21.0 (Schannel) -sk --tls-earlydata -w "%{tls_earlydata}|" twice over TLS printed "0|0|" (BL-906 Notes).
        diagnostics.Arrange("tls early data sent", "not given");
        TransferWriteOutVariables variables = new(TransferResult.Success(0), "https://127.0.0.1/", 0, "https://127.0.0.1/", "https", Clock);

        Variable(diagnostics, variables, "tls_earlydata", "0");
        JsonContains(diagnostics, variables, "\"tls_earlydata\":0,");

        Assert.AreEqual("0", Get(variables, "tls_earlydata"));
        Assert.Contains("\"tls_earlydata\":0,", Get(variables, "json"));
    }

    [TestMethod]
    [DataRow(36L, "36")]
    [DataRow(-36L, "-36")]
    public void TryGetVariableText_TlsEarlyDataSentGiven_PrintsTheReportedByteCount(long sent, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // CURLINFO_EARLYDATA_SENT_T: the bytes sent as early data, negative when the server rejected them.
        diagnostics.Arrange("tls early data sent", sent);
        TransferWriteOutVariables variables = new(TransferResult.Success(0), "https://localhost/", 0, "https://localhost/", "https", Clock)
        {
            TlsEarlyDataSent = sent,
        };

        Variable(diagnostics, variables, "tls_earlydata", expected);
        JsonContains(diagnostics, variables, $"\"tls_earlydata\":{expected},");

        Assert.AreEqual(expected, Get(variables, "tls_earlydata"));
        Assert.Contains($"\"tls_earlydata\":{expected},", Get(variables, "json"));
    }

    [TestMethod]
    public void TryGetVariableText_TimeQueueWithTimings_PrintsOneMicrosecond()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl printed 0.000083 and 0.000038: the queue is left as the transfer starts, which is the handler's start here.
        TransferReport report = new()
        {
            Timings = new TransferTimings(Started: 500, Connect: null, RequestReady: null, RequestSent: null, FirstByteReceived: null, Completed: 900),
        };
        diagnostics.Arrange("started (microseconds)", 500);

        Variable(diagnostics, WithReport(report), "time_queue", "0.000001");

        Assert.AreEqual("0.000001", Get(WithReport(report), "time_queue"));
    }

    [TestMethod]
    public void TryGetVariableText_TimeQueueWithoutTimings_PrintsZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timings", "none");

        Variable(diagnostics, WithReport(new TransferReport()), "time_queue", "0.000000");

        Assert.AreEqual("0.000000", Get(WithReport(new TransferReport()), "time_queue"));
    }

    [TestMethod]
    public void TryGetVariableText_UrlPartsOfFileUrl_MatchCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // A file URL without a drive letter parses alike on every platform's curl.
        diagnostics.Arrange("url", "file:///tmp/wo.txt");
        TransferWriteOutVariables variables = new(
            TransferResult.Success(3), "file:///tmp/wo.txt", 0, "file:///tmp/wo.txt", "file", Clock);

        Show(diagnostics, "url parts", "file|||||0|/tmp/wo.txt|||", RenderUrlParts(variables, "url."));
        Show(diagnostics, "urle parts", "file|||||0|/tmp/wo.txt|||", RenderUrlParts(variables, "urle."));

        Assert.AreEqual("file|||||0|/tmp/wo.txt|||", RenderUrlParts(variables, "url."));
        Assert.AreEqual("file|||||0|/tmp/wo.txt|||", RenderUrlParts(variables, "urle."));
    }

    [TestMethod]
    public void TryGetVariableText_UrlPartsOfDriveLetterFileUrl_MatchPlatformCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // Windows: curl -s -o out.bin -w "..." file:///Z:/bl284tmp/wo.txt (BL-284's Notes); url_effective
        // is file://Z:/bl284tmp/wo.txt. Linux and macOS: curl's urlapi.c rejects a drive letter in a
        // file URL (CURLUE_BAD_FILE_URL), so every part is empty (BL-322's Notes).
        diagnostics.Arrange("url", "file:///Z:/bl284tmp/wo.txt");
        TransferWriteOutVariables variables = new(
            TransferResult.Success(3), "file:///Z:/bl284tmp/wo.txt", 0, "file://Z:/bl284tmp/wo.txt", "file", Clock);
        string expected = OperatingSystem.IsWindows() ? "file|||||0|Z:/bl284tmp/wo.txt|||" : "|||||||||";
        // The parts differ by platform, so the expected text is not written, only what this platform rendered and whether it matches.
        // The parts differ by platform, so only whether they match the platform's curl is written.
        diagnostics.Act("url parts", RenderUrlParts(variables, "url."));
        diagnostics.Assert("url parts match the platform's curl", true, RenderUrlParts(variables, "url.") == expected);
        diagnostics.Assert("urle parts match the platform's curl", true, RenderUrlParts(variables, "urle.") == expected);

        Assert.AreEqual(expected, RenderUrlParts(variables, "url."));
        Assert.AreEqual(expected, RenderUrlParts(variables, "urle."));
    }

    [TestMethod]
    public void TryGetVariableText_UrlPartsOfHttpUrl_MatchCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o NUL -w "..." "http://u:p@127.0.0.1:18284/wo.txt?q=1#frag" (BL-284's Notes).
        const string given = "http://u:p@127.0.0.1:18284/wo.txt?q=1#frag";
        diagnostics.Arrange("url", given);
        TransferReport report = new() { EffectiveUrl = given };
        TransferWriteOutVariables variables = new(TransferResult.Success(0) with { Report = report }, given, 0, given, "http", Clock);

        Show(diagnostics, "url parts", "http|u|p||127.0.0.1|18284|/wo.txt|q=1|frag|", RenderUrlParts(variables, "url."));
        Show(diagnostics, "urle parts", "http|u|p||127.0.0.1|18284|/wo.txt|q=1|frag|", RenderUrlParts(variables, "urle."));

        Assert.AreEqual("http|u|p||127.0.0.1|18284|/wo.txt|q=1|frag|", RenderUrlParts(variables, "url."));
        Assert.AreEqual("http|u|p||127.0.0.1|18284|/wo.txt|q=1|frag|", RenderUrlParts(variables, "urle."));
    }

    [TestMethod]
    public void TryGetVariableText_UrlPartsOfUrlWithoutScheme_GuessHttp()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o NUL -w "..." 127.0.0.1:18284 (BL-284's Notes).
        diagnostics.Arrange("url", "127.0.0.1:18284");
        TransferWriteOutVariables variables = new(
            TransferResult.Success(0), "127.0.0.1:18284", 0, "http://127.0.0.1:18284/", "http", Clock);

        Show(diagnostics, "url parts", "http||||127.0.0.1|18284|/|||", RenderUrlParts(variables, "url."));
        Show(diagnostics, "urle parts", "http||||127.0.0.1|18284|/|||", RenderUrlParts(variables, "urle."));

        Assert.AreEqual("http||||127.0.0.1|18284|/|||", RenderUrlParts(variables, "url."));
        Assert.AreEqual("http||||127.0.0.1|18284|/|||", RenderUrlParts(variables, "urle."));
    }

    [TestMethod]
    [DataRow("http://h/", "80")]
    [DataRow("imap://h/", "143")]
    [DataRow("nosuch://h/", "")]
    [DataRow("nosuch://h:7/", "7")]
    public void TryGetVariableText_UrlPortWithoutOne_IsSchemeDefault(string url, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", url);
        TransferWriteOutVariables variables = new(TransferResult.Success(0), url, 0, url, null, Clock);

        Variable(diagnostics, variables, "url.port", expected);

        Assert.AreEqual(expected, Get(variables, "url.port"));
    }

    [TestMethod]
    public void TryGetVariableText_UrlPartsOfOptionsAndZoneId_AreRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string given = "imap://u;AUTH=PLAIN@[fe80::1%25eth0]/";
        diagnostics.Arrange("url", given);
        TransferWriteOutVariables variables = new(TransferResult.Success(0), given, 0, given, "imap", Clock);

        Show(diagnostics, "url parts", "imap|u||AUTH=PLAIN|[fe80::1]|143|/|||eth0", RenderUrlParts(variables, "url."));

        Assert.AreEqual("imap|u||AUTH=PLAIN|[fe80::1]|143|/|||eth0", RenderUrlParts(variables, "url."));
    }

    [TestMethod]
    public void TryGetVariableText_UrlThatDoesNotParse_PrintsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "http://[::1");
        TransferWriteOutVariables variables = new(TransferResult.Success(0), "http://[::1", 0, "http://[::1", "http", Clock);

        Show(diagnostics, "url parts", "|||||||||", RenderUrlParts(variables, "url."));

        Assert.AreEqual("|||||||||", RenderUrlParts(variables, "url."));
    }

    [TestMethod]
    [DataRow("file:///Z:/bl284tmp/wo.txt", "file")]
    [DataRow("http://127.0.0.1:18284/wo.txt", "http")]
    public void TryGetVariableText_CertificatesWithoutTls_AreZeroAndNothing(string url, string scheme)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o out.bin -w "[%{num_certs}][%{certs}]" for file:// and http:// (BL-284's Notes).
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("scheme", scheme);
        TransferWriteOutVariables variables = new(TransferResult.Success(0) with { Report = new TransferReport() }, url, 0, url, scheme, Clock);

        Show(diagnostics, "certificates", "[0][]", $"[{Get(variables, "num_certs")}][{Get(variables, "certs")}]");

        Assert.AreEqual("[0][]", $"[{Get(variables, "num_certs")}][{Get(variables, "certs")}]");
    }

    [TestMethod]
    [DataRow("file:///Z:/bl284tmp/wo.txt", "file")]
    [DataRow("http://127.0.0.1:18081/", "http")]
    public void TryGetVariableText_ProxyUsedWithoutProxy_IsZero(string url, string scheme)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -w "%{proxy_used}" for file:// and a direct http:// transfer (BL-284's and BL-302's Notes).
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("scheme", scheme);
        TransferWriteOutVariables variables = new(TransferResult.Success(0) with { Report = new TransferReport() }, url, 0, url, scheme, Clock);

        Variable(diagnostics, variables, "proxy_used", "0");

        Assert.AreEqual("0", Get(variables, "proxy_used"));
    }

    [TestMethod]
    public void TryGetVariableText_ProxyUsedWithoutReport_IsZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("report", "none");
        TransferWriteOutVariables variables = new(TransferResult.Success(0), LoopbackUrl, 0, LoopbackUrl, "http", Clock);

        Variable(diagnostics, variables, "proxy_used", "0");

        Assert.AreEqual("0", Get(variables, "proxy_used"));
    }

    [TestMethod]
    public void TryGetVariableText_ProxyUsedThroughProxy_IsOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -x http://127.0.0.1:18080 -w "%{proxy_used}" http://example.test/, forwarded and with -p (BL-302's Notes).
        diagnostics.Arrange("used proxy", true);
        TransferWriteOutVariables variables = WithReport(new TransferReport { UsedProxy = true });

        Variable(diagnostics, variables, "proxy_used", "1");

        Assert.AreEqual("1", Get(variables, "proxy_used"));
    }

    [TestMethod]
    public void TryGetVariableText_CertificatesWithoutReport_AreZeroAndNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("report", "none");
        TransferWriteOutVariables variables = new(TransferResult.Success(0), LoopbackUrl, 0, LoopbackUrl, "http", Clock);

        Show(diagnostics, "certificates", "[0][]", $"[{Get(variables, "num_certs")}][{Get(variables, "certs")}]");

        Assert.AreEqual("[0][]", $"[{Get(variables, "num_certs")}][{Get(variables, "certs")}]");
    }

    [TestMethod]
    public void TryGetVariableText_CertificatesOfLoopbackHttpsTransfer_MatchCurl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -k -s -o NUL -w "%{num_certs}\n%{certs}" https://127.0.0.1:18304/ (BL-303's Notes).
        diagnostics.Arrange("peer certificate count", LoopbackChain.Certificates.Length);
        TransferWriteOutVariables variables = WithReport(new TransferReport { PeerCertificates = LoopbackChain.Certificates });

        Variable(diagnostics, variables, "num_certs", "3");
        Variable(diagnostics, variables, "certs", LoopbackChain.CertsText);

        Assert.AreEqual("3", Get(variables, "num_certs"));
        Assert.AreEqual(LoopbackChain.CertsText, Get(variables, "certs"));
    }

    [TestMethod]
    public void TryGetVariableText_CommandLineInputsNotGiven_PrintAsCurlPrintsThem()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -w "[%{referer}][%{filename_effective}]" to standard output without -e printed [][];
        // a URL curl rejected with exit 3 printed conn_id -1 (BL-284's Notes).
        diagnostics.Arrange("referer", "not given");
        diagnostics.Arrange("output file name", "not given");
        TransferWriteOutVariables variables = new(TransferResult.Success(0), LoopbackUrl, 0, LoopbackUrl, "http", Clock);

        Show(diagnostics, "command line inputs", "[][][-1][0]", CommandLineInputs(variables));

        Assert.AreEqual(
            "[][][-1][0]",
            $"[{Get(variables, "referer")}][{Get(variables, "filename_effective")}][{Get(variables, "conn_id")}][{Get(variables, "xfer_id")}]");
    }

    [TestMethod]
    public void TryGetVariableText_CommandLineInputsGiven_PrintAsGiven()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -e http://ref.example/x -o out.bin, second of two URLs: referer, out.bin, conn_id 1, xfer_id 1 (BL-284's Notes).
        diagnostics.Arrange("referer", "http://ref.example/x");
        diagnostics.Arrange("output file name", "out.bin");
        diagnostics.Arrange("connection id", 1);
        diagnostics.Arrange("transfer id", 1);
        TransferWriteOutVariables variables = new(TransferResult.Success(0), LoopbackUrl, 1, LoopbackUrl, "http", Clock)
        {
            Referer = "http://ref.example/x",
            OutputFileName = "out.bin",
            ConnectionId = 1,
            TransferId = 1,
        };

        Show(diagnostics, "command line inputs", "[http://ref.example/x][out.bin][1][1]", CommandLineInputs(variables));

        Assert.AreEqual(
            "[http://ref.example/x][out.bin][1][1]",
            $"[{Get(variables, "referer")}][{Get(variables, "filename_effective")}][{Get(variables, "conn_id")}][{Get(variables, "xfer_id")}]");
    }

    [TestMethod]
    public void TryGetVariableText_RetryCountGiven_PrintsItAsNumRetries()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl --retry 2 against 503, 503, 200 printed num_retries 2 (BL-513's Notes).
        diagnostics.Arrange("retry count", 2);
        TransferWriteOutVariables variables = new(TransferResult.Success(0), LoopbackUrl, 0, LoopbackUrl, "http", Clock)
        {
            RetryCount = 2,
        };

        Variable(diagnostics, variables, "num_retries", "2");

        Assert.AreEqual("2", Get(variables, "num_retries"));
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", LoopbackUrl);
        TransferResult result = TransferResult.Success(0);

        var nullResult = Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(null!, LoopbackUrl, 0, LoopbackUrl, "http", Clock));
        var nullUrl = Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(result, null!, 0, LoopbackUrl, "http", Clock));
        var nullEffectiveUrl = Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(result, LoopbackUrl, 0, null!, "http", Clock));
        var nullClock = Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(result, LoopbackUrl, 0, LoopbackUrl, "http", null!));

        Thrown(diagnostics, "null result", nullResult);
        Thrown(diagnostics, "null url", nullUrl);
        Thrown(diagnostics, "null effective url", nullEffectiveUrl);
        Thrown(diagnostics, "null clock", nullClock);
    }

    [TestMethod]
    public void Lookups_NullName_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("name", "null");
        TransferWriteOutVariables variables = WithReport(new TransferReport());

        var variableException = Assert.ThrowsExactly<ArgumentNullException>(() => variables.TryGetVariableText(null!, out _));
        var headerException = Assert.ThrowsExactly<ArgumentNullException>(() => variables.FindFirstHeaderValue(null!));

        Thrown(diagnostics, "variable lookup", variableException);
        Thrown(diagnostics, "header lookup", headerException);
    }

    private static readonly string[] MeasuredVariableOrder =
    [
        "response_code", "http_code", "http_connect", "http_version", "method", "content_type", "redirect_url",
        "url_effective", "num_redirects", "size_header", "size_request", "size_download", "size_upload",
        "num_connects", "local_ip", "local_port", "remote_ip", "remote_port", "exitcode",
        "errormsg", "url", "urlnum", "scheme",
    ];

    private static TransferWriteOutVariables WithReport(TransferReport report)
    {
        return new TransferWriteOutVariables(TransferResult.Success(0) with { Report = report }, LoopbackUrl, 0, LoopbackUrl, "http", Clock);
    }

    private static string RenderAll(TransferWriteOutVariables variables)
    {
        return string.Join('|', MeasuredVariableOrder.Select(name => Get(variables, name)));
    }

    private static readonly string[] FixedVariableOrder =
    [
        "ssl_verify_result", "proxy_ssl_verify_result", "tls_earlydata", "num_retries", "ftp_entry_path",
    ];

    private static readonly string[] UrlPartOrder =
    [
        "scheme", "user", "password", "options", "host", "port", "path", "query", "fragment", "zoneid",
    ];

    private static string RenderUrlParts(TransferWriteOutVariables variables, string prefix)
    {
        return string.Join('|', UrlPartOrder.Select(part => Get(variables, prefix + part)));
    }

    private static string RenderFixed(TransferWriteOutVariables variables)
    {
        return string.Join('|', FixedVariableOrder.Select(name => Get(variables, name)));
    }

    private static string RenderTimesAndSpeeds(TransferWriteOutVariables variables)
    {
        return string.Join('|', TimeAndSpeedVariableOrder.Select(name => Get(variables, name)));
    }

    private static string CommandLineInputs(TransferWriteOutVariables variables)
    {
        return $"[{Get(variables, "referer")}][{Get(variables, "filename_effective")}][{Get(variables, "conn_id")}][{Get(variables, "xfer_id")}]";
    }

    private static string Get(TransferWriteOutVariables variables, string name)
    {
        Assert.IsTrue(variables.TryGetVariableText(name, out string? text), name);
        return text;
    }

    /// <summary>Writes what a rendered text came out as and where it first differs from the expected text.</summary>
    private static void Show(TestDiagnostics diagnostics, string label, string expected, string actual)
    {
        diagnostics.Act(label, actual);
        diagnostics.Diff(label, expected, actual);
    }

    /// <summary>Writes one variable's printed text and where it first differs from the expected text.</summary>
    private static void Variable(TestDiagnostics diagnostics, TransferWriteOutVariables variables, string name, string expected)
    {
        Show(diagnostics, name, expected, Get(variables, name));
    }

    /// <summary>Writes whether the %{json} text holds the expected fragment.</summary>
    private static void JsonContains(TestDiagnostics diagnostics, TransferWriteOutVariables variables, string fragment)
    {
        var json = Get(variables, "json");
        diagnostics.Act("json", json);
        diagnostics.Assert("json contains " + fragment, true, json.Contains(fragment, StringComparison.Ordinal));
    }

    /// <summary>Writes the first value of a response header and the expected one.</summary>
    private static void Header(TestDiagnostics diagnostics, TransferWriteOutVariables variables, string name, string? expected)
    {
        var actual = variables.FindFirstHeaderValue(name);
        diagnostics.Act("header '" + name + "'", actual ?? "(null)");
        diagnostics.Assert("header '" + name + "'", expected ?? "(null)", actual ?? "(null)");
    }

    /// <summary>Writes the exception a call threw and the type it was expected to throw.</summary>
    private static void Thrown(TestDiagnostics diagnostics, string label, ArgumentNullException exception)
    {
        diagnostics.Act(label + " exception type", exception.GetType().Name);
        diagnostics.Act(label + " exception message", exception.Message);
        diagnostics.Assert(label + " exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private sealed class MicrosecondTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => 1_000_000;
    }
}
