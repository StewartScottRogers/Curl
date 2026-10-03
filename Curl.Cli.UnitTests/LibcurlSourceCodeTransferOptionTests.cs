namespace Curl.Cli;

/// <summary>
/// Pins the <c>curl_easy_setopt</c> lines <see cref="LibcurlSourceCode" /> writes for the transfer, redirect,
/// speed, time-condition and socket options against curl 8.21.0 (mingw, Schannel), measured on 2026-10-02
/// with <c>Record-CurlExchange.ps1 -NoServer</c> and <c>--libcurl - -s</c> (BL-1106 Notes). Each row is one
/// measured command line; the transfer's lines are compared byte for byte.
/// </summary>
[TestClass]
public sealed class LibcurlSourceCodeTransferOptionTests
{
    private const string HttpUrl = "http://127.0.0.1:1/";
    private const string FtpUrl = "ftp://127.0.0.1:1/f";

    private const string Start =
        "  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 102400L);\n"
        + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/\");\n"
        + "  curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 1L);\n";

    private const string FtpStart =
        "  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 102400L);\n"
        + "  curl_easy_setopt(curl, CURLOPT_URL, \"ftp://127.0.0.1:1/f\");\n"
        + "  curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 1L);\n";

    private const string Agent = "  curl_easy_setopt(curl, CURLOPT_USERAGENT, \"curl/8.21.0\");\n";
    private const string MaxRedirs = "  curl_easy_setopt(curl, CURLOPT_MAXREDIRS, 50L);\n";
    private const string PassiveIp = "  curl_easy_setopt(curl, CURLOPT_FTP_SKIP_PASV_IP, 1L);\n";
    private const string Tls = "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)CURL_SSLVERSION_TLSv1_2);\n";
    private const string KeepAlive = "  curl_easy_setopt(curl, CURLOPT_TCP_KEEPALIVE, 1L);\n";
    private const string Before = Start + Agent + MaxRedirs;
    private const string After = Tls + KeepAlive;

    [TestMethod]
    [DataRow("--request-target|*", Start + "  curl_easy_setopt(curl, CURLOPT_REQUEST_TARGET, \"*\");\n" + Agent + MaxRedirs + After)]
    [DataRow("-l", Start + "  curl_easy_setopt(curl, CURLOPT_DIRLISTONLY, 1L);\n" + Agent + MaxRedirs + After)]
    [DataRow("-a", Start + "  curl_easy_setopt(curl, CURLOPT_APPEND, 1L);\n" + Agent + MaxRedirs + After)]
    [DataRow("-B", Start + "  curl_easy_setopt(curl, CURLOPT_TRANSFERTEXT, 1L);\n" + Agent + MaxRedirs + After)]
    [DataRow("-r|0-5", Start + "  curl_easy_setopt(curl, CURLOPT_RANGE, \"0-5\");\n" + Agent + MaxRedirs + After)]
    [DataRow("--form-escape", Start + "  curl_easy_setopt(curl, CURLOPT_MIME_OPTIONS, 1L);\n" + Agent + MaxRedirs + After)]
    [DataRow("--disallow-username-in-url", Before + After + "  curl_easy_setopt(curl, CURLOPT_DISALLOW_USERNAME_IN_URL, 1L);\n")]
    [DataRow("--alt-svc|a.txt", Before + "  curl_easy_setopt(curl, CURLOPT_ALTSVC, \"a.txt\");\n" + After)]
    [DataRow("--hsts|h.txt", Before + "  curl_easy_setopt(curl, CURLOPT_HSTS, \"h.txt\");\n" + After)]
    [DataRow("--interface|lo", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_INTERFACE, \"lo\");\n" + KeepAlive)]
    [DataRow("--interface|if!lo", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_INTERFACE, \"if!lo\");\n" + KeepAlive)]
    [DataRow("--interface|host!1.2.3.4", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_INTERFACE, \"host!1.2.3.4\");\n" + KeepAlive)]
    [DataRow("--local-port|1000-2000", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_LOCALPORT, 1000L);\n  curl_easy_setopt(curl, CURLOPT_LOCALPORTRANGE, 1001L);\n" + KeepAlive)]
    [DataRow("--local-port|1000", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_LOCALPORT, 1000L);\n  curl_easy_setopt(curl, CURLOPT_LOCALPORTRANGE, 1L);\n" + KeepAlive)]
    [DataRow("--doh-url|https://d/q", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_DOH_URL, \"https://d/q\");\n" + KeepAlive)]
    [DataRow("--unix-socket|/s", Before + After + "  curl_easy_setopt(curl, CURLOPT_UNIX_SOCKET_PATH, \"/s\");\n")]
    [DataRow("--abstract-unix-socket|s", Before + After + "  curl_easy_setopt(curl, CURLOPT_ABSTRACT_UNIX_SOCKET, \"s\");\n")]
    [DataRow("--abstract-unix-socket|s|--unix-socket|u", Before + After + "  curl_easy_setopt(curl, CURLOPT_UNIX_SOCKET_PATH, \"u\");\n")]
    [DataRow("--no-keepalive", Before + Tls)]
    [DataRow("--no-keepalive|--keepalive-time|30", Before + Tls)]
    [DataRow("--keepalive-time|30", Before + After + "  curl_easy_setopt(curl, CURLOPT_TCP_KEEPIDLE, 30L);\n  curl_easy_setopt(curl, CURLOPT_TCP_KEEPINTVL, 30L);\n")]
    [DataRow("--keepalive-cnt|5", Before + After + "  curl_easy_setopt(curl, CURLOPT_TCP_KEEPCNT, 5L);\n")]
    [DataRow("--keepalive-time|0|--keepalive-cnt|0", Before + After)]
    [DataRow("-C|5", Before + "  curl_easy_setopt(curl, CURLOPT_RESUME_FROM_LARGE, (curl_off_t)5);\n" + After)]
    [DataRow("-C|-", Before + After)]
    [DataRow("--max-filesize|100", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_MAXFILESIZE_LARGE, (curl_off_t)100);\n" + KeepAlive)]
    [DataRow("--happy-eyeballs-timeout-ms|300", Before + After + "  curl_easy_setopt(curl, CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS, 300L);\n")]
    [DataRow("--expect100-timeout|2", Before + "  curl_easy_setopt(curl, CURLOPT_EXPECT_100_TIMEOUT_MS, 2000L);\n" + After)]
    [DataRow("--max-redirs|50|--max-filesize|0|--expect100-timeout|0|--happy-eyeballs-timeout-ms|0", Before + After)]
    [DataRow("-Y|100", Before + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_LIMIT, 100L);\n  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_TIME, 30L);\n" + After)]
    [DataRow("-y|20", Before + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_LIMIT, 1L);\n  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_TIME, 20L);\n" + After)]
    [DataRow("-Y|5|-y|0", Before + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_LIMIT, 5L);\n" + After)]
    [DataRow("-Y|0|-y|9", Before + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_LIMIT, 1L);\n  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_TIME, 9L);\n" + After)]
    [DataRow("-y|0", Before + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_LIMIT, 1L);\n" + After)]
    [DataRow("-z|20200101", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_TIMECONDITION, (long)CURL_TIMECOND_IFMODSINCE);\n  curl_easy_setopt(curl, CURLOPT_TIMEVALUE_LARGE, (curl_off_t)1577836800);\n" + KeepAlive)]
    [DataRow("-z|-20200101", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_TIMECONDITION, (long)CURL_TIMECOND_IFUNMODSINCE);\n  curl_easy_setopt(curl, CURLOPT_TIMEVALUE_LARGE, (curl_off_t)1577836800);\n" + KeepAlive)]
    [DataRow("-j", Before + "  curl_easy_setopt(curl, CURLOPT_COOKIESESSION, 1L);\n" + After)]
    [DataRow("--follow", Start + Agent + "  curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 2L);\n" + MaxRedirs + After)]
    [DataRow("-L|--follow", Start + Agent + "  curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 2L);\n" + MaxRedirs + After)]
    [DataRow("--follow|-L", Start + Agent + "  curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);\n" + MaxRedirs + After)]
    [DataRow("--max-redirs|5", Start + Agent + "  curl_easy_setopt(curl, CURLOPT_MAXREDIRS, 5L);\n" + After)]
    [DataRow("--max-redirs|-1", Start + Agent + "  curl_easy_setopt(curl, CURLOPT_MAXREDIRS, -1L);\n" + After)]
    [DataRow("--post301", Before + "  curl_easy_setopt(curl, CURLOPT_POSTREDIR, 1L);\n" + After)]
    [DataRow("--post302", Before + "  curl_easy_setopt(curl, CURLOPT_POSTREDIR, 2L);\n" + After)]
    [DataRow("--post303", Before + "  curl_easy_setopt(curl, CURLOPT_POSTREDIR, 4L);\n" + After)]
    [DataRow("--post301|--post303", Before + "  curl_easy_setopt(curl, CURLOPT_POSTREDIR, 5L);\n" + After)]
    [DataRow("--tr-encoding", Before + "  curl_easy_setopt(curl, CURLOPT_TRANSFER_ENCODING, 1L);\n" + After)]
    [DataRow("--ignore-content-length", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_IGNORE_CONTENT_LENGTH, 1L);\n" + KeepAlive)]
    [DataRow("--path-as-is", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_PATH_AS_IS, 1L);\n" + KeepAlive)]
    [DataRow("--http0.9", Before + "  curl_easy_setopt(curl, CURLOPT_HTTP09_ALLOWED, 1L);\n" + After)]
    [DataRow("--http1.0", Before + "  curl_easy_setopt(curl, CURLOPT_HTTP_VERSION, (long)CURL_HTTP_VERSION_1_0);\n" + After)]
    [DataRow("-0", Before + "  curl_easy_setopt(curl, CURLOPT_HTTP_VERSION, (long)CURL_HTTP_VERSION_1_0);\n" + After)]
    [DataRow("--http1.1", Before + "  curl_easy_setopt(curl, CURLOPT_HTTP_VERSION, (long)CURL_HTTP_VERSION_1_1);\n" + After)]
    [DataRow("--crlf", Before + Tls + "  curl_easy_setopt(curl, CURLOPT_CRLF, 1L);\n" + KeepAlive)]
    [DataRow("-w|x", Before + "  curl_easy_setopt(curl, CURLOPT_CERTINFO, 1L);\n" + After)]
    [DataRow("-w|%{http_code}", Before + "  curl_easy_setopt(curl, CURLOPT_CERTINFO, 1L);\n" + After)]
    public void Generate_TransferOption_WritesCurlsLines(string arguments, string transfer) =>
        Assert.AreEqual(transfer, TransferLinesFor(arguments + "|" + HttpUrl));

    [TestMethod]
    public void Generate_CertificateInfoWithCaAndClientCertificate_SitsBetweenThem() =>
        Assert.AreEqual(
            Before
            + "  curl_easy_setopt(curl, CURLOPT_RESUME_FROM_LARGE, (curl_off_t)5);\n"
            + "  curl_easy_setopt(curl, CURLOPT_CAINFO, \"up.txt\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_CERTINFO, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"c.pem\");\n"
            + After,
            TransferLinesFor("-C|5|-w|x|-E|c.pem|--cacert|up.txt|" + HttpUrl));

    [TestMethod]
    public void Generate_TransferTextAmongTheNetrcLines_SitsBeforeTheLoginOptions() =>
        Assert.AreEqual(
            Start
            + "  curl_easy_setopt(curl, CURLOPT_NETRC, (long)CURL_NETRC_REQUIRED);\n"
            + "  curl_easy_setopt(curl, CURLOPT_NETRC_FILE, \"up.txt\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_TRANSFERTEXT, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_LOGIN_OPTIONS, \"x\");\n"
            + Agent + MaxRedirs + After,
            TransferLinesFor("-B|--login-options|x|-n|--netrc-file|up.txt|" + HttpUrl));

    [TestMethod]
    public void Generate_CookieSessionThroughAProxy_SitsBeforeTheHeaderOption() =>
        Assert.AreEqual(
            "  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 102400L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_URL, \"https://127.0.0.1:1/\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"http://p:1\");\n"
            + Agent + MaxRedirs
            + "  curl_easy_setopt(curl, CURLOPT_HTTP_VERSION, (long)CURL_HTTP_VERSION_1_1);\n"
            + "  curl_easy_setopt(curl, CURLOPT_COOKIE, \"c=d\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_COOKIESESSION, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_HEADEROPT, 1L);\n"
            + After,
            TransferLinesFor("-x|http://p:1|-j|-b|c=d|--http1.1|https://127.0.0.1:1/"));

    [TestMethod]
    public void Generate_SpeedResumeAndConditionOnFtp_FollowThePassiveIpLine() =>
        Assert.AreEqual(
            FtpStart + Agent + PassiveIp
            + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_LIMIT, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_TIME, 20L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_RESUME_FROM_LARGE, (curl_off_t)5);\n"
            + Tls
            + "  curl_easy_setopt(curl, CURLOPT_TIMECONDITION, (long)CURL_TIMECOND_IFUNMODSINCE);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TIMEVALUE_LARGE, (curl_off_t)1577836800);\n"
            + KeepAlive
            + "  curl_easy_setopt(curl, CURLOPT_ABSTRACT_UNIX_SOCKET, \"s\");\n",
            TransferLinesFor("-C|5|--abstract-unix-socket|s|-z|-20200101|-y|20|" + FtpUrl));

    [TestMethod]
    public void Generate_TransferOptionsTogetherOnHttp_WriteEveryLineInCurlsOrder() =>
        Assert.AreEqual(
            Start
            + "  curl_easy_setopt(curl, CURLOPT_REQUEST_TARGET, \"*\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_DIRLISTONLY, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_APPEND, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TRANSFERTEXT, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_RANGE, \"0-5\");\n"
            + "  mime1 = curl_mime_init(curl);\n"
            + "  part1 = curl_mime_addpart(mime1);\n"
            + "  curl_mime_data(part1, \"b\", CURL_ZERO_TERMINATED);\n"
            + "  curl_mime_name(part1, \"a\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_MIMEPOST, mime1);\n"
            + "  curl_easy_setopt(curl, CURLOPT_MIME_OPTIONS, 1L);\n"
            + Agent
            + "  curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 2L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAXREDIRS, 5L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_HTTP_VERSION, (long)CURL_HTTP_VERSION_1_1);\n"
            + "  curl_easy_setopt(curl, CURLOPT_POSTREDIR, 5L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TRANSFER_ENCODING, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_HTTP09_ALLOWED, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_ALTSVC, \"a.txt\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_HSTS, \"h.txt\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_EXPECT_100_TIMEOUT_MS, 2000L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_COOKIESESSION, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_LIMIT, 100L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_TIME, 20L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_CERTINFO, 1L);\n"
            + Tls
            + "  curl_easy_setopt(curl, CURLOPT_PATH_AS_IS, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_CRLF, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TIMECONDITION, (long)CURL_TIMECOND_IFMODSINCE);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TIMEVALUE_LARGE, (curl_off_t)1577836800);\n"
            + "  curl_easy_setopt(curl, CURLOPT_INTERFACE, \"lo\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_DOH_URL, \"https://d/q\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAXFILESIZE_LARGE, (curl_off_t)100);\n"
            + "  curl_easy_setopt(curl, CURLOPT_IGNORE_CONTENT_LENGTH, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_LOCALPORT, 1000L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_LOCALPORTRANGE, 1001L);\n"
            + KeepAlive
            + "  curl_easy_setopt(curl, CURLOPT_TCP_KEEPIDLE, 30L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TCP_KEEPINTVL, 30L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TCP_KEEPCNT, 5L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_UNIX_SOCKET_PATH, \"/s\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS, 300L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_DISALLOW_USERNAME_IN_URL, 1L);\n",
            TransferLinesFor(
                "--disallow-username-in-url|--alt-svc|a.txt|--hsts|h.txt|--interface|lo|--local-port|1000-2000|--doh-url|https://d/q"
                + "|--unix-socket|/s|--keepalive-time|30|--keepalive-cnt|5|-r|0-5|--max-filesize|100"
                + "|--happy-eyeballs-timeout-ms|300|--expect100-timeout|2|-Y|100|-y|20|-z|20200101|-j|--follow|--max-redirs|5"
                + "|--post301|--post303|--tr-encoding|--ignore-content-length|--path-as-is|--http0.9|--request-target|*|--http1.1"
                + "|--crlf|-B|-l|-a|-w|x|--form-escape|-F|a=b|" + HttpUrl));

    private static string TransferLinesFor(string arguments)
    {
        string[] parts = arguments.Split('|');
        CommandLineOptions options = CommandLineParser.Parse(["-s", .. parts], _ => true).Options!;
        string source = LibcurlSourceCode.Generate([(options, parts[^1])]);
        int start = source.IndexOf("  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE", StringComparison.Ordinal);
        int end = source.IndexOf("\n  /* Here is a list", StringComparison.Ordinal);
        return source[start..end];
    }
}
