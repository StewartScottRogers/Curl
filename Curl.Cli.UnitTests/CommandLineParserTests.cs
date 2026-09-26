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
    public void Parse_EmptyCommandLine_RefusesWithTheTryHelpLineAloneAndExit2()
    {
        CommandLineParseResult result = CommandLineParser.Parse([]);

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNotNull(result.Refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        Assert.AreEqual(2, (int)result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { CommandLineRefusal.TryHelpLine }, result.Refusal.StandardErrorLines.ToArray());
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

    // ---- warning lines ------------------------------------------------------------

    [TestMethod]
    [DataRow("-o", "-s")]
    [DataRow("--output", "--output")]
    [DataRow("-o", "--")]
    public void Parse_FlagLikeOutputFile_IsAcceptedWithOneWarning(string option, string fileName)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, fileName, "http://example.com/"]);

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
        CommandLineParseResult result = CommandLineParser.Parse([argument, "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "-s" }, result.Options.OutputFiles.ToArray());
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-s' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_TwoFlagLikeOutputFiles_WarnsTwiceInCommandLineOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "-a", "-o", "-b", "http://example.com/"]);

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
        CommandLineParseResult result = CommandLineParser.Parse(["-o", fileName, "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { fileName }, result.Options.OutputFiles.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_StandardOutputDash_IsAcceptedWithoutWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "-", "http://example.com/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "-" }, result.Options.OutputFiles.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileThenUnknownOption_RefusesAndKeepsTheWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "-s", "--bogus"]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --bogus: is unknown", result.Refusal.StandardErrorLines[0]);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-s' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileAndNoUrl_RefusesAsNoUrlAndKeepsTheWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "-s"]);

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
        CommandLineParseResult result = CommandLineParser.Parse([silentArgument, "-o", "-x", "file:///x"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "-x" }, result.Options.OutputFiles.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileBeforeSilent_KeepsTheWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "-x", "-s", "file:///x"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileAfterSilentThenUnknownOption_RefusesWithoutWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "-o", "-x", "--bogus"]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --bogus: is unknown", result.Refusal.StandardErrorLines[0]);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileBeforeSilentThenUnknownOption_RefusesAndKeepsTheWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "-x", "-s", "--bogus"]);

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
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "-o", "-x", "--no-silent", "file:///x"]);

        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_EmptyCommandLine_IsRefusedWithNoWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse([]);

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
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

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
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

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
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_FlagLikeOutputFileAndMoreOutputFilesThanUrls_WarnsAboutTheFileNameFirst()
    {
        // curl 8.21.0 prints `curl -o -s -o g file:///Z:/nx` as the file-name warning, then the
        // transfer's error, then the output-options warning.
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "-s", "-o", "g", "file:///x"]);

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
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsFalse(result.IsAccepted);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
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
