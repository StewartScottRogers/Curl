using System.Net;
using Curl.Api.Info;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins the <see cref="TransferWriteOutVariables"/> built from a <see cref="TransferInfo"/>: each
/// libcurl variable prints the info's value as curl's <c>tool_writeout.c</c> prints its
/// <c>curl_easy_getinfo</c> answer, one test per variable group (BL-2319).
/// </summary>
[TestClass]
public sealed class TransferWriteOutVariablesFromTransferInfoTests
{
    private const string GivenUrl = "http://example.test/a";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TryGetVariableText_Times_PrintTheInfoTimesInMicroseconds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferInfo info = new()
        {
            QueueTime = TimeSpan.FromMicroseconds(1),
            NameLookupTime = TimeSpan.FromMicroseconds(1_500),
            ConnectTime = TimeSpan.FromMicroseconds(2_000),
            AppConnectTime = TimeSpan.FromMicroseconds(3_250),
            PreTransferTime = TimeSpan.FromMicroseconds(3_300),
            PostTransferTime = TimeSpan.FromMicroseconds(3_400),
            StartTransferTime = TimeSpan.FromMicroseconds(4_000),
            RedirectTime = TimeSpan.FromMicroseconds(250),
            TotalTime = TimeSpan.FromSeconds(1.25) + TimeSpan.FromTicks(7),
        };
        diagnostics.Arrange("total time ticks", info.TotalTime.Ticks);

        string actual = Render(info, "time_queue", "time_namelookup", "time_connect", "time_appconnect", "time_pretransfer", "time_posttransfer", "time_starttransfer", "time_redirect", "time_total");

        Show(diagnostics, "0.000001|0.001500|0.002000|0.003250|0.003300|0.003400|0.004000|0.000250|1.250000", actual);
    }

    [TestMethod]
    public void TryGetVariableText_Sizes_PrintTheInfoSizesAndTheReportsDeliveredSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferInfo info = new() { SizeDownload = 51, SizeUpload = 7, HeaderSize = 111, RequestSize = 82 };
        TransferResult decoded = TransferResult.Success(51) with { Report = new TransferReport { DeliveredSize = 501 } };
        diagnostics.Arrange("download size", info.SizeDownload);

        string undecoded = Render(info, TransferResult.Success(0), "size_download", "size_upload", "size_header", "size_request", "size_delivered");
        string delivered = Render(info, decoded, "size_delivered");

        Show(diagnostics, "51|7|111|82|51", undecoded);
        Show(diagnostics, "501", delivered);
    }

    [TestMethod]
    public void TryGetVariableText_Speeds_PrintTheInfoSpeeds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferInfo info = new() { SpeedDownload = 40_960, SpeedUpload = 12 };
        diagnostics.Arrange("download speed", info.SpeedDownload);

        string actual = Render(info, "speed_download", "speed_upload");

        Show(diagnostics, "40960|12", actual);
    }

    [TestMethod]
    public void TryGetVariableText_Addresses_PrintTheInfoAddressesAndPorts()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferInfo connected = new() { PrimaryIp = "192.0.2.7", PrimaryPort = 8443, LocalIp = "192.0.2.1", LocalPort = 50123 };
        TransferInfo unconnectedLocalEnd = new() { PrimaryIp = "192.0.2.7", PrimaryPort = 69 };
        TransferInfo noConnection = new();
        diagnostics.Arrange("primary port", connected.PrimaryPort);

        string connectedText = Render(connected, "remote_ip", "remote_port", "local_ip", "local_port");
        string unconnectedLocalEndText = Render(unconnectedLocalEnd, "remote_port", "local_ip", "local_port");
        string noConnectionText = Render(noConnection, "remote_ip", "remote_port", "local_ip", "local_port");

        Show(diagnostics, "192.0.2.7|8443|192.0.2.1|50123", connectedText);
        Show(diagnostics, "69||0", unconnectedLocalEndText);
        Show(diagnostics, "|-1||-1", noConnectionText);
    }

    [TestMethod]
    public void TryGetVariableText_Http_PrintsTheInfoCodesVersionMethodTypeAndScheme()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferInfo info = new()
        {
            ResponseCode = 404,
            HttpConnectCode = 200,
            HttpVersion = 2,
            EffectiveMethod = "POST",
            ContentType = "text/html",
            Scheme = "https",
            UsedProxy = 1,
            FtpEntryPath = "/home/u",
        };
        diagnostics.Arrange("response code", info.ResponseCode);

        string actual = Render(info, "response_code", "http_code", "http_connect", "http_version", "method", "content_type", "scheme", "proxy_used", "ftp_entry_path");
        string versions = string.Join('|', new long[] { 0, 1, 2, 3, 30, 4, 29, 31, -1 }.Select(version => Render(new TransferInfo { HttpVersion = version }, "http_version")));
        string fresh = Render(new TransferInfo(), "response_code", "http_connect", "method", "content_type", "scheme");

        Show(diagnostics, "404|404|200|1.1|POST|text/html|https|1|/home/u", actual);
        Show(diagnostics, "0|1|1.1|2|3|0|0|0|0", versions);
        Show(diagnostics, "000|000|GET||", fresh);
    }

    [TestMethod]
    public void TryGetVariableText_Tls_PrintsTheInfoVerifyResultsAndEarlyData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferInfo info = new() { SslVerifyResult = 18, ProxySslVerifyResult = 20, EarlyDataSent = -5 };
        diagnostics.Arrange("verify result", info.SslVerifyResult);

        string actual = Render(info, "ssl_verify_result", "proxy_ssl_verify_result", "tls_earlydata");

        Show(diagnostics, "18|20|-5", actual);
    }

    [TestMethod]
    public void TryGetVariableText_Redirects_PrintTheInfoRedirectUrlCountEffectiveUrlAndReferer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferInfo info = new()
        {
            EffectiveUrl = "https://next.test:8443/b?c=1",
            RedirectUrl = "https://elsewhere.test/",
            RedirectCount = 2,
            Referer = "https://example.test/a",
        };
        diagnostics.Arrange("effective url", info.EffectiveUrl);

        string actual = Render(info, "url_effective", "redirect_url", "num_redirects", "referer", "urle.host", "urle.port", "url.host");

        Show(diagnostics, "https://next.test:8443/b?c=1|https://elsewhere.test/|2|https://example.test/a|next.test|8443|example.test", actual);
    }

    [TestMethod]
    public void TryGetVariableText_Ids_PrintTheInfoConnectionAndTransferIdsAndConnectionCount()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferInfo info = new() { ConnectionId = 3, TransferId = 5, NumConnects = 1 };
        diagnostics.Arrange("connection id", info.ConnectionId);

        string actual = Render(info, "conn_id", "xfer_id", "num_connects");
        string fresh = Render(new TransferInfo(), "conn_id", "xfer_id");

        Show(diagnostics, "3|5|1", actual);
        Show(diagnostics, "-1|-1", fresh);
    }

    [TestMethod]
    public void TryGetVariableText_ToolVariables_ComeFromTheResultAndArguments()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferResult result = TransferResult.Failure(CurlExitCode.CouldntConnect, "Failed to connect");
        diagnostics.Arrange("exit code", result.ExitCode);
        TransferWriteOutVariables variables = new(new TransferInfo(), result, GivenUrl, 2) { OutputFileName = "out.txt", RetryCount = 1 };

        string actual = Render(variables, "exitcode", "errormsg", "url", "urlnum", "filename_effective", "num_retries", "num_headers", "num_certs");

        Show(diagnostics, "7|Failed to connect|http://example.test/a|2|out.txt|1|0|0", actual);
        Assert.IsTrue(variables.TransferFailed);
    }

    [TestMethod]
    public void InfoProperties_Set_ReplaceTheInfoValuesTheyReadBack()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferInfo info = new() { Referer = "a", ConnectionId = 1, TransferId = 2, SslVerifyResult = 3, ProxySslVerifyResult = 4, EarlyDataSent = 5 };
        diagnostics.Arrange("info connection id", info.ConnectionId);

        TransferWriteOutVariables variables = new(info, TransferResult.Success(0), GivenUrl, 0)
        {
            Referer = "b",
            ConnectionId = 10,
            TransferId = 20,
            SslVerifyResult = 30,
            ProxySslVerifyResult = 40,
            TlsEarlyDataSent = 50,
        };
        string properties = $"{variables.Referer}|{variables.ConnectionId}|{variables.TransferId}|{variables.SslVerifyResult}|{variables.ProxySslVerifyResult}|{variables.TlsEarlyDataSent}";

        Show(diagnostics, "b|10|20|30|40|50", properties);
        Show(diagnostics, "b|10|20|30|40|50", Render(variables, "referer", "conn_id", "xfer_id", "ssl_verify_result", "proxy_ssl_verify_result", "tls_earlydata"));
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferResult result = TransferResult.Success(0);
        diagnostics.Arrange("url", GivenUrl);

        var nullInfo = Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(null!, result, GivenUrl, 0));
        var nullResult = Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(new TransferInfo(), null!, GivenUrl, 0));
        var nullUrl = Assert.ThrowsExactly<ArgumentNullException>(() => new TransferWriteOutVariables(new TransferInfo(), result, null!, 0));

        diagnostics.Assert("null info parameter", "info", nullInfo.ParamName);
        diagnostics.Assert("null result parameter", "result", nullResult.ParamName);
        diagnostics.Assert("null url parameter", "url", nullUrl.ParamName);
    }

    private static string Render(TransferInfo info, params string[] names) => Render(info, TransferResult.Success(0), names);

    private static string Render(TransferInfo info, TransferResult result, params string[] names) =>
        Render(new TransferWriteOutVariables(info, result, GivenUrl, 0), names);

    private static string Render(TransferWriteOutVariables variables, params string[] names) =>
        string.Join('|', names.Select(name => variables.TryGetVariableText(name, out string? text) ? text : "?"));

    private static void Show(TestDiagnostics diagnostics, string expected, string actual)
    {
        diagnostics.Act("rendered", actual);
        diagnostics.Assert("rendered", expected, actual);
    }
}
