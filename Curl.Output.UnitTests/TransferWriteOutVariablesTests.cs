using System.Net;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void TryGetVariableText_RedirectResponseNotFollowed_MatchesCurl()
    {
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
        TransferWriteOutVariables variables = new(TransferResult.Success(5) with { Report = report }, LoopbackUrl, 0, LoopbackUrl, "http", Clock);

        Assert.AreEqual(
            "302|302|000|1.1|GET|text/plain; charset=utf-8|http://127.0.0.1:18225/next?q=1|http://127.0.0.1:18225/a?b|0|111|82|5|0|1|127.0.0.1|50123|127.0.0.1|18225|0||http://127.0.0.1:18225/a?b|0|http",
            RenderAll(variables));
    }

    [TestMethod]
    public void TryGetVariableText_FileTransferWithoutReport_MatchesCurl()
    {
        // curl -s -o NUL -w "..." file:///C:/Windows/win.ini: nothing learned beyond the bytes.
        TransferWriteOutVariables variables = new(
            TransferResult.Success(92), "file:///C:/Windows/win.ini", 0, "file://C:/Windows/win.ini", "file", Clock);

        Assert.AreEqual(
            "000|000|000|0|GET|||file://C:/Windows/win.ini|0|0|0|92|0|0||-1||-1|0||file:///C:/Windows/win.ini|0|file",
            RenderAll(variables));
        Assert.IsFalse(variables.TransferFailed);
    }

    [TestMethod]
    public void TryGetVariableText_ConnectionRefused_MatchesCurl()
    {
        // curl -s -o NUL -w "..." http://127.0.0.1:1/x exited 7.
        const string message = "Failed to connect to 127.0.0.1:1 after 2043 ms: Could not connect to server";
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.CouldntConnect, message), "http://127.0.0.1:1/x", 0, "http://127.0.0.1:1/x", "http", Clock);

        Assert.AreEqual(
            "000|000|000|0|GET|||http://127.0.0.1:1/x|0|0|0|0|0|0||-1||-1|7|" + message + "|http://127.0.0.1:1/x|0|http",
            RenderAll(variables));
        Assert.AreEqual("0", Get(variables, "num_headers"));
        Assert.IsTrue(variables.TransferFailed);
    }

    [TestMethod]
    public void TryGetVariableText_UnsupportedScheme_PrintsAnEmptyScheme()
    {
        // curl -w "%{scheme}" nope://x/ printed nothing and exited 1.
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Protocol \"nope\" not supported"), "nope://x/", 0, "nope://x/", null, Clock);

        Assert.AreEqual(string.Empty, Get(variables, "scheme"));
        Assert.AreEqual("1", Get(variables, "exitcode"));
        Assert.AreEqual("Protocol \"nope\" not supported", Get(variables, "errormsg"));
    }

    [TestMethod]
    public void TryGetVariableText_SecondUrl_PrintsItsNumberAndTheUrlAsGiven()
    {
        // curl ... file:///C:/Windows/win.ini FILE:///C:/Windows/nosuch.ini: the second line had urlnum 1 and url as typed.
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.FileCouldntReadFile, "Could not open file C:/Windows/nosuch.ini"),
            "FILE:///C:/Windows/nosuch.ini",
            1,
            "file://C:/Windows/nosuch.ini",
            "file", Clock);

        Assert.AreEqual("1", Get(variables, "urlnum"));
        Assert.AreEqual("FILE:///C:/Windows/nosuch.ini", Get(variables, "url"));
        Assert.AreEqual("file://C:/Windows/nosuch.ini", Get(variables, "url_effective"));
        Assert.AreEqual("37", Get(variables, "exitcode"));
    }

    [TestMethod]
    public void TryGetVariableText_FailedPostUnderFail_KeepsTheReportsCodesAndSizes()
    {
        // curl -s -f -o NUL -d abcdef -w "..." against a 404: exit 22, code 404, method POST, size_upload 6.
        TransferReport report = new() { ResponseCode = 404, HttpVersion = new Version(1, 1), Method = "POST", UploadSize = 6, RequestSize = 155, HeaderSize = 45 };
        TransferResult result = TransferResult.Failure(CurlExitCode.HttpReturnedError, "The requested URL returned error: 404") with { Report = report };
        TransferWriteOutVariables variables = new(result, "http://127.0.0.1:18225/p", 0, "http://127.0.0.1:18225/p", "http", Clock);

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
        // curl -s -p -x http://127.0.0.1:18225 -w "%{http_code}|%{http_connect}" against a 407 CONNECT reply.
        TransferReport report = new() { ProxyConnectResponseCode = 407 };
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 407") with { Report = report },
            "http://example.invalid/",
            0,
            "http://example.invalid/",
            "http", Clock);

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
        // An HTTP/1.0 status line printed "1" and HTTP/1.1 printed "1.1".
        TransferWriteOutVariables variables = WithReport(new TransferReport { HttpVersion = new Version(major, minor) });

        Assert.AreEqual(expected, Get(variables, "http_version"));
    }

    [TestMethod]
    public void TryGetVariableText_ExplicitMethod_PrintsItAsSent()
    {
        // curl -X PATCH -w "%{method}" printed PATCH.
        TransferWriteOutVariables variables = WithReport(new TransferReport { Method = "PATCH" });

        Assert.AreEqual("PATCH", Get(variables, "method"));
    }

    [TestMethod]
    public void TryGetVariableText_IPv6Endpoints_PrintWithoutBrackets()
    {
        // curl -w "%{local_ip}|%{remote_ip}|%{remote_port}" http://[::1]:18226/ printed "::1|::1|18226".
        TransferReport report = new()
        {
            LocalEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 50200),
            RemoteEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 18226),
        };
        TransferWriteOutVariables variables = WithReport(report);

        Assert.AreEqual("::1", Get(variables, "local_ip"));
        Assert.AreEqual("::1", Get(variables, "remote_ip"));
        Assert.AreEqual("18226", Get(variables, "remote_port"));
    }

    [TestMethod]
    public void TryGetVariableText_FollowedRedirect_PrintsTheFollowersUrlAndCount()
    {
        TransferReport report = new() { EffectiveUrl = "http://127.0.0.1:18225/next", RedirectCount = 2 };
        TransferWriteOutVariables variables = WithReport(report);

        Assert.AreEqual("http://127.0.0.1:18225/next", Get(variables, "url_effective"));
        Assert.AreEqual("2", Get(variables, "num_redirects"));
    }

    [TestMethod]
    public void TryGetVariableText_ReportSizes_WinOverBytesTransferred()
    {
        TransferWriteOutVariables variables = new(
            TransferResult.Success(999) with { Report = new TransferReport { DownloadSize = 7 } }, LoopbackUrl, 0, LoopbackUrl, "http", Clock);

        Assert.AreEqual("7", Get(variables, "size_download"));
    }

    [TestMethod]
    [DataRow("nosuch")]
    [DataRow("url.bogus")]
    [DataRow("urle.bogus")]
    [DataRow("url.")]
    [DataRow("referer")]
    [DataRow("num_cert")]
    [DataRow("HTTP_CODE")]
    [DataRow(" http_code")]
    [DataRow("")]
    public void TryGetVariableText_UnknownName_IsNotKnown(string name)
    {
        TransferWriteOutVariables variables = WithReport(new TransferReport());

        Assert.IsFalse(variables.TryGetVariableText(name, out string? text));
        Assert.IsNull(text);
    }

    [TestMethod]
    public void FindFirstHeaderValue_DuplicatesAndPadding_FirstValueTrimmedCaseInsensitively()
    {
        // curl -w "[%header{x-dup}][%header{X-DUP}][%header{x-lf}][%header{ x-dup}]" printed "[one][one][a b][]".
        TransferReport report = new()
        {
            ResponseHeaders = [new("X-Dup", "one"), new("X-Dup", "two"), new("X-Lf", " \ta b  ")],
        };
        TransferWriteOutVariables variables = WithReport(report);

        Assert.AreEqual("one", variables.FindFirstHeaderValue("x-dup"));
        Assert.AreEqual("one", variables.FindFirstHeaderValue("X-DUP"));
        Assert.AreEqual("a b", variables.FindFirstHeaderValue("x-lf"));
        Assert.IsNull(variables.FindFirstHeaderValue(" x-dup"));
    }

    [TestMethod]
    public void FindFirstHeaderValue_NoReport_FindsNothing()
    {
        TransferWriteOutVariables variables = new(TransferResult.Success(0), LoopbackUrl, 0, LoopbackUrl, "http", Clock);

        Assert.IsNull(variables.FindFirstHeaderValue("content-length"));
    }

    [TestMethod]
    public void TryGetVariableText_TimedPost_MatchesCurl()
    {
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

        Assert.AreEqual(
            "0.000065|0.005721|0.000000|0.006038|0.006038|0.057008|0.000000|0.057115|350170|87542",
            RenderTimesAndSpeeds(WithReport(report)));
    }

    [TestMethod]
    public void TryGetVariableText_NoTimings_PrintsZeroes()
    {
        // curl -s -o NUL -w "..." file:///tmp/bl226.bin (100000 bytes) printed zero for both speeds.
        TransferWriteOutVariables variables = new(TransferResult.Success(100000), "file:///tmp/bl226.bin", 0, "file:///tmp/bl226.bin", "file", Clock);

        Assert.AreEqual(
            "0.000000|0.000000|0.000000|0.000000|0.000000|0.000000|0.000000|0.000000|0|0",
            RenderTimesAndSpeeds(variables));
    }

    [TestMethod]
    public void TryGetVariableText_TlsAfterRedirect_PrintsSecondsPastOneAndTheRedirectTime()
    {
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

        Assert.AreEqual(
            "0.000000|1.600000|1.700001|1.700002|1.700003|1.800000|1.500000|2.000000|1500000|0",
            RenderTimesAndSpeeds(WithReport(report)));
    }

    [TestMethod]
    public void TryGetVariableText_EventAtTheStart_PrintsOneMicrosecond()
    {
        // Curl_pgrsTime makes every event that happened at least one microsecond after the start.
        TransferReport report = new()
        {
            DownloadSize = 5,
            Timings = new TransferTimings(Started: 42, Connect: null, RequestReady: 42, RequestSent: null, FirstByteReceived: null, Completed: 42),
        };

        Assert.AreEqual(
            "0.000000|0.000000|0.000000|0.000001|0.000000|0.000000|0.000000|0.000001|5000000|0",
            RenderTimesAndSpeeds(WithReport(report)));
    }

    [TestMethod]
    [DataRow(2_000_000L, "4611686018427387")]
    [DataRow(999_999L, "9223372036854775807")]
    public void TryGetVariableText_SizeTooLargeToScale_DividesAsTrspeed(long totalMicroseconds, string expected)
    {
        // trspeed: size / whole seconds from one second on, the largest value below it.
        TransferReport report = new()
        {
            DownloadSize = long.MaxValue / 1000,
            Timings = new TransferTimings(Started: 0, Connect: null, RequestReady: null, RequestSent: null, FirstByteReceived: null, Completed: totalMicroseconds),
        };

        Assert.AreEqual(expected, Get(WithReport(report), "speed_download"));
    }

    [TestMethod]
    public void TryGetVariableText_FileTransfer_PrintsTheFixedVariablesAsCurl()
    {
        // curl -s -o out.bin -w "%{<name>}" file:///Z:/bl284tmp/wo.txt, one name at a time; see BL-284's Notes.
        TransferWriteOutVariables variables = new(
            TransferResult.Success(3), "file:///Z:/bl284tmp/wo.txt", 0, "file:///Z:/bl284tmp/wo.txt", "file", Clock);

        Assert.AreEqual("0|0|0|0|", RenderFixed(variables));
    }

    [TestMethod]
    public void TryGetVariableText_HttpTransfer_PrintsTheFixedVariablesAsCurl()
    {
        // curl -s -o out.bin -w "%{<name>}" "http://u:p@127.0.0.1:18284/wo.txt?q=1#frag", one name at a time.
        TransferWriteOutVariables variables = WithReport(new TransferReport { ResponseCode = 200, ConnectionCount = 1 });

        Assert.AreEqual("0|0|0|0|", RenderFixed(variables));
    }

    [TestMethod]
    public void TryGetVariableText_FailedTransfer_PrintsTheFixedVariablesAsCurl()
    {
        // curl -s -o out.bin -w "..." https://self-signed.badssl.com/ exited 60 with ssl_verify_result 0 under Schannel.
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.PeerFailedVerification, "SSL certificate problem"),
            "https://self-signed.badssl.com/", 0, "https://self-signed.badssl.com/", "https", Clock);

        Assert.AreEqual("0|0|0|0|", RenderFixed(variables));
    }

    [TestMethod]
    public void TryGetVariableText_TimeQueueWithTimings_PrintsOneMicrosecond()
    {
        // curl printed 0.000083 and 0.000038: the queue is left as the transfer starts, which is the handler's start here.
        TransferReport report = new()
        {
            Timings = new TransferTimings(Started: 500, Connect: null, RequestReady: null, RequestSent: null, FirstByteReceived: null, Completed: 900),
        };

        Assert.AreEqual("0.000001", Get(WithReport(report), "time_queue"));
    }

    [TestMethod]
    public void TryGetVariableText_TimeQueueWithoutTimings_PrintsZero()
    {
        Assert.AreEqual("0.000000", Get(WithReport(new TransferReport()), "time_queue"));
    }

    [TestMethod]
    public void TryGetVariableText_UrlPartsOfFileUrl_MatchCurl()
    {
        // curl -s -o out.bin -w "..." file:///Z:/bl284tmp/wo.txt (BL-284's Notes); url_effective is file://Z:/bl284tmp/wo.txt.
        TransferWriteOutVariables variables = new(
            TransferResult.Success(3), "file:///Z:/bl284tmp/wo.txt", 0, "file://Z:/bl284tmp/wo.txt", "file", Clock);

        Assert.AreEqual("file|||||0|Z:/bl284tmp/wo.txt|||", RenderUrlParts(variables, "url."));
        Assert.AreEqual("file|||||0|Z:/bl284tmp/wo.txt|||", RenderUrlParts(variables, "urle."));
    }

    [TestMethod]
    public void TryGetVariableText_UrlPartsOfHttpUrl_MatchCurl()
    {
        // curl -s -o NUL -w "..." "http://u:p@127.0.0.1:18284/wo.txt?q=1#frag" (BL-284's Notes).
        const string given = "http://u:p@127.0.0.1:18284/wo.txt?q=1#frag";
        TransferReport report = new() { EffectiveUrl = given };
        TransferWriteOutVariables variables = new(TransferResult.Success(0) with { Report = report }, given, 0, given, "http", Clock);

        Assert.AreEqual("http|u|p||127.0.0.1|18284|/wo.txt|q=1|frag|", RenderUrlParts(variables, "url."));
        Assert.AreEqual("http|u|p||127.0.0.1|18284|/wo.txt|q=1|frag|", RenderUrlParts(variables, "urle."));
    }

    [TestMethod]
    public void TryGetVariableText_UrlPartsOfUrlWithoutScheme_GuessHttp()
    {
        // curl -s -o NUL -w "..." 127.0.0.1:18284 (BL-284's Notes).
        TransferWriteOutVariables variables = new(
            TransferResult.Success(0), "127.0.0.1:18284", 0, "http://127.0.0.1:18284/", "http", Clock);

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
        TransferWriteOutVariables variables = new(TransferResult.Success(0), url, 0, url, null, Clock);

        Assert.AreEqual(expected, Get(variables, "url.port"));
    }

    [TestMethod]
    public void TryGetVariableText_UrlPartsOfOptionsAndZoneId_AreRead()
    {
        const string given = "imap://u;AUTH=PLAIN@[fe80::1%25eth0]/";
        TransferWriteOutVariables variables = new(TransferResult.Success(0), given, 0, given, "imap", Clock);

        Assert.AreEqual("imap|u||AUTH=PLAIN|[fe80::1]|143|/|||eth0", RenderUrlParts(variables, "url."));
    }

    [TestMethod]
    public void TryGetVariableText_UrlThatDoesNotParse_PrintsNothing()
    {
        TransferWriteOutVariables variables = new(TransferResult.Success(0), "http://[::1", 0, "http://[::1", "http", Clock);

        Assert.AreEqual("|||||||||", RenderUrlParts(variables, "url."));
    }

    [TestMethod]
    [DataRow("file:///Z:/bl284tmp/wo.txt", "file")]
    [DataRow("http://127.0.0.1:18284/wo.txt", "http")]
    public void TryGetVariableText_CertificatesWithoutTls_AreZeroAndNothing(string url, string scheme)
    {
        // curl -s -o out.bin -w "[%{num_certs}][%{certs}]" for file:// and http:// (BL-284's Notes).
        TransferWriteOutVariables variables = new(TransferResult.Success(0) with { Report = new TransferReport() }, url, 0, url, scheme, Clock);

        Assert.AreEqual("[0][]", $"[{Get(variables, "num_certs")}][{Get(variables, "certs")}]");
    }

    [TestMethod]
    public void TryGetVariableText_CertificatesWithoutReport_AreZeroAndNothing()
    {
        TransferWriteOutVariables variables = new(TransferResult.Success(0), LoopbackUrl, 0, LoopbackUrl, "http", Clock);

        Assert.AreEqual("[0][]", $"[{Get(variables, "num_certs")}][{Get(variables, "certs")}]");
    }

    [TestMethod]
    public void TryGetVariableText_CertificatesOfLoopbackHttpsTransfer_MatchCurl()
    {
        // curl -k -s -o NUL -w "%{num_certs}\n%{certs}" https://127.0.0.1:18304/ (BL-303's Notes).
        TransferWriteOutVariables variables = WithReport(new TransferReport { PeerCertificates = LoopbackChain.Certificates });

        Assert.AreEqual("3", Get(variables, "num_certs"));
        Assert.AreEqual(LoopbackChain.CertsText, Get(variables, "certs"));
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        TransferResult result = TransferResult.Success(0);

        Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(null!, LoopbackUrl, 0, LoopbackUrl, "http", Clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(result, null!, 0, LoopbackUrl, "http", Clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(result, LoopbackUrl, 0, null!, "http", Clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(result, LoopbackUrl, 0, LoopbackUrl, "http", null!));
    }

    [TestMethod]
    public void Lookups_NullName_Throw()
    {
        TransferWriteOutVariables variables = WithReport(new TransferReport());

        Assert.ThrowsExactly<ArgumentNullException>(() => variables.TryGetVariableText(null!, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => variables.FindFirstHeaderValue(null!));
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

    private static string Get(TransferWriteOutVariables variables, string name)
    {
        Assert.IsTrue(variables.TryGetVariableText(name, out string? text), name);
        return text;
    }

    private sealed class MicrosecondTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => 1_000_000;
    }
}
