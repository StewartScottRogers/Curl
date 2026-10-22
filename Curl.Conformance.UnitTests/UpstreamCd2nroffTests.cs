namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamCd2nroff"/> against upstream's <c>scripts/cd2nroff</c> (curl 8.22.0): the front matter it
/// reads, each Markdown construct's nroff line, the escapes and the blank lines it keeps, and the pages it writes from
/// a manual folder (BL-3000).
/// </summary>
[TestClass]
public sealed class UpstreamCd2nroffTests
{
    [TestMethod]
    public void Convert_EveryConstruct_WritesTheLinesCd2nroffWrites()
    {
        string[] markdown =
        [
            "---", "c: Copyright", "Title: curl_demo", "Section: 3", "Source: libcurl", "See-also:", "  - curl_b (3)", "  - curl_a (3)",
            "  not an item", "- unindented", "Protocol:", "  - HTTP", "TLS-backend:", "  - OpenSSL", "Added-in: 7.1", "---",
            string.Empty, "# NAME", string.Empty,
            "curl_demo - a **bold** and *italic* `code` it's", "\"quoted\" first", string.Empty,
            ".period first <!-- gone -->", "<!-- only a comment -->", "See curl_easy_setopt(3) and \\<tag\\> and a-b.", " \t",
            "## CURLOPT_X", "~~~c", ".dot \\n", "'apos", "~~~", string.Empty,
            "## \"two words\"", "    indented \\x", "    .more", "back",
            "##", "# %PROTOCOLS%", "# %AVAILABILITY%", "# A", "# \"B", "# \"QUOTED\"",
        ];

        (string FileName, List<string> Lines)? page = UpstreamCd2nroff.Convert(markdown);

        Assert.IsNotNull(page);
        Assert.AreEqual("curl_demo.3", page.Value.FileName);
        string[] expected =
        [
            ".TH curl_demo 3", ".SH NAME",
            "curl_demo \\- a \\fBbold\\fP and \\fIitalic\\fP \\fIcode\\fP it\\(aqs", "\\&\"quoted\" first", string.Empty,
            "\\&.period first ", string.Empty, "See \\fIcurl_easy_setopt(3)\\fP and <tag> and a\\-b.",
            ".IP CURLOPT_X", ".nf", "\\&.dot \\\\n", "\\&'apos", ".fi",
            ".IP \"two words\"", ".nf", "indented \\\\x", "\\&.more", ".fi", string.Empty, "back",
            ".PP", ".SH PROTOCOLS", ".SH AVAILABILITY", "Added in curl 7.1", ".SH A", ".SH \"B", ".SH QUOTED",
            ".SH SEE ALSO", ".BR curl_a (3),", ".BR curl_b (3)",
        ];
        CollectionAssert.AreEqual(expected, page.Value.Lines);
    }

    [TestMethod]
    public void Convert_AvailabilityNotApplicable_WritesNoAvailabilitySection()
    {
        string[] markdown = ["---", "Title: t", "Section: 3", "See-also: curl_x (3)", "Added-in: n/a", "---", "# %AVAILABILITY%", "text"];

        (string FileName, List<string> Lines)? page = UpstreamCd2nroff.Convert(markdown);

        Assert.IsNotNull(page);
        CollectionAssert.AreEqual(new[] { ".TH t 3", "text", ".SH SEE ALSO", ".BR curl_x (3)" }, page.Value.Lines);
    }

    [TestMethod]
    [DataRow("no front matter", "text")]
    [DataRow("---", "Title: t", "Section: 3")]
    [DataRow("---", "Section: 3", "---")]
    [DataRow("---", "Title: t", "---")]
    public void Convert_NoClosedFrontMatterWithTitleAndSection_WritesNoPage(params string[] markdown) =>
        Assert.IsNull(UpstreamCd2nroff.Convert(markdown));

    [TestMethod]
    public void Convert_NullLines_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCd2nroff.Convert(null!));

    [TestMethod]
    public void ConvertFolder_PagesAndOptionPages_KeysThemByTheirBuiltPaths()
    {
        string folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"cd2nroff-{Guid.NewGuid():N}")).FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "opts"));
            File.WriteAllText(Path.Combine(folder, "page.md"), "---\nTitle: curl_page\nSection: 3\n---\ntext\n");
            File.WriteAllText(Path.Combine(folder, "notes.md"), "no front matter\n");
            File.WriteAllText(Path.Combine(folder, "opts", "option.md"), "---\nTitle: CURLOPT_PAGE\nSection: 3\n---\n");

            Dictionary<string, IReadOnlyList<string>> pages = UpstreamCd2nroff.ConvertFolder(folder);

            CollectionAssert.AreEquivalent(new[] { "curl_page.3", "opts/CURLOPT_PAGE.3" }, pages.Keys.ToArray());
            CollectionAssert.AreEqual(new[] { ".TH curl_page 3", "text", ".SH SEE ALSO" }, pages["curl_page.3"].ToArray());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void ConvertFolder_MissingFolder_WritesNoPages() =>
        Assert.IsEmpty(UpstreamCd2nroff.ConvertFolder(Path.Combine(AppContext.BaseDirectory, "log", $"missing-{Guid.NewGuid():N}")));
}
