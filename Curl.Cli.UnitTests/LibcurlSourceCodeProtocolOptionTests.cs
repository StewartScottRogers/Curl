using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins the <c>curl_easy_setopt</c> lines <see cref="LibcurlSourceCode" /> writes for the FTP, SSH, TFTP,
/// telnet, mail, verbose, rate-limit, query and protocol options against curl 8.21.0 (mingw, Schannel),
/// measured on 2026-10-02 with <c>Record-CurlExchange.ps1 -NoServer</c> and <c>--libcurl - -s</c> (BL-1174
/// Notes). Each row is one measured command line; the transfer's lines are compared byte for byte.
/// </summary>
[TestClass]
public sealed class LibcurlSourceCodeProtocolOptionTests
{
    private const string HttpUrl = "http://127.0.0.1:1/";
    private const string FtpUrl = "ftp://127.0.0.1:1/f";
    private const string SftpUrl = "sftp://127.0.0.1:1/f";

    private const string Buffer = "  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 102400L);\n";
    private const string NoProgress = "  curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 1L);\n";
    private const string Agent = "  curl_easy_setopt(curl, CURLOPT_USERAGENT, \"curl/8.21.0\");\n";
    private const string MaxRedirs = "  curl_easy_setopt(curl, CURLOPT_MAXREDIRS, 50L);\n";
    private const string PassiveIp = "  curl_easy_setopt(curl, CURLOPT_FTP_SKIP_PASV_IP, 1L);\n";
    private const string Tls = "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)CURL_SSLVERSION_TLSv1_2);\n";
    private const string KeepAlive = "  curl_easy_setopt(curl, CURLOPT_TCP_KEEPALIVE, 1L);\n";
    private const string Http = Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/\");\n" + NoProgress + Agent + MaxRedirs;
    private const string Ftp = Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"ftp://127.0.0.1:1/f\");\n" + NoProgress + Agent;
    private const string Sftp = Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"sftp://127.0.0.1:1/f\");\n" + NoProgress + Agent;
    private const string After = Tls + KeepAlive;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--ftp-port|-", Ftp + "  curl_easy_setopt(curl, CURLOPT_FTPPORT, \"-\");\n" + PassiveIp + After)]
    [DataRow("--ftp-port|-|--ftp-pasv", Ftp + PassiveIp + After)]
    [DataRow("--ftp-ssl-ccc", Ftp + "  curl_easy_setopt(curl, CURLOPT_FTP_SSL_CCC, (long)CURLFTPSSL_CCC_PASSIVE);\n" + PassiveIp + After)]
    [DataRow("--ftp-ssl-ccc-mode|active", Ftp + "  curl_easy_setopt(curl, CURLOPT_FTP_SSL_CCC, (long)CURLFTPSSL_CCC_ACTIVE);\n" + PassiveIp + After)]
    [DataRow("--ftp-ssl-ccc|--no-ftp-ssl-ccc", Ftp + PassiveIp + After)]
    [DataRow("--ftp-account|acc", Ftp + "  curl_easy_setopt(curl, CURLOPT_FTP_ACCOUNT, \"acc\");\n" + PassiveIp + After)]
    [DataRow("--no-ftp-skip-pasv-ip|--no-epsv|--no-eprt", Ftp + After)]
    [DataRow("--disable-epsv|--disable-eprt|--ftp-skip-pasv-ip|--epsv|--eprt", Ftp + PassiveIp + After)]
    [DataRow("--ftp-method|nocwd", Ftp + PassiveIp + "  curl_easy_setopt(curl, CURLOPT_FTP_FILEMETHOD, 2L);\n" + After)]
    [DataRow("--ftp-method|singlecwd", Ftp + PassiveIp + "  curl_easy_setopt(curl, CURLOPT_FTP_FILEMETHOD, 3L);\n" + After)]
    [DataRow("--ftp-method|multicwd", Ftp + PassiveIp + "  curl_easy_setopt(curl, CURLOPT_FTP_FILEMETHOD, 1L);\n" + After)]
    [DataRow("--ftp-alternative-to-user|alt", Ftp + PassiveIp + "  curl_easy_setopt(curl, CURLOPT_FTP_ALTERNATIVE_TO_USER, \"alt\");\n" + After)]
    [DataRow("--ftp-pret", Ftp + PassiveIp + "  curl_easy_setopt(curl, CURLOPT_FTP_USE_PRET, 1L);\n" + After)]
    [DataRow("--ssl", Ftp + PassiveIp + Tls + "  curl_easy_setopt(curl, CURLOPT_USE_SSL, (long)CURLUSESSL_TRY);\n" + KeepAlive)]
    [DataRow("--ftp-ssl", Ftp + PassiveIp + Tls + "  curl_easy_setopt(curl, CURLOPT_USE_SSL, (long)CURLUSESSL_TRY);\n" + KeepAlive)]
    [DataRow("--ssl-reqd", Ftp + PassiveIp + Tls + "  curl_easy_setopt(curl, CURLOPT_USE_SSL, (long)CURLUSESSL_ALL);\n" + KeepAlive)]
    [DataRow("--ftp-ssl-reqd", Ftp + PassiveIp + Tls + "  curl_easy_setopt(curl, CURLOPT_USE_SSL, (long)CURLUSESSL_ALL);\n" + KeepAlive)]
    [DataRow("--ftp-ssl-control", Ftp + PassiveIp + Tls + "  curl_easy_setopt(curl, CURLOPT_USE_SSL, (long)CURLUSESSL_CONTROL);\n" + KeepAlive)]
    [DataRow("--ssl|--ftp-ssl-control|--ssl-reqd", Ftp + PassiveIp + Tls + "  curl_easy_setopt(curl, CURLOPT_USE_SSL, (long)CURLUSESSL_ALL);\n" + KeepAlive)]
    [DataRow("--ftp-create-dirs", Ftp + PassiveIp + Tls + "  curl_easy_setopt(curl, CURLOPT_FTP_CREATE_MISSING_DIRS, 2L);\n" + KeepAlive)]
    [DataRow("--limit-rate|100", Ftp + PassiveIp + "  curl_easy_setopt(curl, CURLOPT_MAX_SEND_SPEED_LARGE, (curl_off_t)100);\n  curl_easy_setopt(curl, CURLOPT_MAX_RECV_SPEED_LARGE, (curl_off_t)100);\n" + After)]
    public void Generate_FtpOption_WritesCurlsLines(string arguments, string transfer)
    {
        string expected = arguments.StartsWith("--limit-rate", StringComparison.Ordinal) ? transfer.Replace("102400L", "100L", StringComparison.Ordinal) : transfer;
        string actual = SetoptLinesFor(arguments + "|" + FtpUrl);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("--tftp-blksize|512", "  curl_easy_setopt(curl, CURLOPT_TFTP_BLKSIZE, 512L);\n")]
    [DataRow("--mail-from|a@b", "  curl_easy_setopt(curl, CURLOPT_MAIL_FROM, \"a@b\");\n")]
    [DataRow("--mail-rcpt-allowfails", "  curl_easy_setopt(curl, CURLOPT_MAIL_RCPT_ALLOWFAILS, 1L);\n")]
    [DataRow("--create-file-mode|0600", "  curl_easy_setopt(curl, CURLOPT_NEW_FILE_PERMS, 384L);\n")]
    [DataRow("--create-file-mode|0644", "  curl_easy_setopt(curl, CURLOPT_NEW_FILE_PERMS, 420L);\n")]
    [DataRow("--proto|=http", "  curl_easy_setopt(curl, CURLOPT_PROTOCOLS_STR, \"http\");\n")]
    [DataRow("--proto|=HTTP", "  curl_easy_setopt(curl, CURLOPT_PROTOCOLS_STR, \"http\");\n")]
    [DataRow("--proto|=https,http,ftp", "  curl_easy_setopt(curl, CURLOPT_PROTOCOLS_STR, \"ftp,http,https\");\n")]
    [DataRow("--proto|all", "  curl_easy_setopt(curl, CURLOPT_PROTOCOLS_STR, \"dict,file,ftp,ftps,gopher,gophers,http,https,imap,imaps,ldap,ldaps,mqtt,mqtts,pop3,pop3s,rtsp,scp,sftp,smtp,smtps,telnet,tftp,ws,wss\");\n")]
    [DataRow("--proto-redir|=http", "  curl_easy_setopt(curl, CURLOPT_REDIR_PROTOCOLS_STR, \"http\");\n")]
    [DataRow("--mail-auth|a@b", "  curl_easy_setopt(curl, CURLOPT_MAIL_AUTH, \"a@b\");\n")]
    [DataRow("--proto-default|FtP", "  curl_easy_setopt(curl, CURLOPT_DEFAULT_PROTOCOL, \"FtP\");\n")]
    [DataRow("--tftp-no-options", "  curl_easy_setopt(curl, CURLOPT_TFTP_NO_OPTIONS, 1L);\n")]
    [DataRow("--upload-flags|deleted", "  curl_easy_setopt(curl, CURLOPT_UPLOAD_FLAGS, 18L);\n")]
    [DataRow("--upload-flags|answered", "  curl_easy_setopt(curl, CURLOPT_UPLOAD_FLAGS, 17L);\n")]
    [DataRow("--upload-flags|draft", "  curl_easy_setopt(curl, CURLOPT_UPLOAD_FLAGS, 20L);\n")]
    [DataRow("--upload-flags|-seen", "  curl_easy_setopt(curl, CURLOPT_UPLOAD_FLAGS, 0L);\n")]
    public void Generate_OptionWrittenLast_FollowsTheKeepaliveLines(string arguments, string lines)
    {
        string actual = SetoptLinesFor(arguments + "|" + HttpUrl);

        Diagnostics.Diff("setopt lines", Http + After + lines, actual);
        Assert.AreEqual(Http + After + lines, actual);
    }

    [TestMethod]
    [DataRow("--create-file-mode|0")]
    [DataRow("--upload-flags|seen")]
    [DataRow("--ip-tos|1|--vlan-priority|1|--mptcp|--doh-insecure|--doh-cert-status|--krb|clear|--krb4|clear")]
    [DataRow("--ip-tos|0|--vlan-priority|0")]
    [DataRow("--dns-servers|1.1.1.1|--dns-interface|lo|--dns-ipv4-addr|1.2.3.4|--dns-ipv6-addr|::1|--ech|false|--ssl-sessions|s.txt")]
    [DataRow("--tlsuser|u|--tlspassword|p|--tlsauthtype|SRP|--proxy-tlsuser|u|--proxy-tlspassword|p|--proxy-tlsauthtype|SRP|--knownhosts|k")]
    [DataRow("--log-file|l.txt")]
    [DataRow("--http2")]
    [DataRow("--http2-prior-knowledge")]
    [DataRow("--http3")]
    [DataRow("--http3-only")]
    public void Generate_OptionTheSchannelBuildWritesNothingFor_WritesNoLine(string arguments)
    {
        string actual = SetoptLinesFor(arguments + "|" + HttpUrl);

        Diagnostics.Diff("setopt lines", Http + After, actual);
        Assert.AreEqual(Http + After, actual);
    }

    [TestMethod]
    [DataRow("--limit-rate|1k", "1024L", "1024")]
    [DataRow("--limit-rate|200k", "102400L", "204800")]
    [DataRow("--limit-rate|1G", "102400L", "1073741824")]
    public void Generate_LimitRate_LowersTheBufferAndCapsBothDirections(string arguments, string buffer, string rate)
    {
        string expected = Http.Replace("102400L", buffer, StringComparison.Ordinal)
            + $"  curl_easy_setopt(curl, CURLOPT_MAX_SEND_SPEED_LARGE, (curl_off_t){rate});\n"
            + $"  curl_easy_setopt(curl, CURLOPT_MAX_RECV_SPEED_LARGE, (curl_off_t){rate});\n"
            + After;
        string actual = SetoptLinesFor(arguments + "|" + HttpUrl);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Generate_LimitRateZero_WritesNothing()
    {
        string actual = SetoptLinesFor("--limit-rate|0|" + HttpUrl);

        Diagnostics.Diff("setopt lines", Http + After, actual);
        Assert.AreEqual(Http + After, actual);
    }

    [TestMethod]
    [DataRow("--url-query|a=b|" + HttpUrl, "http://127.0.0.1:1/\\?a=b")]
    [DataRow("--url-query|a=b|--url-query|c=d|http://127.0.0.1:1/?x", "http://127.0.0.1:1/\\?x&a=b&c=d")]
    [DataRow("-G|-d|a=1|--url-query|b=2|" + HttpUrl, "http://127.0.0.1:1/\\?a=1")]
    [DataRow("-G|--url-query|b=2|" + HttpUrl, "http://127.0.0.1:1/\\?b=2")]
    public void Generate_UrlQuery_IsAppendedToTheUrl(string arguments, string url)
    {
        string lines = SetoptLinesFor(arguments);
        string expectedLine = $"  curl_easy_setopt(curl, CURLOPT_URL, \"{url}\");\n";

        Diagnostics.Assert("contains url line", true, lines.Contains(expectedLine, StringComparison.Ordinal));
        Assert.Contains(expectedLine, lines);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("--trace|t.txt")]
    [DataRow("--trace-ascii|t.txt")]
    public void Generate_Verbose_WritesVerboseFirstAndListsTheDebugCallback(string arguments)
    {
        string source = GenerateSource(arguments + "|" + HttpUrl);
        const string VerboseFirst = "  curl = curl_easy_init();\n  curl_easy_setopt(curl, CURLOPT_VERBOSE, 1L);\n" + Buffer;
        const string DebugCallback = "     them yourself.\n\n  CURLOPT_DEBUGFUNCTION was set to a function pointer\n  CURLOPT_DEBUGDATA was set to an object pointer\n  CURLOPT_WRITEDATA";

        Diagnostics.Assert("contains verbose first", true, source.Contains(VerboseFirst, StringComparison.Ordinal));
        Diagnostics.Assert("contains debug callback list", true, source.Contains(DebugCallback, StringComparison.Ordinal));
        Assert.Contains(VerboseFirst, source);
        Assert.Contains(DebugCallback, source);
    }

    [TestMethod]
    public void Generate_SocketCallbacks_AreListedAfterTheStandardOptions()
    {
        const string Expected = "  CURLOPT_STDERR was set to an object pointer\n"
            + "  CURLOPT_OPENSOCKETFUNCTION was set to a function pointer\n"
            + "  CURLOPT_SOCKOPTFUNCTION was set to a function pointer\n"
            + "  CURLOPT_SOCKOPTDATA was set to an object pointer\n\n  */\n";
        string source = GenerateSource("--mptcp|--ip-tos|1|" + HttpUrl);

        Diagnostics.Assert("contains socket callbacks", true, source.Contains(Expected, StringComparison.Ordinal));
        Assert.Contains(Expected, source);
    }

    [TestMethod]
    [DataRow("--vlan-priority|2")]
    [DataRow("--ip-tos|1")]
    public void Generate_SocketOption_ListsTheSocketOptionCallbackAlone(string arguments)
    {
        string source = GenerateSource(arguments + "|" + HttpUrl);

        Diagnostics.Assert("contains socket option callback", true, source.Contains("  CURLOPT_STDERR was set to an object pointer\n  CURLOPT_SOCKOPTFUNCTION", StringComparison.Ordinal));
        Diagnostics.Assert("contains open socket callback", false, source.Contains("OPENSOCKETFUNCTION", StringComparison.Ordinal));
        Assert.Contains("  CURLOPT_STDERR was set to an object pointer\n  CURLOPT_SOCKOPTFUNCTION", source);
        Assert.DoesNotContain("OPENSOCKETFUNCTION", source);
    }

    [TestMethod]
    public void Generate_Parallel_WritesNeitherTheListNorThePerformCall()
    {
        const string Expected = "  curl = curl_easy_init();\n  curl_easy_setopt(curl, CURLOPT_VERBOSE, 1L);\n" + Http + After + "  curl_easy_cleanup(curl);\n";
        string source = GenerateSource("-Z|-v|--mptcp|" + HttpUrl);

        Diagnostics.Assert("contains transfer without list", true, source.Contains(Expected, StringComparison.Ordinal));
        Assert.Contains(Expected, source);
    }

    [TestMethod]
    public void Generate_SshOptionsOnSftp_FollowTheKeyPasswordAndPrecedeTheCertificate()
    {
        string expected =
            Sftp
            + "  curl_easy_setopt(curl, CURLOPT_KEYPASSWD, \"pw\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_PRIVATE_KEYFILE, \"k\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_PUBLIC_KEYFILE, \"p\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_HOST_PUBLIC_KEY_MD5, \"0123456789abcdef0123456789abcdef\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_HOST_PUBLIC_KEY_SHA256, \"abc=\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_COMPRESSION, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"c.pem\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSLKEY, \"k\");\n"
            + After;
        string actual = SetoptLinesFor("--pubkey|p|--hostpubmd5|0123456789abcdef0123456789abcdef|--hostpubsha256|abc=|--compressed-ssh|--pass|pw|-E|c.pem|--key|k|" + SftpUrl);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Generate_SshOptionsOnScp_AreWritten()
    {
        const string Expected = "  curl_easy_setopt(curl, CURLOPT_SSH_HOST_PUBLIC_KEY_MD5, \"0123456789abcdef0123456789abcdef\");\n  curl_easy_setopt(curl, CURLOPT_SSH_COMPRESSION, 1L);\n" + Tls;
        string lines = SetoptLinesFor("--compressed-ssh|--hostpubmd5|0123456789abcdef0123456789abcdef|scp://127.0.0.1:1/f");

        Diagnostics.Assert("contains ssh lines", true, lines.Contains(Expected, StringComparison.Ordinal));
        Assert.Contains(Expected, lines);
    }

    [TestMethod]
    public void Generate_SshOptionsOnHttp_WriteOnlyTheTlsKey()
    {
        string expected = Http + "  curl_easy_setopt(curl, CURLOPT_SSLKEY, \"k\");\n" + After;
        string actual = SetoptLinesFor("--pubkey|p|--hostpubmd5|0123456789abcdef0123456789abcdef|--compressed-ssh|--key|k|" + HttpUrl);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Generate_FtpOptionsTogether_WriteEveryLineInCurlsOrder()
    {
        string expected =
            "  curl_easy_setopt(curl, CURLOPT_VERBOSE, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 1024L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_URL, \"ftp://127.0.0.1:1/f\");\n"
            + NoProgress
            + "  curl_easy_setopt(curl, CURLOPT_DIRLISTONLY, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TRANSFERTEXT, 1L);\n"
            + Agent
            + "  curl_easy_setopt(curl, CURLOPT_FTPPORT, \"-\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_FTP_SSL_CCC, (long)CURLFTPSSL_CCC_ACTIVE);\n"
            + "  curl_easy_setopt(curl, CURLOPT_FTP_ACCOUNT, \"acc\");\n"
            + PassiveIp
            + "  curl_easy_setopt(curl, CURLOPT_FTP_FILEMETHOD, 2L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_FTP_ALTERNATIVE_TO_USER, \"alt\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_FTP_USE_PRET, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_LIMIT, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_LOW_SPEED_TIME, 30L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAX_SEND_SPEED_LARGE, (curl_off_t)1024);\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAX_RECV_SPEED_LARGE, (curl_off_t)1024);\n"
            + "  curl_easy_setopt(curl, CURLOPT_RESUME_FROM_LARGE, (curl_off_t)5);\n"
            + Tls
            + "  curl_easy_setopt(curl, CURLOPT_USE_SSL, (long)CURLUSESSL_ALL);\n"
            + "  curl_easy_setopt(curl, CURLOPT_CRLF, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_QUOTE, slist1);\n"
            + "  curl_easy_setopt(curl, CURLOPT_POSTQUOTE, slist2);\n"
            + "  curl_easy_setopt(curl, CURLOPT_PREQUOTE, slist3);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TIMECONDITION, (long)CURL_TIMECOND_IFMODSINCE);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TIMEVALUE_LARGE, (curl_off_t)1577836800);\n"
            + "  curl_easy_setopt(curl, CURLOPT_INTERFACE, \"lo\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_FTP_CREATE_MISSING_DIRS, 2L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAXFILESIZE_LARGE, (curl_off_t)9);\n"
            + KeepAlive
            + "  curl_easy_setopt(curl, CURLOPT_TCP_KEEPCNT, 3L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TFTP_BLKSIZE, 1024L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAIL_FROM, \"x\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROTOCOLS_STR, \"ftp\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_REDIR_PROTOCOLS_STR, \"http\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_DEFAULT_PROTOCOL, \"ftp\");\n";
        string actual = SetoptLinesFor(
            "--ftp-port|-|--ftp-ssl-ccc-mode|active|--ftp-account|acc|--ftp-alternative-to-user|alt|--ftp-pret|--ftp-method|nocwd"
            + "|--ftp-create-dirs|--ssl-reqd|-Q|a|-Q|-b|-Q|+c|-C|5|-Y|1|--limit-rate|1k|-v|--tftp-blksize|1024|--mail-from|x"
            + "|--interface|lo|-l|-B|--proto|=ftp|--proto-redir|=http|--proto-default|ftp|--crlf|-z|20200101|--max-filesize|9"
            + "|--keepalive-cnt|3|" + FtpUrl);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Generate_QuoteCommands_SplitByPrefixIntoThreeLists()
    {
        string source = GenerateSource("-Q||-Q|-|-Q|*x|-Q|+y|" + FtpUrl);
        const string Expected = "  slist1 = NULL;\n  slist1 = curl_slist_append(slist1, \"\");\n  slist1 = curl_slist_append(slist1, \"*x\");\n"
            + "  slist2 = NULL;\n  slist2 = curl_slist_append(slist2, \"\");\n"
            + "  slist3 = NULL;\n  slist3 = curl_slist_append(slist3, \"y\");\n";

        Diagnostics.Assert("contains three quote lists", true, source.Contains(Expected, StringComparison.Ordinal));
        Assert.Contains(Expected, source);
    }

    [TestMethod]
    public void Generate_OptionsOnHttpTogether_WriteEveryLineInCurlsOrder()
    {
        string expected =
            Buffer
            + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/\");\n"
            + NoProgress
            + "  curl_easy_setopt(curl, CURLOPT_NOBODY, 1L);\n"
            + Agent + MaxRedirs + Tls
            + "  curl_easy_setopt(curl, CURLOPT_SSL_CIPHER_LIST, \"x\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_USE_SSL, (long)CURLUSESSL_TRY);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSL_ENABLE_ALPN, 0L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_PATH_AS_IS, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_FILETIME, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_CRLF, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_QUOTE, slist1);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TIMECONDITION, (long)CURL_TIMECOND_IFMODSINCE);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TIMEVALUE_LARGE, (curl_off_t)1577836800);\n"
            + "  curl_easy_setopt(curl, CURLOPT_CUSTOMREQUEST, \"GET\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_INTERFACE, \"lo\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_TELNETOPTIONS, slist2);\n"
            + "  curl_easy_setopt(curl, CURLOPT_CONNECTTIMEOUT_MS, 5000L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_DOH_URL, \"https://d/q\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_FTP_CREATE_MISSING_DIRS, 2L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAXFILESIZE_LARGE, (curl_off_t)9);\n"
            + "  curl_easy_setopt(curl, CURLOPT_IPRESOLVE, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SERVICE_NAME, \"s\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_IGNORE_CONTENT_LENGTH, 1L);\n"
            + KeepAlive
            + "  curl_easy_setopt(curl, CURLOPT_TCP_KEEPCNT, 3L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_TFTP_BLKSIZE, 1024L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAIL_FROM, \"f\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAIL_RCPT, slist3);\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAIL_RCPT_ALLOWFAILS, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_NEW_FILE_PERMS, 384L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROTOCOLS_STR, \"http\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_REDIR_PROTOCOLS_STR, \"http\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_RESOLVE, slist4);\n"
            + "  curl_easy_setopt(curl, CURLOPT_CONNECT_TO, slist5);\n"
            + "  curl_easy_setopt(curl, CURLOPT_GSSAPI_DELEGATION, 2L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_MAIL_AUTH, \"a\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SASL_AUTHZID, \"z\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SASL_IR, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_UNIX_SOCKET_PATH, \"/s\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_DEFAULT_PROTOCOL, \"http\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_TFTP_NO_OPTIONS, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS, 300L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_DISALLOW_USERNAME_IN_URL, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_UPLOAD_FLAGS, 18L);\n";
        string actual = SetoptLinesFor(
            "--ssl|-I|--path-as-is|--ciphers|x|--no-alpn|--crlf|-Q|q|--telnet-option|A=b|-z|20200101|-X|GET|--interface|lo"
            + "|--ftp-create-dirs|--connect-timeout|5|--doh-url|https://d/q|--max-filesize|9|-4|--service-name|s"
            + "|--ignore-content-length|--keepalive-cnt|3|--tftp-blksize|1024|--mail-from|f|--mail-rcpt|r|--mail-rcpt-allowfails"
            + "|--create-file-mode|0600|--proto|=http|--proto-redir|=http|--resolve|a:1:b|--connect-to|a:1:b:2|--mail-auth|a"
            + "|--delegation|always|--sasl-authzid|z|--sasl-ir|--unix-socket|/s|--proto-default|http|--tftp-no-options"
            + "|--happy-eyeballs-timeout-ms|300|--disallow-username-in-url|--upload-flags|deleted|" + HttpUrl);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    /// <summary>Parses <paramref name="arguments"/> (separated by <c>|</c>, after <c>-s</c>), writing the full argument array first.</summary>
    private CommandLineOptions Parse(string arguments)
    {
        string[] parsedArguments = ["-s", .. arguments.Split('|')];
        Diagnostics.ArrangeArguments(parsedArguments);
        return OpenSslBuildParser.Parse(parsedArguments, _ => true).Options!;
    }

    /// <summary>Generates the libcurl source for <paramref name="arguments"/>, whose last element is the URL, and writes it.</summary>
    private string GenerateSource(string arguments)
    {
        string url = arguments.Split('|')[^1];
        string source = LibcurlSourceCode.Generate([(Parse(arguments), url)]);
        Diagnostics.Act("source", source);
        return source;
    }

    /// <summary>The transfer's <c>curl_easy_setopt</c> lines: from after <c>curl_easy_init</c> to the list of options that cannot be generated.</summary>
    private string SetoptLinesFor(string arguments)
    {
        string source = GenerateSource(arguments);
        const string Init = "  curl = curl_easy_init();\n";
        int start = source.IndexOf(Init, StringComparison.Ordinal) + Init.Length;
        int end = source.IndexOf("\n  /* Here is a list", StringComparison.Ordinal);
        return source[start..end];
    }
}
