namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--xattr</c> and its <c>--no-xattr</c> negation: off when
/// absent, and the last spelling wins, as for every negatable flag.
/// </summary>
[TestClass]
public sealed class CommandLineXattrOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    [TestMethod]
    public void Parse_NoXattrOption_DoesNotAskForExtendedAttributes()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ExtendedAttributes);
    }

    [TestMethod]
    public void Parse_Xattr_AsksForExtendedAttributes()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--xattr", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.ExtendedAttributes);
    }

    [TestMethod]
    public void Parse_XattrThenNoXattr_DoesNotAskForExtendedAttributes()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--xattr", "--no-xattr", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ExtendedAttributes);
    }

    [TestMethod]
    public void AiHelp_XattrSection_DoesNotSayItIsNotSupported()
    {
        Assert.IsTrue(CurlAiHelpText.TryGetMarkdown("output", out string markdown));
        int start = markdown.IndexOf("## --xattr", StringComparison.Ordinal);
        int end = markdown.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = end < 0 ? markdown[start..] : markdown[start..end];

        Assert.IsGreaterThanOrEqualTo(0, start);
        Assert.DoesNotContain("Not supported by this build yet", section);
    }
}
