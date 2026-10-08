using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("http://127.0.0.1:1/", "http://127.0.0.1:1/up.txt")]
    [DataRow("http://127.0.0.1:1/d/", "http://127.0.0.1:1/d/up.txt")]
    [DataRow("http://127.0.0.1:1", "http://127.0.0.1:1/up.txt")]
    [DataRow("http://127.0.0.1:1/d", "http://127.0.0.1:1/d")]
    [DataRow("127.0.0.1:1", "http://127.0.0.1:1/up.txt")]
    [DataRow("HTTP://127.0.0.1:1/a/../d/", "http://127.0.0.1:1/d/up.txt")]
    public void Generate_UploadFile_AppendsItsNameWritesUploadAndEndsWithItsSize(string url, string written)
    {
        string expected = Buffer + $"  curl_easy_setopt(curl, CURLOPT_URL, \"{written}\");\n" + NoProgress + Upload + Agent + MaxRedirs + After + InFileSize;
        LibcurlTransfer transfer = new(Parse("-T|up.txt|" + url), url) { UploadFile = "up.txt", UploadFileSize = 5 };

        string actual = SetoptLinesFor(transfer);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(0L)]
    public void Generate_UploadFileWithoutASize_WritesNoSize(long? size)
    {
        string expected = Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/up.txt\");\n" + NoProgress + Upload + Agent + MaxRedirs + After;
        Diagnostics.Arrange("upload file size", size?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null");
        LibcurlTransfer transfer = new(Parse("-T|up.txt|" + HttpUrl), HttpUrl) { UploadFile = "up.txt", UploadFileSize = size };

        string actual = SetoptLinesFor(transfer);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Generate_UploadFromStandardInput_KeepsTheUrlAsGiven()
    {
        string expected = Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"HTTP://127.0.0.1:1/a/../d/\");\n" + NoProgress + Upload + Agent + MaxRedirs + After;
        LibcurlTransfer transfer = new(Parse("-T|-|HTTP://127.0.0.1:1/a/../d/"), "HTTP://127.0.0.1:1/a/../d/") { UploadFile = "-" };

        string actual = SetoptLinesFor(transfer);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Generate_UploadToAMalformedUrl_WritesTheUrlAsGiven()
    {
        const string Expected = "  curl_easy_setopt(curl, CURLOPT_URL, \"http://[::1/\");\n";
        LibcurlTransfer transfer = new(Parse("-T|up.txt|http://[::1/"), "http://[::1/") { UploadFile = "up.txt" };

        string actual = SetoptLinesFor(transfer);

        Diagnostics.Assert("contains URL line", true, actual.Contains(Expected, StringComparison.Ordinal));
        Assert.Contains(Expected, actual);
    }

    [TestMethod]
    public void Generate_UploadWithOtherOptions_WritesUploadAfterFailOnErrorAndBeforeAppend()
    {
        string expected =
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/up.txt\");\n" + NoProgress
            + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"http://127.0.0.1:2\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_FAILONERROR, 1L);\n"
            + Upload
            + "  curl_easy_setopt(curl, CURLOPT_APPEND, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_USERPWD, \"a:b\");\n"
            + Agent + MaxRedirs + After + InFileSize;
        LibcurlTransfer transfer = new(Parse("-T|up.txt|-f|-u|a:b|-x|http://127.0.0.1:2|-a|" + HttpUrl), HttpUrl) { UploadFile = "up.txt", UploadFileSize = 5 };

        string actual = SetoptLinesFor(transfer);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Generate_UploadOnImapWithTheLastOptions_EndsWithTheSize()
    {
        string expected =
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"imap://127.0.0.1:1/INBOX\");\n" + NoProgress + Upload + Agent + After
            + "  curl_easy_setopt(curl, CURLOPT_RESOLVE, slist1);\n"
            + "  curl_easy_setopt(curl, CURLOPT_HAPPY_EYEBALLS_TIMEOUT_MS, 5L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_DISALLOW_USERNAME_IN_URL, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_UPLOAD_FLAGS, 17L);\n"
            + InFileSize;
        LibcurlTransfer transfer = new(
            Parse("-T|up.txt|--resolve|a:1:127.0.0.1|--disallow-username-in-url|--upload-flags|answered|--happy-eyeballs-timeout-ms|5|imap://127.0.0.1:1/INBOX"),
            "imap://127.0.0.1:1/INBOX")
        { UploadFile = "up.txt", UploadFileSize = 5 };

        string actual = SetoptLinesFor(transfer);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("If-None-Match: \"abc\"")]
    [DataRow("If-None-Match: \"\"")]
    public void Generate_EtagCompare_AddsItsHeaderToTheHeaderList(string header)
    {
        Diagnostics.Arrange("If-None-Match header", header);
        LibcurlTransfer transfer = new(Parse("--etag-compare|e.txt|" + HttpUrl), HttpUrl) { IfNoneMatchHeaders = [header] };

        string source = Generate(transfer);

        string headerLine = $"  slist1 = curl_slist_append(slist1, {LibcurlSourceCode.QuoteCString(header)});\n";
        string listLines = NoProgress + "  curl_easy_setopt(curl, CURLOPT_HTTPHEADER, slist1);\n" + Agent;
        Diagnostics.Assert("contains header line", true, source.Contains(headerLine, StringComparison.Ordinal));
        Diagnostics.Assert("contains header list lines", true, source.Contains(listLines, StringComparison.Ordinal));
        Assert.Contains(headerLine, source);
        Assert.Contains(listLines, source);
    }

    [TestMethod]
    public void Generate_EtagCompareWithHeaders_AddsItsHeaderAfterThem()
    {
        const string Expected = "  slist1 = curl_slist_append(slist1, \"X-A: 1\");\n  slist1 = curl_slist_append(slist1, \"If-None-Match: \\\"\\\"\");\n";
        LibcurlTransfer transfer = new(Parse("-H|X-A: 1|--etag-compare|e.txt|" + HttpUrl), HttpUrl) { IfNoneMatchHeaders = ["If-None-Match: \"\""] };

        string source = Generate(transfer);

        Diagnostics.Assert("contains both header lines in order", true, source.Contains(Expected, StringComparison.Ordinal));
        Assert.Contains(Expected, source);
    }

    [TestMethod]
    public void Generate_ResumeFromTheOutputFile_WritesItsSize()
    {
        string expected =
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/\");\n" + NoProgress + Agent + MaxRedirs
            + "  curl_easy_setopt(curl, CURLOPT_RESUME_FROM_LARGE, (curl_off_t)7);\n" + After;
        LibcurlTransfer transfer = new(Parse("-C|-|-o|out.bin|" + HttpUrl), HttpUrl) { OutputFileSize = 7 };

        string actual = SetoptLinesFor(transfer);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(0L)]
    public void Generate_ResumeFromAMissingOrEmptyOutputFile_WritesNoOffset(long? size)
    {
        string expected = Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/\");\n" + NoProgress + Agent + MaxRedirs + After;
        Diagnostics.Arrange("output file size", size?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null");
        LibcurlTransfer transfer = new(Parse("-C|-|-o|out.bin|" + HttpUrl), HttpUrl) { OutputFileSize = size };

        string actual = SetoptLinesFor(transfer);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Generate_ResumeAnUpload_WritesMinusOne()
    {
        string expected =
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/up.txt\");\n" + NoProgress + Upload + Agent + MaxRedirs
            + "  curl_easy_setopt(curl, CURLOPT_RESUME_FROM_LARGE, (curl_off_t)-1);\n" + After + InFileSize;
        LibcurlTransfer transfer = new(Parse("-C|-|-T|up.txt|" + HttpUrl), HttpUrl) { UploadFile = "up.txt", UploadFileSize = 5 };

        string actual = SetoptLinesFor(transfer);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Generate_SshKnownHostsFile_FollowsTheSshLinesAndPrecedesTheKey()
    {
        string expected =
            Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"sftp://127.0.0.1:1/f\");\n" + NoProgress + Agent
            + "  curl_easy_setopt(curl, CURLOPT_SSH_PRIVATE_KEYFILE, \"k\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_HOST_PUBLIC_KEY_MD5, \"0123456789abcdef0123456789abcdef\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_COMPRESSION, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSH_KNOWNHOSTS, \"C:\\\\h\\\\.ssh/known_hosts\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSLKEY, \"k\");\n"
            + After;
        Diagnostics.Arrange("known hosts file", "C:\\h\\.ssh/known_hosts");
        LibcurlTransfer transfer = new(Parse("--key|k|--hostpubmd5|0123456789abcdef0123456789abcdef|--compressed-ssh|" + SftpUrl), SftpUrl)
        {
            SshKnownHostsFile = "C:\\h\\.ssh/known_hosts",
        };

        string actual = SetoptLinesFor(transfer);

        Diagnostics.Diff("setopt lines", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("--hostpubmd5|0123456789abcdef0123456789abcdef|")]
    [DataRow("--hostpubsha256|abc=|")]
    public void Generate_SshKnownHostsMissingWithAFingerprint_GoesOnWithoutTheLine(string fingerprint)
    {
        Diagnostics.Arrange("known hosts file missing", true);
        LibcurlTransfer transfer = new(Parse(fingerprint + SftpUrl), SftpUrl) { SshKnownHostsFileMissing = true };

        string source = Generate(transfer);

        Diagnostics.Assert("contains KNOWNHOSTS", false, source.Contains("KNOWNHOSTS", StringComparison.Ordinal));
        Diagnostics.Assert("contains perform line", true, source.Contains("  result = curl_easy_perform(curl);\n", StringComparison.Ordinal));
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
        Diagnostics.Act("source", source);

        string expectedEnd =
            "  result = curl_easy_perform(curl);\n\n"
            + Buffer + "  curl_easy_setopt(curl, CURLOPT_URL, \"sftp://127.0.0.1:1/up.txt\");\n" + NoProgress + Upload + Agent
            + "  curl_easy_setopt(curl, CURLOPT_SSH_PRIVATE_KEYFILE, \"k\");\n"
            + "  curl_easy_cleanup(curl);\n  curl = NULL;\n\n  return (int)result;\n}\n/**** End of sample code ****/\n";
        Diagnostics.Assert("source ends with", true, source.EndsWith(expectedEnd, StringComparison.Ordinal));
        Diagnostics.Assert("curl_easy_perform count", 1, source.Split("curl_easy_perform").Length - 1);
        Assert.EndsWith(expectedEnd, source);
        Assert.AreEqual(1, source.Split("curl_easy_perform").Length - 1);
    }

    [TestMethod]
    public void Generate_NullTransfers_Throws()
    {
        Diagnostics.Arrange("transfers", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlSourceCode.Generate((IReadOnlyList<LibcurlTransfer>)null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    /// <summary>Writes the arguments as an <c>ARRANGE</c> line, then parses them (after <c>-s</c>) and returns the options.</summary>
    private CommandLineOptions Parse(string arguments)
    {
        string[] split = ["-s", .. arguments.Split('|')];
        Diagnostics.ArrangeArguments(split);
        return CommandLineParser.Parse(split, _ => true).Options!;
    }

    /// <summary>Generates the source for <paramref name="transfer"/> and writes it as an <c>ACT</c> line.</summary>
    private string Generate(LibcurlTransfer transfer)
    {
        string source = LibcurlSourceCode.Generate([transfer]);
        Diagnostics.Act("source", source);
        return source;
    }

    /// <summary>The transfer's <c>curl_easy_setopt</c> lines: from after <c>curl_easy_init</c> to the list of options that cannot be generated.</summary>
    private string SetoptLinesFor(LibcurlTransfer transfer)
    {
        string source = Generate(transfer);
        const string Init = "  curl = curl_easy_init();\n";
        int start = source.IndexOf(Init, StringComparison.Ordinal) + Init.Length;
        int end = source.IndexOf("\n  /* Here is a list", StringComparison.Ordinal);
        string lines = source[start..end];
        Diagnostics.Act("setopt lines", lines);
        return lines;
    }

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);
}
