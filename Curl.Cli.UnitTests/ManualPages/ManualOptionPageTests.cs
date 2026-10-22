using System.Text;
using Curl.Cli.ManualPages;

namespace Curl.Cli.UnitTests.ManualPages;

[TestClass]
public sealed class ManualOptionPageTests
{
    private const string Minimal = "Long: verbose\nCategory: important\nAdded: 8.0\nSee-also: silent\nExample: -v $URL\n";

    private const string Full = "Short: v\nLong: verbose\nArg: <x>\nCategory: important global\nAdded: 4.0\nProtocols: HTTP FTP\n"
        + "Experimental: yes\nScope: global\nMulti: single\nRequires: TLS\nMutexed: silent trace nope\n"
        + "See-also:\n  - silent\n  - trace\n  - gone\nExample:\n  - -v $URL\n  - --expand-silent $URL\n";

    [TestMethod]
    public void Write_MissingDocument_ExitsWithTwo()
    {
        ManualPageContext context = Context([]);

        ManualPageExit exit = Assert.ThrowsExactly<ManualPageExit>(() => new ManualOptionPage(context, true).Write("opt.md"));

        Assert.AreEqual(2, exit.ExitCode);
        Assert.AreEqual("could not find ./opt.md\n", context.Error.ToString());
    }

    [TestMethod]
    public void Write_RefusedHeader_ReturnsTheHeadersCode()
    {
        ManualPageContext context = Context(new() { ["./opt.md"] = "no header\n" });

        int code = new ManualOptionPage(context, true).Write("opt.md");

        Assert.AreEqual(2, code);
        Assert.AreEqual("", context.Output.ToString());
    }

    [TestMethod]
    public void Write_MinimalManpage_WritesNamesExampleAddedAndSeeAlso()
    {
        (int code, string output, string error) = Write(Minimal, true);

        Assert.AreEqual(0, code);
        Assert.AreEqual("", error);
        StringAssert.StartsWith(output, ".IP \"\\-\\-verbose\"\n");
        StringAssert.Contains(output, "\nExample:\n.nf\ncurl -v https://example.com\n.fi\n");
        StringAssert.EndsWith(output, "\nAdded in 8.0. See also \\fI\\-\\-silent\\fP.\n");
    }

    [TestMethod]
    public void Write_FullManpage_WritesEveryPartAndWarnsAboutUnknownNames()
    {
        (int code, string output, string error) = Write(Full, true);

        Assert.AreEqual(0, code);
        StringAssert.StartsWith(output, ".IP \"\\-v, \\-\\-verbose <x>\"\n");
        StringAssert.Contains(output, "(HTTP FTP)");
        StringAssert.Contains(output, "this option is experimental");
        StringAssert.Contains(output, "This option is global and does not need to be specified for each use of");
        StringAssert.Contains(output, "is provided several times, the last set value is used.");
        StringAssert.Contains(output, "\nExamples:\n.nf\ncurl -v https://example.com\ncurl --expand-silent https://example.com\n.fi\n");
        StringAssert.EndsWith(
            output,
            "\nFor \\fI\\-\\-verbose\\fP to work, it requires that the underlying libcurl is built to support TLS.\n"
            + "This option is mutually exclusive with \\fI\\-\\-silent\\fP, \\fI\\-\\-trace\\fP and \\fI\\-\\-nope\\fP.\n"
            + "See also \\fI\\-\\-silent\\fP, \\fI\\-\\-trace\\fP and \\fI\\-\\-gone\\fP.\n");
        Assert.AreEqual(
            $"opt.md:{Closing(Full)}:1:WARN: see-also a non-existing option: gone\nWARN: opt.md mutexes a non-existing option: nope\n",
            error);
    }

    [TestMethod]
    public void Write_FullAscii_WritesEveryPartInPlainText()
    {
        (int code, string output, _) = Write(Full, false);

        Assert.AreEqual(0, code);
        Assert.AreEqual(
            "    -v, --verbose <x>\n"
            + "\t    (HTTP FTP) **WARNING**: this option is experimental. Do not use  in\n"
            + "\t    production. Talks.\n\n"
            + "\t    This option is global  and does not need  to be specified for  each\n"
            + "\t    use of --next.  If --verbose  is provided several  times, the  last\n"
            + "\t    set value is used.\n\n"
            + "\t    Examples:\n"
            + "\t     curl -v https://example.com\n"
            + "\t     curl --expand-silent https://example.com\n\n"
            + "\t    For --verbose to work, it  requires that the underlying libcurl  is\n"
            + "\t    built to  support  TLS.  This option  is  mutually  exclusive  with\n"
            + "\t    --silent, --trace  and  --nope.  See  also  --silent,  --trace  and\n"
            + "\t    --gone.\n\n",
            output);
    }

    [TestMethod]
    public void Write_TwoSeeAlsoNames_JoinsThemWithAnd()
    {
        (_, string output, _) = Write(Minimal.Replace("See-also: silent\n", "See-also:\n  - silent\n  - trace\n", StringComparison.Ordinal), true);

        StringAssert.EndsWith(output, "See also \\fI\\-\\-silent\\fP and \\fI\\-\\-trace\\fP.\n");
    }

    [TestMethod]
    public void Write_TwoMutexedNames_JoinsThemWithAnd()
    {
        (_, string output, _) = Write($"{Minimal}Mutexed: silent trace\n", true);

        StringAssert.Contains(output, "This option is mutually exclusive with \\fI\\-\\-silent\\fP and \\fI\\-\\-trace\\fP.\n");
    }

    [TestMethod]
    public void Write_SecondExampleIsFalse_WritesSingularExample()
    {
        (_, string output, _) = Write(Minimal.Replace("Example: -v $URL\n", "Example:\n  - -v $URL\n  - 0\n", StringComparison.Ordinal), true);

        StringAssert.Contains(output, "\nExample:\n.nf\ncurl -v https://example.com\ncurl 0\n.fi\n");
    }

    [TestMethod]
    public void Write_ExampleWithBackslash_DoublesItInManpage()
    {
        (_, string output, _) = Write(Minimal.Replace("-v $URL", "-v a\\b", StringComparison.Ordinal), true);

        StringAssert.Contains(output, "curl -v a\\\\b\n");
    }

    [TestMethod]
    public void Write_UnrecognizedProtocol_ExitsWithTwo()
    {
        ManualPageContext context = IndexedContext($"{Minimal}Protocols: HTTP GOPHER\n");

        ManualPageExit exit = Assert.ThrowsExactly<ManualPageExit>(() => new ManualOptionPage(context, true).Write("opt.md"));

        Assert.AreEqual(2, exit.ExitCode);
        StringAssert.EndsWith(context.Error.ToString(), "ERROR: unrecognized protocol: GOPHER\n");
    }

    [TestMethod]
    [DataRow("Scope: global\n", "global scope option does not have global category")]
    [DataRow("Scope: url\nCategory: global\n", "unrecognized scope: 'url'")]
    [DataRow("Multi: twice\n", "unrecognized Multi: 'twice'")]
    [DataRow("Example: -Z $URL\n", "unknown option in example: -Z")]
    [DataRow("Example: --zz=1 $URL\n", "unknown option in example: '--zz'")]
    public void Write_RefusedField_ReturnsTwoNamingIt(string field, string message)
    {
        string fields = Minimal.Replace("Example: -v $URL\n", "", StringComparison.Ordinal);
        fields = field.Contains("Category", StringComparison.Ordinal) ? fields.Replace("Category: important\n", "", StringComparison.Ordinal) : fields;
        fields += field.StartsWith("Example", StringComparison.Ordinal) ? field : $"{field}Example: -v $URL\n";

        (int code, _, string error) = Write(fields, true);

        Assert.AreEqual(2, code);
        Assert.AreEqual($"opt.md:{Closing(fields)}:1:ERROR: {message}\n", error);
    }

    [TestMethod]
    [DataRow("append", true, "can be used several times in a command line.")]
    [DataRow("boolean", true, "Disable it again with \\-\\-no-verbose.")]
    [DataRow("boolean", false, "Disable it again with --no-verbose.")]
    [DataRow("mutex", true, "multiple times has no extra effect.")]
    [DataRow("per-URL", true, "is associated with a single URL.")]
    public void Write_KnownMulti_AddsItsLine(string multi, bool manpage, string line)
    {
        (int code, string output, _) = Write($"{Minimal}Multi: {multi}\n", manpage);

        Assert.AreEqual(0, code);
        StringAssert.Contains(output, line);
    }

    [TestMethod]
    public void Write_BooleanNamedNo_DisablesWithThePositiveName()
    {
        (_, string output, _) = Write($"{Minimal.Replace("Long: verbose", "Long: no-buffer", StringComparison.Ordinal)}Multi: boolean\n", true);

        StringAssert.Contains(output, "Disable it again with \\-\\-buffer.");
    }

    [TestMethod]
    public void Write_CustomMulti_AddsNoLineAndNoError()
    {
        (int code, string output, string error) = Write($"{Minimal}Multi: custom\n", true);

        Assert.AreEqual(0, code);
        Assert.AreEqual("", error);
        Assert.DoesNotContain("several times", output);
    }

    [TestMethod]
    public void Write_LongAsciiExample_WrapsAtTheMarginWithContinuations()
    {
        string example = "-v https://example.com/" + new string('a', 130);

        (int code, string output, _) = Write(Minimal.Replace("-v $URL", example, StringComparison.Ordinal), false);

        Assert.AreEqual(0, code);
        StringAssert.Contains(output, $"curl {example[..60]}\\");
        StringAssert.Contains(output, $"{example[60..120]}\\");
        StringAssert.Contains(output, example[120..]);
    }

    [TestMethod]
    public void Write_AsciiExampleOverflowingByALoneZero_DropsTheZero()
    {
        string example = ("-v https://example.com/" + new string('a', 60))[..60] + "0";

        (_, string output, _) = Write(Minimal.Replace("-v $URL", example, StringComparison.Ordinal), false);

        StringAssert.Contains(output, $"curl {example[..60]}\\");
        Assert.DoesNotContain($"{example[..60]}0", output);
    }

    private static int Closing(string fields) => Document(fields).Split('\n').ToList().LastIndexOf("---") + 1;

    private static string Document(string fields) => $"---\n{fields}C: Copyright\nSPDX-License-Identifier: curl\n---\nTalks.\n";

    private static (int Code, string Output, string Error) Write(string fields, bool manpage)
    {
        ManualPageContext context = IndexedContext(fields);
        int code = new ManualOptionPage(context, manpage).Write("opt.md");
        return (code, context.Output.ToString(), context.Error.ToString());
    }

    private static ManualPageContext IndexedContext(string fields)
    {
        ManualPageContext context = Context(new()
        {
            ["./opt.md"] = Document(fields),
            ["./verbose.md"] = "---\nShort: v\nLong: verbose\nHelp: Talk\n---\n",
            ["./silent.md"] = "---\nShort: s\nLong: silent\nHelp: Hush\n---\n",
            ["./trace.md"] = "---\nLong: trace\nHelp: Trace\n---\n",
            ["./buffer.md"] = "---\nLong: buffer\nHelp: Buffer\n---\n",
        });
        foreach (string name in new[] { "verbose.md", "silent.md", "trace.md", "buffer.md" })
        {
            context.Index.Add(context, name);
        }

        return context;
    }

    private static ManualPageContext Context(Dictionary<string, string> files) =>
        new(path => files.GetValueOrDefault(path), "2026-10-21", new StringBuilder(), new StringBuilder());
}
