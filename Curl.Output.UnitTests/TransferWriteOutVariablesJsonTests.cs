using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <c>%{json}</c> and <c>%{header_json}</c> from <see cref="TransferWriteOutVariables"/>
/// to what curl 8.21.0 (mingw, Schannel) printed on 2026-09-27; the commands are in
/// BL-227's Notes.
/// </summary>
[TestClass]
public sealed class TransferWriteOutVariablesJsonTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>The reference build's <c>curl_version</c>, so a test can pin curl's bytes whole.</summary>
    private const string ReferenceLibraryVersion =
        "libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP";

    /// <summary>A clock whose timestamps are microseconds, so a test writes curl's microsecond values directly.</summary>
    private static readonly TimeProvider Clock = new MicrosecondTimeProvider();

    [TestMethod]
    public void TryGetVariableText_JsonOfHttpTransfer_MatchesCurl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // Record-CurlExchange.ps1 -Port 18227 -Response 'HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n
        // Content-Length: 5\r\nX-A: 1\r\n\r\nhello' -CurlArgs -s,-o,NUL,-w,%{json},http://127.0.0.1:18227/j?q=1
        // curl printed time_queue 0.000055; this tool's queue time is one microsecond (ADR-0035).
        const string url = "http://127.0.0.1:18227/j?q=1";
        TransferReport report = new()
        {
            ResponseCode = 200,
            HttpVersion = new Version(1, 1),
            Method = "GET",
            ContentType = "text/plain",
            HeaderSize = 72,
            RequestSize = 84,
            DownloadSize = 5,
            ConnectionCount = 1,
            ResponseHeaders = [new("Content-Type", "text/plain"), new("Content-Length", "5"), new("X-A", "1")],
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 62095),
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 18227),
            Timings = new TransferTimings(
                Started: 1_000_000,
                Connect: new ConnectTimings(Started: 1_000_010, NameResolved: 1_000_071, Connected: 1_000_708, TlsHandshakeCompleted: null),
                RequestReady: 1_000_784,
                RequestSent: 1_000_784,
                FirstByteReceived: 1_032_135,
                Completed: 1_032_216),
        };
        TransferWriteOutVariables variables = new(TransferResult.Success(5) with { Report = report }, url, 0, url, "http", Clock)
        {
            OutputFileName = "NUL",
            ConnectionId = 0,
            LibraryVersion = ReferenceLibraryVersion,
        };

        diagnostics.Arrange("url", url);
        diagnostics.Arrange("report", report);

        string json = Get(variables, "json");
        diagnostics.Act("json", json);

        string expectedJson =
            "{\"certs\":\"\",\"conn_id\":0,\"content_type\":\"text/plain\",\"errormsg\":null,\"exitcode\":0,"
            + "\"filename_effective\":\"NUL\",\"ftp_entry_path\":null,\"http_code\":200,\"http_connect\":0,"
            + "\"http_version\":\"1.1\",\"local_ip\":\"127.0.0.1\",\"local_port\":62095,\"method\":\"GET\",\"num_certs\":0,"
            + "\"num_connects\":1,\"num_headers\":3,\"num_redirects\":0,\"num_retries\":0,\"proxy_ssl_verify_result\":0,"
            + "\"proxy_used\":0,\"redirect_url\":null,\"referer\":null,\"remote_ip\":\"127.0.0.1\",\"remote_port\":18227,"
            + "\"response_code\":200,\"scheme\":\"http\",\"size_delivered\":5,\"size_download\":5,\"size_header\":72,"
            + "\"size_request\":84,\"size_upload\":0,\"speed_download\":155,\"speed_upload\":0,\"ssl_verify_result\":0,"
            + "\"time_appconnect\":0.000000,\"time_connect\":0.000708,\"time_namelookup\":0.000071,"
            + "\"time_posttransfer\":0.000784,\"time_pretransfer\":0.000784,\"time_queue\":0.000001,"
            + "\"time_redirect\":0.000000,\"time_starttransfer\":0.032135,\"time_total\":0.032216,\"tls_earlydata\":0,"
            + "\"url\":\"http://127.0.0.1:18227/j?q=1\",\"url.fragment\":null,\"url.host\":\"127.0.0.1\",\"url.options\":null,"
            + "\"url.password\":null,\"url.path\":\"/j\",\"url.port\":\"18227\",\"url.query\":\"q=1\",\"url.scheme\":\"http\","
            + "\"url.user\":null,\"url.zoneid\":null,\"url_effective\":\"http://127.0.0.1:18227/j?q=1\",\"urle.fragment\":null,"
            + "\"urle.host\":\"127.0.0.1\",\"urle.options\":null,\"urle.password\":null,\"urle.path\":\"/j\",\"urle.port\":\"18227\","
            + "\"urle.query\":\"q=1\",\"urle.scheme\":\"http\",\"urle.user\":null,\"urle.zoneid\":null,\"urlnum\":0,\"xfer_id\":0,"
            + "\"curl_version\":\"" + ReferenceLibraryVersion + "\"}";
        diagnostics.Diff("json", expectedJson, json);
        Assert.AreEqual(expectedJson, json);
    }

    [TestMethod]
    public void TryGetVariableText_JsonOfUnsupportedScheme_MatchesCurl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -w "%{json}" foo://x/ exited 1. curl printed time_pretransfer, time_posttransfer and
        // time_starttransfer 0.000014 and time_total 0.000019; a transfer no handler ran has no timings here.
        const string url = "foo://x/";
        TransferWriteOutVariables variables = new(
            TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Protocol \"foo\" not supported"), url, 0, url, null, Clock)
        {
            LibraryVersion = ReferenceLibraryVersion,
        };

        diagnostics.Arrange("url", url);
        diagnostics.Arrange("result", "Failure UnsupportedProtocol");

        string json = Get(variables, "json");
        diagnostics.Act("json", json);

        string expectedJson =
            "{\"certs\":\"\",\"conn_id\":-1,\"content_type\":null,\"errormsg\":\"Protocol \\\"foo\\\" not supported\",\"exitcode\":1,"
            + "\"filename_effective\":null,\"ftp_entry_path\":null,\"http_code\":0,\"http_connect\":0,\"http_version\":\"0\","
            + "\"local_ip\":\"\",\"local_port\":-1,\"method\":\"GET\",\"num_certs\":0,\"num_connects\":0,\"num_headers\":0,"
            + "\"num_redirects\":0,\"num_retries\":0,\"proxy_ssl_verify_result\":0,\"proxy_used\":0,\"redirect_url\":null,"
            + "\"referer\":null,\"remote_ip\":\"\",\"remote_port\":-1,\"response_code\":0,\"scheme\":null,\"size_delivered\":0,"
            + "\"size_download\":0,\"size_header\":0,\"size_request\":0,\"size_upload\":0,\"speed_download\":0,\"speed_upload\":0,"
            + "\"ssl_verify_result\":0,\"time_appconnect\":0.000000,\"time_connect\":0.000000,\"time_namelookup\":0.000000,"
            + "\"time_posttransfer\":0.000000,\"time_pretransfer\":0.000000,\"time_queue\":0.000000,\"time_redirect\":0.000000,"
            + "\"time_starttransfer\":0.000000,\"time_total\":0.000000,\"tls_earlydata\":0,\"url\":\"foo://x/\","
            + "\"url.fragment\":null,\"url.host\":\"x\",\"url.options\":null,\"url.password\":null,\"url.path\":\"/\","
            + "\"url.port\":null,\"url.query\":null,\"url.scheme\":\"foo\",\"url.user\":null,\"url.zoneid\":null,"
            + "\"url_effective\":\"foo://x/\",\"urle.fragment\":null,\"urle.host\":\"x\",\"urle.options\":null,"
            + "\"urle.password\":null,\"urle.path\":\"/\",\"urle.port\":null,\"urle.query\":null,\"urle.scheme\":\"foo\","
            + "\"urle.user\":null,\"urle.zoneid\":null,\"urlnum\":0,\"xfer_id\":0,"
            + "\"curl_version\":\"" + ReferenceLibraryVersion + "\"}";
        diagnostics.Diff("json", expectedJson, json);
        Assert.AreEqual(expectedJson, json);
    }

    [TestMethod]
    public void TryGetVariableText_JsonEscapesControlCharactersQuotesAndBackslashes_AsCurl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o NUL -e $'a\rb\nc\x02' -w '%{json}' file:///nonexist printed "referer":"a\rb\nc\u0002";
        // a Content-Type of a"b\c<TAB>d<0x1F> printed "content_type":"a\"b\\c\td\u001f".
        TransferReport report = new() { ContentType = "a\"b\\c\td\u001f" };
        TransferWriteOutVariables variables = new(TransferResult.Success(0) with { Report = report }, "http://h/", 0, "http://h/", "http", Clock)
        {
            Referer = "a\rb\nc\u0002",
        };

        diagnostics.Arrange("referer", variables.Referer);
        diagnostics.Arrange("content type", report.ContentType);

        string json = Get(variables, "json");
        diagnostics.Act("json", json);
        diagnostics.Assert("contains escaped referer", true, json.Contains("\"referer\":\"a\\rb\\nc\\u0002\"", StringComparison.Ordinal));
        diagnostics.Assert("contains escaped content type", true, json.Contains("\"content_type\":\"a\\\"b\\\\c\\td\\u001f\"", StringComparison.Ordinal));

        StringAssert.Contains(json, "\"referer\":\"a\\rb\\nc\\u0002\"");
        StringAssert.Contains(json, "\"content_type\":\"a\\\"b\\\\c\\td\\u001f\"");
    }

    [TestMethod]
    public void TryGetVariableText_HeaderJson_GroupsLowerCasedNamesInFirstSeenOrder()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // Record-CurlExchange.ps1 -Port 18227 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-Dup: one\r\n
        // Set-Cookie: a=1\r\nx-dup:  two  \r\nX-Quote: a"b\\c\x01\x7f\r\nContent-Type: text/plain\r\nEmpty:\r\n\r\nhello'
        // -CurlArgs -s,-o,NUL,-w,%{header_json},http://127.0.0.1:18227/w, the line feeds written as CR LF.
        TransferReport report = new()
        {
            ResponseHeaders =
            [
                new("Content-Length", "5"),
                new("X-Dup", "one"),
                new("Set-Cookie", "a=1"),
                new("x-dup", "  two  "),
                new("X-Quote", "a\"b\\c\u0001\u007f"),
                new("Content-Type", "text/plain"),
                new("Empty", string.Empty),
            ],
        };

        diagnostics.Arrange("header count", report.ResponseHeaders.Count);

        string headerJson = Get(WithReport(report), "header_json");
        diagnostics.Act("header_json", headerJson);

        string expectedJson =
            "{\"content-length\":[\"5\"],\n\"x-dup\":[\"one\",\"two\"],\n\"set-cookie\":[\"a=1\"],\n"
            + "\"x-quote\":[\"a\\\"b\\\\c\\u0001\u007f\"],\n\"content-type\":[\"text/plain\"],\n\"empty\":[\"\"]\n}";
        diagnostics.Diff("header_json", expectedJson, headerJson);
        Assert.AreEqual(expectedJson, headerJson);
    }

    [TestMethod]
    public void TryGetVariableText_HeaderJsonEscapesTabBackspaceAndFormFeed_AsCurl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // A header X-C: a<TAB>b<BS>c<FF>d<0x1F>e/f printed "x-c":["a\tb\bc\fd\u001fe/f"].
        TransferReport report = new() { ResponseHeaders = [new("Content-Length", "0"), new("X-C", "a\tb\bc\fd\u001fe/f")] };

        diagnostics.Arrange("header count", report.ResponseHeaders.Count);

        string headerJson = Get(WithReport(report), "header_json");
        diagnostics.Act("header_json", headerJson);

        string expectedJson =
            "{\"content-length\":[\"0\"],\n\"x-c\":[\"a\\tb\\bc\\fd\\u001fe/f\"]\n}";
        diagnostics.Diff("header_json", expectedJson, headerJson);
        Assert.AreEqual(expectedJson, headerJson);
    }

    [TestMethod]
    public void TryGetVariableText_HeaderJsonWithoutHeaders_IsEmptyObjectOverTwoLines()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl -s -o /dev/null -w '%{header_json}' file:///tmp/a.txt printed {, CR LF and }.
        diagnostics.Arrange("report", "no response headers");

        string headerJson = Get(WithReport(new TransferReport()), "header_json");

        diagnostics.Act("header_json", headerJson);
        diagnostics.Diff("header_json", "{\n}", headerJson);
        Assert.AreEqual("{\n}", headerJson);
    }

    [TestMethod]
    public void TryGetVariableText_SizeDeliveredWithoutADeliveredSize_IsTheDownloadSize()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // With no --compressed, curl printed size_delivered equal to size_download: 5 for a 5-byte body.
        diagnostics.Arrange("download size", 5);

        string delivered = Get(WithReport(new TransferReport { DownloadSize = 5 }), "size_delivered");

        diagnostics.Act("size_delivered", delivered);
        diagnostics.Diff("size_delivered", "5", delivered);
        Assert.AreEqual("5", delivered);
    }

    [TestMethod]
    public void TryGetVariableText_SizesOfADecodedBody_ComeFromTheirOwnCounts()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // curl --compressed -w '%{size_download} %{size_delivered}' printed 51 501 for a 51-byte gzip body of 501 bytes (BL-516).
        TransferWriteOutVariables variables = WithReport(new TransferReport { DownloadSize = 51, DeliveredSize = 501 });

        diagnostics.Arrange("download size", 51);
        diagnostics.Arrange("delivered size", 501);

        string download = Get(variables, "size_download");
        string delivered = Get(variables, "size_delivered");

        diagnostics.Act("size_download", download);
        diagnostics.Act("size_delivered", delivered);
        diagnostics.Diff("size_download", "51", download);
        diagnostics.Diff("size_delivered", "501", delivered);
        Assert.AreEqual("51", download);
        Assert.AreEqual("501", delivered);
    }

    [TestMethod]
    public void TryGetVariableText_SizeDeliveredWithoutAReport_IsTheBytesTransferred()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        TransferWriteOutVariables variables = new(TransferResult.Success(7), "http://h/", 0, "http://h/", "http", Clock);

        diagnostics.Arrange("bytes transferred", 7);

        string delivered = Get(variables, "size_delivered");

        diagnostics.Act("size_delivered", delivered);
        diagnostics.Diff("size_delivered", "7", delivered);
        Assert.AreEqual("7", delivered);
    }

    [TestMethod]
    [DataRow(true, false, "libcurl/8.21.0 Schannel")]
    [DataRow(true, true, "libcurl/8.21.0 Schannel")]
    [DataRow(false, true, "libcurl/8.21.0 SecureTransport")]
    [DataRow(false, false, "libcurl/8.21.0 OpenSSL")]
    public void FormatLibraryVersion_NamesThePlatformsTlsBackend(bool isWindows, bool isMacOS, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("isWindows", isWindows);
        diagnostics.Arrange("isMacOS", isMacOS);

        string actual = TransferWriteOutVariables.FormatLibraryVersion(isWindows, isMacOS);

        diagnostics.Act("library version", actual);
        diagnostics.Diff("library version", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void LibraryVersion_ByDefault_IsTheRunningSystems()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string expected = TransferWriteOutVariables.FormatLibraryVersion(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());
        diagnostics.Arrange("expected library version", expected);

        string json = Get(WithReport(new TransferReport()), "json");

        diagnostics.Act("json", json);
        diagnostics.Assert("json ends with curl_version", true, json.EndsWith("\"curl_version\":\"" + expected + "\"}", StringComparison.Ordinal));
        StringAssert.EndsWith(json, "\"curl_version\":\"" + expected + "\"}");
    }

    private static TransferWriteOutVariables WithReport(TransferReport report)
    {
        return new TransferWriteOutVariables(TransferResult.Success(0) with { Report = report }, "http://h/", 0, "http://h/", "http", Clock);
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
