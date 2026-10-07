using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--xattr</c> and its <c>--no-xattr</c> negation: off when
/// absent, and the last spelling wins, as for every negatable flag.
/// </summary>
[TestClass]
public sealed class CommandLineXattrOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoXattrOption_DoesNotAskForExtendedAttributes()
    {
        CommandLineParseResult result = Parse([Url]);

        AssertExtendedAttributes(result, false);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ExtendedAttributes);
    }

    [TestMethod]
    public void Parse_Xattr_AsksForExtendedAttributes()
    {
        CommandLineParseResult result = Parse(["--xattr", Url]);

        AssertExtendedAttributes(result, true);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.ExtendedAttributes);
    }

    [TestMethod]
    public void Parse_XattrThenNoXattr_DoesNotAskForExtendedAttributes()
    {
        CommandLineParseResult result = Parse(["--xattr", "--no-xattr", Url]);

        AssertExtendedAttributes(result, false);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ExtendedAttributes);
    }

    [TestMethod]
    public void AiHelp_XattrSection_DoesNotSayItIsNotSupported()
    {
        Diagnostics.Arrange("ai-help category", "output");
        Assert.IsTrue(CurlAiHelpText.TryGetMarkdown("output", out string markdown));
        int start = markdown.IndexOf("## --xattr", StringComparison.Ordinal);
        int end = markdown.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = end < 0 ? markdown[start..] : markdown[start..end];
        Diagnostics.Act("section start", start);
        Diagnostics.Act("section", section);

        Diagnostics.Assert("section found", true, start >= 0);
        Diagnostics.Assert("says not supported", false, section.Contains("Not supported by this build yet", StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, start);
        Assert.DoesNotContain("Not supported by this build yet", section);
    }

    /// <summary>Parses <paramref name="arguments"/>, writing them, the outcome and the extended attributes flag as diagnostics.</summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("extended attributes", result.Options.ExtendedAttributes);
        }

        return result;
    }

    private void AssertExtendedAttributes(CommandLineParseResult result, bool expected)
    {
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("extended attributes", expected, CommandLineParseDiagnostics.Peek(result.Options)?.ExtendedAttributes);
    }
}
