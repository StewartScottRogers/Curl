using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the table-driven parser reads a command line the way curl 8.21.0 does:
/// short options bundle and a value letter takes the rest of its bundle, long options
/// match exactly and case-sensitively and accept <c>--name=value</c>, the first
/// <c>--</c> ends option parsing, and every refusal names the whole argument as typed,
/// prints the try-help line and exits with <see cref="CurlExitCode.FailedInit"/>.
/// Pure string work; no test here touches the file system or the network.
/// </summary>
[TestClass]
public sealed class CommandLineParserTests
{
    // ---- accepted command lines ---------------------------------------------------

    [TestMethod]
    public void Parse_BundledSilentAndShowError_SetsBothFlags()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-sS", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsTrue(result.Options.ShowError);
    }

    [TestMethod]
    public void Parse_SeparateLongFlags_SetsBothFlags()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--silent", "--show-error", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsTrue(result.Options.ShowError);
    }

    [TestMethod]
    public void Parse_AttachedShortValue_SetsOutputFile()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-ofile", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "file" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_SeparateShortValue_SetsOutputFile()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "file", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "file" }, result.Options.OutputFiles.ToArray());
        CollectionAssert.AreEqual(new[] { "http://example.com/" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_SeparateLongValue_SetsOutputFile()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--output", "file", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "file" }, result.Options.OutputFiles.ToArray());
        CollectionAssert.AreEqual(new[] { "http://example.com/" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_LongValueAfterEquals_SetsOutputFile()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--output=file", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "file" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_FlagThenValueInBundle_TakesRestOfBundle()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-sofile", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "file" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_ValueThenFlagLetterInBundle_TakesLetterAsValue()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-os", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "s" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_ValueLooksLikeOption_TakesItAsValue()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "-s", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "-s" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_UrlOptionThenPositional_CollectsInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--url", "a", "b"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "a", "b" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_PositionalThenUrlOption_CollectsInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["b", "--url=a"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "b", "a" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_RepeatedOutput_CollectsInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "f", "-o", "g", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "f", "g" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_NoArguments_ReturnsDefaults()
    {
        CommandLineParseResult result = CommandLineParser.Parse([]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Refusal);
        Assert.IsEmpty(result.Options.Urls);
        Assert.IsEmpty(result.Options.OutputFiles);
        Assert.IsFalse(result.Options.Silent);
        Assert.IsFalse(result.Options.ShowError);
    }

    [TestMethod]
    public void Parse_DoubleDash_TreatsRestAsUrls()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "--", "-o", "-", "--"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsEmpty(result.Options.OutputFiles);
        CollectionAssert.AreEqual(new[] { "-o", "-", "--" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_FlagWithAttachedValue_IgnoresValue()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--silent=x", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "http://example.com/" }, result.Options.Urls.ToArray());
    }

    // ---- refusals -----------------------------------------------------------------

    [TestMethod]
    public void Parse_UnknownLongOption_Refuses()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--bogus"]);

        AssertRefused(result, "curl: option --bogus: is unknown");
    }

    [TestMethod]
    public void Parse_UnknownLongOptionWithEquals_NamesWholeArgument()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--bogus=x"]);

        AssertRefused(result, "curl: option --bogus=x: is unknown");
    }

    [TestMethod]
    [DataRow("--sil")]
    [DataRow("--Silent")]
    public void Parse_AbbreviatedOrMiscasedLongOption_Refuses(string argument)
    {
        CommandLineParseResult result = CommandLineParser.Parse([argument]);

        AssertRefused(result, $"curl: option {argument}: is unknown");
    }

    [TestMethod]
    public void Parse_UnknownShortOption_Refuses()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-!"]);

        AssertRefused(result, "curl: option -!: is unknown");
    }

    [TestMethod]
    [DataRow("-s!x")]
    [DataRow("-!s")]
    public void Parse_UnknownLetterInBundle_NamesWholeArgument(string argument)
    {
        CommandLineParseResult result = CommandLineParser.Parse([argument]);

        AssertRefused(result, $"curl: option {argument}: is unknown");
    }

    [TestMethod]
    public void Parse_LoneDash_RefusesAsUnknown()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-"]);

        AssertRefused(result, "curl: option -: is unknown");
    }

    [TestMethod]
    public void Parse_ShortValueLast_RequiresParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o"]);

        AssertRefused(result, "curl: option -o: requires parameter");
    }

    [TestMethod]
    public void Parse_LongValueLast_RequiresParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--output"]);

        AssertRefused(result, "curl: option --output: requires parameter");
    }

    [TestMethod]
    public void Parse_BundledValueLast_NamesWholeBundle()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-so"]);

        AssertRefused(result, "curl: option -so: requires parameter");
    }

    [TestMethod]
    public void Parse_UrlOptionLast_RequiresParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--url"]);

        AssertRefused(result, "curl: option --url: requires parameter");
    }

    [TestMethod]
    public void Parse_BlankSeparateShortValue_Refuses()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", ""]);

        AssertRefused(result, "curl: option -o: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankLongValueAfterEquals_Refuses()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--output="]);

        AssertRefused(result, "curl: option --output=: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankSeparateUrlValue_Refuses()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--url", ""]);

        AssertRefused(result, "curl: option --url: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankValueAfterBundle_RefusesNamingWholeBundle()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-so", ""]);

        AssertRefused(result, "curl: option -so: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankSeparateLongOutputValue_Refuses()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--output", ""]);

        AssertRefused(result, "curl: option --output: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankPositional_Refuses()
    {
        CommandLineParseResult result = CommandLineParser.Parse([""]);

        AssertRefused(result, "curl: option : blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankPositionalAfterDoubleDash_Refuses()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--", ""]);

        AssertRefused(result, "curl: option : blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_NullElement_RefusesAsBlank()
    {
        CommandLineParseResult result = CommandLineParser.Parse([null!]);

        AssertRefused(result, "curl: option : blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_NullList_ThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineParser.Parse(null!));
    }

    [TestMethod]
    public void Parse_NullPathExists_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineParser.Parse(["http://example.com/"], null!));

        Assert.AreEqual("pathExists", exception.ParamName);
    }

    [TestMethod]
    public void Parse_SeveralRefusableArguments_RefusesTheFirst()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-sS", "--bogus", "-o"]);

        AssertRefused(result, "curl: option --bogus: is unknown");
    }

    [TestMethod]
    [DataRow(new[] { "-s" })]
    [DataRow(new[] { "--" })]
    [DataRow(new[] { "-o", "file" })]
    public void Parse_ArgumentsButNoUrl_RefusesAsNoUrlSpecified(string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        AssertRefused(result, "curl: (2) no URL specified");
    }

    [TestMethod]
    public void Parse_UnknownOptionAndNoUrl_RefusesAsUnknownNotAsNoUrl()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--bogus"]);

        AssertRefused(result, "curl: option --bogus: is unknown");
    }

    [TestMethod]
    public void Parse_FlagAndUrl_IsAccepted()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "http://example.com/" }, result.Options.Urls.ToArray());
    }

    private static void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.Options);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
