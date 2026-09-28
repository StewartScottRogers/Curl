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
        IReadOnlyList<string> lines = CurlHelpText.Lines(subject, CurlHelpText.DefaultColumns);

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
        IReadOnlyList<string> lines = CurlHelpText.Lines(subject, columns);

        CollectionAssert.AreEqual(ReferenceLines(referenceFile), lines.ToArray());
    }

    [TestMethod]
    public void Lines_Usage_JoinedWithCrLf_IsTheMeasuredByteCount()
    {
        // curl -h > file on Windows wrote 1290 bytes: 23 lines, each ended with CR LF.
        IReadOnlyList<string> lines = CurlHelpText.Lines(null, CurlHelpText.DefaultColumns);

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
        IReadOnlyList<string> lines = CurlHelpText.Lines(subject, CurlHelpText.DefaultColumns);

        CollectionAssert.AreEqual(ReferenceLines(referenceFile), lines.ToArray());
    }

    [TestMethod]
    public void Lines_OneColumn_GivesEveryOptionUnpaddedAndEveryCategoryOnItsOwnLine()
    {
        IReadOnlyList<string> lines = CurlHelpText.Lines(null, 1);

        Assert.AreEqual(" -d, --data <data>  HTTP POST data", lines[1]);
        int listStart = lines.ToList().IndexOf("Use \"--help category\" to get an overview of all categories, which are:") + 1;
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
        Assert.IsTrue(CurlHelpText.IsOptionSubject(subject));
        Assert.ThrowsExactly<ArgumentException>(() => CurlHelpText.Lines(subject, CurlHelpText.DefaultColumns));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("all")]
    public void IsOptionSubject_NoLeadingDash_IsFalse(string? subject)
    {
        Assert.IsFalse(CurlHelpText.IsOptionSubject(subject));
    }

    [TestMethod]
    public void Lines_ZeroColumns_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CurlHelpText.Lines(null, 0));
    }

    private static string[] ReferenceLines(string referenceFile)
    {
        using Stream stream = typeof(CurlHelpTextTests).Assembly.GetManifestResourceStream("HelpReference." + referenceFile)!;
        using StreamReader reader = new(stream);
        string text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        return text.Split('\n')[..^1];
    }
}
