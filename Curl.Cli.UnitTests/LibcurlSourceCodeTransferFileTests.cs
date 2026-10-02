namespace Curl.Cli;

/// <summary>
/// Pins the <c>--libcurl</c> lines <see cref="LibcurlSourceCode" /> writes from what the console learns about a
/// transfer's files - <c>-T</c>, <c>--etag-compare</c>, <c>-C -</c> and the SSH known-hosts file - against
/// curl 8.21.0 (mingw, Schannel), measured on 2026-10-02 with <c>Record-CurlExchange.ps1</c> and
/// <c>--libcurl - -s</c> (BL-1177 Notes).
/// </summary>
[TestClass]
public sealed class LibcurlSourceCodeTransferFileTests
{
    private const string HttpUrl = "http://127.0.0.1:1/";
    private const string SftpUrl = "sftp://127.0.0.1:1/f";

    private const string Buffer = "  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 102400L);\n";
    private const string NoProgress = "  curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 1L);\n";
    private const string Upload = "  curl_easy_setopt(curl, CURLOPT_UPLOAD, 1L);\n";
    private const string Agent = "  curl_easy_setopt(curl, CURLOPT_USERAGENT, \"curl/8.21.0\");\n";
    private const string MaxRedirs = "  curl_easy_setopt(curl, CURLOPT_MAXREDIRS, 50L);\n";
    private const string After = "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)CURL_SSLVERSION_TLSv1_2);\n  curl_easy_setopt(curl, CURLOPT_TCP_KEEPALIVE, 1L);\n";
    private const string InFileSize = "  curl_easy_setopt(curl, CURLOPT_INFILESIZE_LARGE, (curl_off_t)5);\n";

    [TestMethod]
    [DataRow("http://127.0.0.1:1/", "http://127.0.0.1:1/up.txt")]
    [DataRow("http://127.0.0.1:1/d/", "http://127.0.0.1:1/d/up.txt")]
    [DataRow("http://127.0.0.1:1", "http://127.0.0.1:1/up.txt")]
    [DataRow("http://127.0.0.1:1/d", "http://127.0.0.1:1/d")]
    [DataRow("127.0.0.1:1", "http://127.0.0.1:1/up.txt")]
    [DataRow("HTTP://127.0.0.1:1/a/../d/", "http://127.0.0.1:1/d/up.txt")]
    public void Generate_UploadFile_AppendsItsNameWritesUploadAndEndsWithItsSize(string url, string written) =>
        Assert.AreEqual(
            Buffer + $"  curl_easy_setopt(curl, CURLOPT_URL, \"{written}\");\n" + NoProgress + Upload + Agent + MaxRedirs + After + InFileSize,
            SetoptLinesFor(new LibcurlTransfer(Parse("-T|up.txt|" + url), url) { UploadFile = "up.txt", UploadFileSize = 5 }));

    [TestMethod]
    [DataRow(null)]
    [DataRow(0L)]
    public void Generate_UploadFileWithoutASize_WritesNoSize(long? size) =>
        Assert.AreEqual(
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/up.txt\");\n" + NoProgress + Upload + Agent + MaxRedirs + After,
            SetoptLinesFor(new LibcurlTransfer(Parse("-T|up.txt|" + HttpUrl), HttpUrl) { UploadFile = "up.txt", UploadFileSize = size }));

    [TestMethod]
    public void Generate_UploadFromStandardInput_KeepsTheUrlAsGiven() =>
        Assert.AreEqual(
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"HTTP://127.0.0.1:1/a/../d/\");\n" + NoProgress + Upload + Agent + MaxRedirs + After,
            SetoptLinesFor(new LibcurlTransfer(Parse("-T|-|HTTP://127.0.0.1:1/a/../d/"), "HTTP://127.0.0.1:1/a/../d/") { UploadFile = "-" }));

    [TestMethod]
    public void Generate_UploadToAMalformedUrl_WritesTheUrlAsGiven() =>
        Assert.Contains(
            "  curl_easy_setopt(curl, CURLOPT_URL, \"http://[::1/\");\n",
            SetoptLinesFor(new LibcurlTransfer(Parse("-T|up.txt|http://[::1/"), "http://[::1/") { UploadFile = "up.txt" }));

    [TestMethod]
    public void Generate_UploadWithOtherOptions_WritesUploadAfterFailOnErrorAndBeforeAppend() =>
        Assert.AreEqual(
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/up.txt\");\n" + NoProgress
            + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"http://127.0.0.1:2\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_FAILONERROR, 1L);\n"
            + Upload
            + "  curl_easy_setopt(curl, CURLOPT_APPEND, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_USERPWD, \"a:b\");\n"
            + Agent + MaxRedirs + After + InFileSize,
            SetoptLinesFor(new LibcurlTransfer(Parse("-T|up.txt|-f|-u|a:b|-x|http://127.0.0.1:2|-a|" + HttpUrl), HttpUrl) { UploadFile = "up.txt", UploadFileSize = 5 }));

    [TestMethod]
    public void Generate_UploadOnImapWithTheLastOptions_EndsWithTheSize() =>
        Assert.AreEqual(
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"imap://127.0.0.1:1/INBOX\");\n" + NoProgress + Upload + Agent + After
            + "  curl_easy_setopt(curl, CURLOPT_RESOLVE, slist1);\n"
            + "  curl_easy_setopt(curl, CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS, 5L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_DISALLOW_USERNAME_IN_URL, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_UPLOAD_FLAGS, 17L);\n"
            + InFileSize,
            SetoptLinesFor(new LibcurlTransfer(
                Parse("-T|up.txt|--resolve|a:1:127.0.0.1|--disallow-username-in-url|--upload-flags|answered|--happy-eyeballs-timeout-ms|5|imap://127.0.0.1:1/INBOX"),
                "imap://127.0.0.1:1/INBOX")
            { UploadFile = "up.txt", UploadFileSize = 5 }));

    [TestMethod]
    [DataRow("If-None-Match: \"abc\"")]
    [DataRow("If-None-Match: \"\"")]
    public void Generate_EtagCompare_AddsItsHeaderToTheHeaderList(string header)
    {
        string source = Generate(new LibcurlTransfer(Parse("--etag-compare|e.txt|" + HttpUrl), HttpUrl) { IfNoneMatchHeaders = [header] });

        Assert.Contains($"  slist1 = curl_slist_append(slist1, {LibcurlSourceCode.QuoteCString(header)});\n", source);
        Assert.Contains(NoProgress + "  curl_easy_setopt(curl, CURLOPT_HTTPHEADER, slist1);\n" + Agent, source);
    }

    [TestMethod]
    public void Generate_EtagCompareWithHeaders_AddsItsHeaderAfterThem() =>
        Assert.Contains(
            "  slist1 = curl_slist_append(slist1, \"X-A: 1\");\n  slist1 = curl_slist_append(slist1, \"If-None-Match: \\\"\\\"\");\n",
            Generate(new LibcurlTransfer(Parse("-H|X-A: 1|--etag-compare|e.txt|" + HttpUrl), HttpUrl) { IfNoneMatchHeaders = ["If-None-Match: \"\""] }));

    [TestMethod]
    public void Generate_ResumeFromTheOutputFile_WritesItsSize() =>
        Assert.AreEqual(
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/\");\n" + NoProgress + Agent + MaxRedirs
            + "  curl_easy_setopt(curl, CURLOPT_RESUME_FROM_LARGE, (curl_off_t)7);\n" + After,
            SetoptLinesFor(new LibcurlTransfer(Parse("-C|-|-o|out.bin|" + HttpUrl), HttpUrl) { OutputFileSize = 7 }));

    [TestMethod]
    [DataRow(null)]
    [DataRow(0L)]
    public void Generate_ResumeFromAMissingOrEmptyOutputFile_WritesNoOffset(long? size) =>
        Assert.AreEqual(
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/\");\n" + NoProgress + Agent + MaxRedirs + After,
            SetoptLinesFor(new LibcurlTransfer(Parse("-C|-|-o|out.bin|" + HttpUrl), HttpUrl) { OutputFileSize = size }));

    [TestMethod]
    public void Generate_ResumeAnUpload_WritesMinusOne() =>
        Assert.AreEqual(
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/up.txt\");\n" + NoProgress + Upload + Agent + MaxRedirs
            + "  curl_easy_setopt(curl, CURLOPT_RESUME_FROM_LARGE, (curl_off_t)-1);\n" + After + InFileSize,
            SetoptLinesFor(new LibcurlTransfer(Parse("-C|-|-T|up.txt|" + HttpUrl), HttpUrl) { UploadFile = "up.txt", UploadFileSize = 5 }));

    [TestMethod]
    public void Generate_SshKnownHostsFile_FollowsTheSshLinesAndPrecedesTheKey() =>
        Assert.AreEqual(
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"sftp://127.0.0.1:1/f\");\n" + NoProgress + Agent
            + "  curl_easy_setopt(curl, CURLOPT_SSH_PRIVATE_KEYFILE, \"k\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_HOST_PUBLIC_KEY_MD5, \"0123456789abcdef0123456789abcdef\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_COMPRESSION, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_KNOWNHOSTS, \"C:\\\\h\\\\.ssh/known_hosts\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSLKEY, \"k\");\n"
            + After,
            SetoptLinesFor(new LibcurlTransfer(Parse("--key|k|--hostpubmd5|0123456789abcdef0123456789abcdef|--compressed-ssh|" + SftpUrl), SftpUrl)
            {
                SshKnownHostsFile = "C:\\h\\.ssh/known_hosts",
            }));

    [TestMethod]
    [DataRow("--hostpubmd5|0123456789abcdef0123456789abcdef|")]
    [DataRow("--hostpubsha256|abc=|")]
    public void Generate_SshKnownHostsMissingWithAFingerprint_GoesOnWithoutTheLine(string fingerprint)
    {
        string source = Generate(new LibcurlTransfer(Parse(fingerprint + SftpUrl), SftpUrl) { SshKnownHostsFileMissing = true });

        Assert.DoesNotContain("KNOWNHOSTS", source);
        Assert.Contains("  result = curl_easy_perform(curl);\n", source);
    }

    [TestMethod]
    public void Generate_SshKnownHostsMissing_StopsBeforeTheKnownHostsLineAndEndsTheTransfers()
    {
        LibcurlTransfer http = new(Parse(HttpUrl), HttpUrl);
        LibcurlTransfer sftp = new(Parse("--key|k|-T|up.txt|--create-file-mode|0600|sftp://127.0.0.1:1/"), "sftp://127.0.0.1:1/")
        {
            UploadFile = "up.txt",
            UploadFileSize = 5,
            SshKnownHostsFileMissing = true,
        };

        string source = LibcurlSourceCode.Generate([http, sftp, http]);

        Assert.EndsWith(
            "  result = curl_easy_perform(curl);\n\n"
            + Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"sftp://127.0.0.1:1/up.txt\");\n" + NoProgress + Upload + Agent
            + "  curl_easy_setopt(curl, CURLOPT_SSH_PRIVATE_KEYFILE, \"k\");\n"
            + "  curl_easy_cleanup(curl);\n  curl = NULL;\n\n  return (int)result;\n}\n/**** End of sample code ****/\n",
            source);
        Assert.AreEqual(1, source.Split("curl_easy_perform").Length - 1);
    }

    [TestMethod]
    public void Generate_NullTransfers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlSourceCode.Generate((IReadOnlyList<LibcurlTransfer>)null!));

    private static CommandLineOptions Parse(string arguments) =>
        CommandLineParser.Parse(["-s", .. arguments.Split('|')], _ => true).Options!;

    private static string Generate(LibcurlTransfer transfer) => LibcurlSourceCode.Generate([transfer]);

    /// <summary>The transfer's <c>curl_easy_setopt</c> lines: from after <c>curl_easy_init</c> to the list of options that cannot be generated.</summary>
    private static string SetoptLinesFor(LibcurlTransfer transfer)
    {
        string source = Generate(transfer);
        const string Init = "  curl = curl_easy_init();\n";
        int start = source.IndexOf(Init, StringComparison.Ordinal) + Init.Length;
        int end = source.IndexOf("\n  /* Here is a list", StringComparison.Ordinal);
        return source[start..end];
    }
}
