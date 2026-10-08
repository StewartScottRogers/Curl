using Curl.Protocol.Abstractions;
using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // ---- accepted command lines ---------------------------------------------------

    [TestMethod]
    public void Parse_BundledSilentAndShowError_SetsBothFlags()
    {
        CommandLineParseResult result = Parse(["-sS", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsTrue(result.Options.ShowError);
    }

    [TestMethod]
    public void Parse_SeparateLongFlags_SetsBothFlags()
    {
        CommandLineParseResult result = Parse(["--silent", "--show-error", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsTrue(result.Options.ShowError);
    }

    [TestMethod]
    public void Parse_AttachedShortValue_SetsOutputFile()
    {
        CommandLineParseResult result = Parse(["-ofile", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "file" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_SeparateShortValue_SetsOutputFile()
    {
        CommandLineParseResult result = Parse(["-o", "file", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "file" }, result.Options.OutputFiles.ToArray());
        CollectionAssert.AreEqual(new[] { "http://example.com/" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_SeparateLongValue_SetsOutputFile()
    {
        CommandLineParseResult result = Parse(["--output", "file", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "file" }, result.Options.OutputFiles.ToArray());
        CollectionAssert.AreEqual(new[] { "http://example.com/" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_LongValueAfterEquals_SetsOutputFile()
    {
        CommandLineParseResult result = Parse(["--output=file", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "file" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_FlagThenValueInBundle_TakesRestOfBundle()
    {
        CommandLineParseResult result = Parse(["-sofile", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "file" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_ValueThenFlagLetterInBundle_TakesLetterAsValue()
    {
        CommandLineParseResult result = Parse(["-os", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "s" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_ValueLooksLikeOption_TakesItAsValue()
    {
        CommandLineParseResult result = Parse(["-o", "-s", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "-s" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_UrlOptionThenPositional_CollectsInOrder()
    {
        CommandLineParseResult result = Parse(["--url", "a", "b"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "a", "b" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_PositionalThenUrlOption_CollectsInOrder()
    {
        CommandLineParseResult result = Parse(["b", "--url=a"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "b", "a" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_RepeatedOutput_CollectsInOrder()
    {
        CommandLineParseResult result = Parse(["-o", "f", "-o", "g", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "f", "g" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyCommandLine_RefusesWithTheTryHelpLineAloneAndExit2()
    {
        CommandLineParseResult result = Parse([]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Assert.IsNotNull(result.Refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        Assert.AreEqual(2, (int)result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { CommandLineRefusal.TryHelpLine }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_DoubleDash_TreatsRestAsUrls()
    {
        CommandLineParseResult result = Parse(["-s", "--", "-o", "-", "--"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsEmpty(result.Options.OutputFiles);
        CollectionAssert.AreEqual(new[] { "-o", "-", "--" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_FlagWithAttachedValue_IgnoresValue()
    {
        CommandLineParseResult result = Parse(["--silent=x", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "http://example.com/" }, result.Options.Urls.ToArray());
    }

    // ---- refusals -----------------------------------------------------------------

    [TestMethod]
    public void Parse_UnknownLongOption_Refuses()
    {
        CommandLineParseResult result = Parse(["--bogus"]);

        AssertRefused(result, "curl: option --bogus: is unknown");
    }

    [TestMethod]
    public void Parse_UnknownLongOptionWithEquals_NamesWholeArgument()
    {
        CommandLineParseResult result = Parse(["--bogus=x"]);

        AssertRefused(result, "curl: option --bogus=x: is unknown");
    }

    [TestMethod]
    [DataRow("--sil")]
    [DataRow("--Silent")]
    public void Parse_AbbreviatedOrMiscasedLongOption_Refuses(string argument)
    {
        CommandLineParseResult result = Parse([argument]);

        AssertRefused(result, $"curl: option {argument}: is unknown");
    }

    [TestMethod]
    public void Parse_UnknownShortOption_Refuses()
    {
        CommandLineParseResult result = Parse(["-!"]);

        AssertRefused(result, "curl: option -!: is unknown");
    }

    [TestMethod]
    [DataRow("-s!x")]
    [DataRow("-!s")]
    public void Parse_UnknownLetterInBundle_NamesWholeArgument(string argument)
    {
        CommandLineParseResult result = Parse([argument]);

        AssertRefused(result, $"curl: option {argument}: is unknown");
    }

    [TestMethod]
    public void Parse_LoneDash_RefusesAsUnknown()
    {
        CommandLineParseResult result = Parse(["-"]);

        AssertRefused(result, "curl: option -: is unknown");
    }

    [TestMethod]
    public void Parse_ShortValueLast_RequiresParameter()
    {
        CommandLineParseResult result = Parse(["-o"]);

        AssertRefused(result, "curl: option -o: requires parameter");
    }

    [TestMethod]
    public void Parse_LongValueLast_RequiresParameter()
    {
        CommandLineParseResult result = Parse(["--output"]);

        AssertRefused(result, "curl: option --output: requires parameter");
    }

    [TestMethod]
    public void Parse_BundledValueLast_NamesWholeBundle()
    {
        CommandLineParseResult result = Parse(["-so"]);

        AssertRefused(result, "curl: option -so: requires parameter");
    }

    [TestMethod]
    public void Parse_UrlOptionLast_RequiresParameter()
    {
        CommandLineParseResult result = Parse(["--url"]);

        AssertRefused(result, "curl: option --url: requires parameter");
    }

    [TestMethod]
    public void Parse_BlankSeparateShortValue_Refuses()
    {
        CommandLineParseResult result = Parse(["-o", ""]);

        AssertRefused(result, "curl: option -o: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankLongValueAfterEquals_Refuses()
    {
        CommandLineParseResult result = Parse(["--output="]);

        AssertRefused(result, "curl: option --output=: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankSeparateUrlValue_Refuses()
    {
        CommandLineParseResult result = Parse(["--url", ""]);

        AssertRefused(result, "curl: option --url: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankValueAfterBundle_RefusesNamingWholeBundle()
    {
        CommandLineParseResult result = Parse(["-so", ""]);

        AssertRefused(result, "curl: option -so: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankSeparateLongOutputValue_Refuses()
    {
        CommandLineParseResult result = Parse(["--output", ""]);

        AssertRefused(result, "curl: option --output: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankPositional_Refuses()
    {
        CommandLineParseResult result = Parse([""]);

        AssertRefused(result, "curl: option : blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_BlankPositionalAfterDoubleDash_Refuses()
    {
        CommandLineParseResult result = Parse(["--", ""]);

        AssertRefused(result, "curl: option : blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_NullElement_RefusesAsBlank()
    {
        CommandLineParseResult result = Parse([null!]);

        AssertRefused(result, "curl: option : blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_NullList_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("arguments", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineParser.Parse(null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void Parse_NullPathExists_ThrowsArgumentNull()
    {
        Diagnostics.ArrangeArguments(["http://example.com/"]);
        Diagnostics.Arrange("path exists", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineParser.Parse(["http://example.com/"], null!));

        Diagnostics.Act("exception parameter", exception.ParamName);
        Diagnostics.Assert("exception parameter", "pathExists", exception.ParamName);
        Assert.AreEqual("pathExists", exception.ParamName);
    }

    [TestMethod]
    public void Parse_SeveralRefusableArguments_RefusesTheFirst()
    {
        CommandLineParseResult result = Parse(["-sS", "--bogus", "-o"]);

        AssertRefused(result, "curl: option --bogus: is unknown");
    }

    [TestMethod]
    [DataRow(new[] { "-s" })]
    [DataRow(new[] { "--" })]
    [DataRow(new[] { "-o", "file" })]
    public void Parse_ArgumentsButNoUrl_RefusesAsNoUrlSpecified(string[] arguments)
    {
        CommandLineParseResult result = Parse(arguments);

        AssertRefused(result, "curl: (2) no URL specified");
    }

    [TestMethod]
    public void Parse_UnknownOptionAndNoUrl_RefusesAsUnknownNotAsNoUrl()
    {
        CommandLineParseResult result = Parse(["--bogus"]);

        AssertRefused(result, "curl: option --bogus: is unknown");
    }

    [TestMethod]
    public void Parse_FlagAndUrl_IsAccepted()
    {
        CommandLineParseResult result = Parse(["-s", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        CollectionAssert.AreEqual(new[] { "http://example.com/" }, result.Options.Urls.ToArray());
    }

    // ---- warning lines ------------------------------------------------------------

    [TestMethod]
    [DataRow("-o", "-s")]
    [DataRow("--output", "--output")]
    [DataRow("-o", "--")]
    public void Parse_FlagLikeOutputFile_IsAcceptedWithOneWarning(string option, string fileName)
    {
        CommandLineParseResult result = Parse([option, fileName, "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { fileName }, result.Options.OutputFiles.ToArray());
        CollectionAssert.AreEqual(new[] { "http://example.com/" }, result.Options.Urls.ToArray());
        CollectionAssert.AreEqual(
            new[] { $"Warning: The filename argument '{fileName}' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("-o-s")]
    [DataRow("--output=-s")]
    public void Parse_AttachedFlagLikeOutputFile_IsAcceptedWithOneWarning(string argument)
    {
        CommandLineParseResult result = Parse([argument, "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "-s" }, result.Options.OutputFiles.ToArray());
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-s' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_TwoFlagLikeOutputFiles_WarnsTwiceInCommandLineOrder()
    {
        CommandLineParseResult result = Parse(["-o", "-a", "-o", "-b", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "-a", "-b" }, result.Options.OutputFiles.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: The filename argument '-a' looks like a flag.",
                "Warning: The filename argument '-b' looks like a flag.",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("file")]
    [DataRow("a-b")]
    public void Parse_OrdinaryOutputFile_CarriesNoWarning(string fileName)
    {
        CommandLineParseResult result = Parse(["-o", fileName, "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { fileName }, result.Options.OutputFiles.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_StandardOutputDash_IsAcceptedWithoutWarning()
    {
        CommandLineParseResult result = Parse(["-o", "-", "http://example.com/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "-" }, result.Options.OutputFiles.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileThenUnknownOption_RefusesAndKeepsTheWarning()
    {
        CommandLineParseResult result = Parse(["-o", "-s", "--bogus"]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --bogus: is unknown", result.Refusal.StandardErrorLines[0]);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-s' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileAndNoUrl_RefusesAsNoUrlAndKeepsTheWarning()
    {
        CommandLineParseResult result = Parse(["-o", "-s"]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: (2) no URL specified", result.Refusal.StandardErrorLines[0]);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-s' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("-sS")]
    public void Parse_FlagLikeOutputFileAfterSilent_IsAcceptedWithoutWarning(string silentArgument)
    {
        CommandLineParseResult result = Parse([silentArgument, "-o", "-x", "file:///x"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "-x" }, result.Options.OutputFiles.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileBeforeSilent_KeepsTheWarning()
    {
        CommandLineParseResult result = Parse(["-o", "-x", "-s", "file:///x"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileAfterSilentThenUnknownOption_RefusesWithoutWarning()
    {
        CommandLineParseResult result = Parse(["-s", "-o", "-x", "--bogus"]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --bogus: is unknown", result.Refusal.StandardErrorLines[0]);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileBeforeSilentThenUnknownOption_RefusesAndKeepsTheWarning()
    {
        CommandLineParseResult result = Parse(["-o", "-x", "-s", "--bogus"]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --bogus: is unknown", result.Refusal.StandardErrorLines[0]);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileWhileSilentThenNoSilent_StaysWithoutWarning()
    {
        // --no-silent turns silence off, but the warning raised while -s was in effect stays
        // dropped, as curl 8.21.0 drops it.
        CommandLineParseResult result = Parse(["-s", "-o", "-x", "--no-silent", "file:///x"]);

        Diagnostics.Assert("warning lines", 0, result.WarningLines.Count);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_EmptyCommandLine_IsRefusedWithNoWarning()
    {
        CommandLineParseResult result = Parse([]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    // ---- warning lines after the transfers -------------------------------------------

    [TestMethod]
    [DataRow(new[] { "-o", "f", "-o", "g", "file:///x" })]
    [DataRow(new[] { "-o", "f", "-o", "g", "-o", "h", "file:///x" })]
    [DataRow(new[] { "-o", "f", "--url", "file:///x", "-o", "g" })]
    [DataRow(new[] { "-s", "--no-silent", "-o", "f", "-o", "g", "file:///x" })]
    public void Parse_MoreOutputFilesThanUrls_IsAcceptedWithOneWarningAfterTheTransfers(string[] arguments)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
        CollectionAssert.AreEqual(
            new[] { "Warning: Got more output options than URLs" },
            result.WarningLinesAfterTransfers.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "-o", "f", "file:///x" })]
    [DataRow(new[] { "-o", "f", "file:///x", "file:///y" })]
    [DataRow(new[] { "file:///x" })]
    public void Parse_NoMoreOutputFilesThanUrls_CarriesNoWarningAfterTheTransfers(string[] arguments)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    [DataRow(new[] { "-s", "-o", "f", "-o", "g", "file:///x" })]
    [DataRow(new[] { "-o", "f", "-o", "g", "file:///x", "-s" })]
    [DataRow(new[] { "-sS", "-o", "f", "-o", "g", "file:///x" })]
    public void Parse_MoreOutputFilesThanUrlsWhileSilentAtTheEnd_CarriesNoWarningAfterTheTransfers(string[] arguments)
    {
        // Unlike a warning raised while reading, curl 8.21.0 checks -s when it prints this one,
        // after the transfers, so a -s anywhere on the command line drops it.
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileAndMoreOutputFilesThanUrls_WarnsAboutTheFileNameFirst()
    {
        // curl 8.21.0 prints `curl -o -s -o g file:///Z:/nx` as the file-name warning, then the
        // transfer's error, then the output-options warning.
        CommandLineParseResult result = Parse(["-o", "-s", "-o", "g", "file:///x"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-s' looks like a flag." },
            result.WarningLines.ToArray());
        CollectionAssert.AreEqual(
            new[] { "Warning: Got more output options than URLs" },
            result.WarningLinesAfterTransfers.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "-o", "f", "-o", "g" })]
    [DataRow(new[] { "-o", "f", "-o", "g", "file:///x", "--bogus" })]
    public void Parse_MoreOutputFilesThanUrlsButRefused_CarriesNoWarningAfterTheTransfers(string[] arguments)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        if (result.Options is { } options)
        {
            Diagnostics.Act("urls", CommandLineParseDiagnostics.QuoteEach(options.Urls));
            Diagnostics.Act("output files", CommandLineParseDiagnostics.QuoteEach(options.OutputFiles));
        }

        foreach (string line in result.WarningLinesAfterTransfers)
        {
            Diagnostics.Act("warning after transfers", line);
        }

        return result;
    }

    private void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);
        Diagnostics.Assert("first stderr line", expectedFirstLine, CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines[0]);
        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.Options);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
