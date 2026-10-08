using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="CurlHelpText"/> to curl 8.21.0's own help pages. Every file under
/// <c>HelpReference</c> (embedded in this assembly) is what the mingw reference build (<c>/mingw64/bin/curl</c>) printed to a
/// redirected standard output on 2026-09-27, with its CR LF line ends made LF: <c>help.txt</c> is
/// <c>curl -h</c>, <c>help-&lt;subject&gt;.txt</c> is <c>curl --help &lt;subject&gt;</c>, and a
/// <c>-&lt;n&gt;-columns</c> file was printed with <c>COLUMNS=&lt;n&gt;</c> set. The rest run at the
/// default 79 columns curl uses when nothing gives a width.
/// </summary>
[TestClass]
public sealed class CurlHelpTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(null, "help.txt")]
    [DataRow("all", "help-all.txt")]
    [DataRow("category", "help-category.txt")]
    [DataRow("bogus", "help-bogus.txt")]
    [DataRow("auth", "help-auth.txt")]
    [DataRow("connection", "help-connection.txt")]
    [DataRow("curl", "help-curl.txt")]
    [DataRow("deprecated", "help-deprecated.txt")]
    [DataRow("dns", "help-dns.txt")]
    [DataRow("file", "help-file.txt")]
    [DataRow("ftp", "help-ftp.txt")]
    [DataRow("global", "help-global.txt")]
    [DataRow("http", "help-http.txt")]
    [DataRow("imap", "help-imap.txt")]
    [DataRow("ldap", "help-ldap.txt")]
    [DataRow("output", "help-output.txt")]
    [DataRow("pop3", "help-pop3.txt")]
    [DataRow("post", "help-post.txt")]
    [DataRow("proxy", "help-proxy.txt")]
    [DataRow("scp", "help-scp.txt")]
    [DataRow("sftp", "help-sftp.txt")]
    [DataRow("smtp", "help-smtp.txt")]
    [DataRow("ssh", "help-ssh.txt")]
    [DataRow("telnet", "help-telnet.txt")]
    [DataRow("tftp", "help-tftp.txt")]
    [DataRow("timeout", "help-timeout.txt")]
    [DataRow("tls", "help-tls.txt")]
    [DataRow("upload", "help-upload.txt")]
    [DataRow("verbose", "help-verbose.txt")]
    public void Lines_DefaultColumns_MatchCurl(string? subject, string referenceFile)
    {
        IReadOnlyList<string> lines = Lines(subject, CurlHelpText.DefaultColumns);

        AssertReferenceLines(referenceFile, lines);
        CollectionAssert.AreEqual(ReferenceLines(referenceFile), lines.ToArray());
    }

    [TestMethod]
    [DataRow(null, 21, "help-21-columns.txt")]
    [DataRow(null, 40, "help-40-columns.txt")]
    [DataRow(null, 200, "help-200-columns.txt")]
    [DataRow("all", 40, "help-all-40-columns.txt")]
    [DataRow("all", 200, "help-all-200-columns.txt")]
    public void Lines_OtherColumns_MatchCurl(string? subject, int columns, string referenceFile)
    {
        IReadOnlyList<string> lines = Lines(subject, columns);

        AssertReferenceLines(referenceFile, lines);
        CollectionAssert.AreEqual(ReferenceLines(referenceFile), lines.ToArray());
    }

    [TestMethod]
    public void Lines_Usage_JoinedWithCrLf_IsTheMeasuredByteCount()
    {
        // curl -h > file on Windows wrote 1290 bytes: 23 lines, each ended with CR LF.
        IReadOnlyList<string> lines = Lines(null, CurlHelpText.DefaultColumns);

        int length = string.Concat(lines.Select(line => line + "\r\n")).Length;
        Diagnostics.Assert("length joined with CR LF", 1290, length);
        Assert.AreEqual(1290, string.Concat(lines.Select(line => line + "\r\n")).Length);
    }

    [TestMethod]
    [DataRow("ALL", "help-all.txt")]
    [DataRow("Http", "help-http.txt")]
    [DataRow("CATEGORY", "help-category.txt")]
    [DataRow("", "help.txt")]
    [DataRow("http://example.invalid", "help-bogus.txt")]
    public void Lines_SubjectInAnyCaseOrEmpty_MatchesCurl(string subject, string referenceFile)
    {
        IReadOnlyList<string> lines = Lines(subject, CurlHelpText.DefaultColumns);

        AssertReferenceLines(referenceFile, lines);
        CollectionAssert.AreEqual(ReferenceLines(referenceFile), lines.ToArray());
    }

    [TestMethod]
    public void Lines_OneColumn_GivesEveryOptionUnpaddedAndEveryCategoryOnItsOwnLine()
    {
        IReadOnlyList<string> lines = Lines(null, 1);

        Diagnostics.Assert("second line", " -d, --data <data>  HTTP POST data", lines[1]);
        Assert.AreEqual(" -d, --data <data>  HTTP POST data", lines[1]);
        int listStart = lines.ToList().IndexOf("Use \"--help category\" to get an overview of all categories, which are:") + 1;
        Diagnostics.Act("category list start", listStart);
        Diagnostics.Assert("line at list start", "\"\"", "\"" + lines[listStart] + "\"");
        Diagnostics.Assert("first category line", "\"auth, \"", "\"" + lines[listStart + 1] + "\"");
        Diagnostics.Assert("twenty-fourth category line", "\"upload, \"", "\"" + lines[listStart + 24] + "\"");
        Diagnostics.Assert("last category line", "verbose.", lines[listStart + 25]);
        Assert.AreEqual(string.Empty, lines[listStart]);
        Assert.AreEqual("auth, ", lines[listStart + 1]);
        Assert.AreEqual("upload, ", lines[listStart + 24]);
        Assert.AreEqual("verbose.", lines[listStart + 25]);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("--verbose")]
    [DataRow("--bogus")]
    [DataRow("-")]
    public void Lines_OptionSubject_Throws(string subject)
    {
        Diagnostics.Arrange("subject", "\"" + subject + "\"");
        Diagnostics.Act("is option subject", CurlHelpText.IsOptionSubject(subject));
        Diagnostics.Assert("is option subject", true, CurlHelpText.IsOptionSubject(subject));
        Assert.IsTrue(CurlHelpText.IsOptionSubject(subject));
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => CurlHelpText.Lines(subject, CurlHelpText.DefaultColumns));
        Diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("all")]
    public void IsOptionSubject_NoLeadingDash_IsFalse(string? subject)
    {
        Diagnostics.Arrange("subject", subject is null ? "null" : "\"" + subject + "\"");
        bool isOptionSubject = CurlHelpText.IsOptionSubject(subject);
        Diagnostics.Act("is option subject", isOptionSubject);

        Diagnostics.Assert("is option subject", false, isOptionSubject);
        Assert.IsFalse(CurlHelpText.IsOptionSubject(subject));
    }

    [TestMethod]
    public void Lines_ZeroColumns_Throws()
    {
        Diagnostics.Arrange("columns", 0);
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CurlHelpText.Lines(null, 0));
        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    private static string[] ReferenceLines(string referenceFile)
    {
        using Stream stream = typeof(CurlHelpTextTests).Assembly.GetManifestResourceStream("HelpReference." + referenceFile)!;
        using StreamReader reader = new(stream);
        string text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        return text.Split('\n')[..^1];
    }

    /// <summary>Returns <see cref="CurlHelpText.Lines"/>, writing the subject, the columns and the lines as diagnostics.</summary>
    private IReadOnlyList<string> Lines(string? subject, int columns)
    {
        Diagnostics.Arrange("subject", subject is null ? "null" : "\"" + subject + "\"");
        Diagnostics.Arrange("columns", columns);
        IReadOnlyList<string> lines = CurlHelpText.Lines(subject, columns);
        Diagnostics.Act("line count", lines.Count);
        Diagnostics.Act("lines", CommandLineParseDiagnostics.QuoteEach(lines));
        return lines;
    }

    private void AssertReferenceLines(string referenceFile, IReadOnlyList<string> lines)
    {
        Diagnostics.Arrange("reference file", referenceFile);
        Diagnostics.Assert("lines", CommandLineParseDiagnostics.QuoteEach(ReferenceLines(referenceFile)), CommandLineParseDiagnostics.QuoteEach(lines));
    }
}
