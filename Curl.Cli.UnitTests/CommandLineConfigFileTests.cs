using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-K</c> / <c>--config</c>. Measured with the local curl 8.21.0 (mingw, Schannel) on
/// 2026-09-26 in Git Bash with <c>CURL_HOME</c> and <c>HOME</c> pointing at an empty directory and
/// <c>curl -q -K &lt;file&gt; [arguments]</c>, the file written with <c>printf</c>; values that do not
/// show in standard error were read back with <c>-G</c> and <c>-w '%{url_effective}'</c> (or <c>%{method}</c>) against
/// <c>http://127.0.0.1:1/</c>. Each test names the file and the lines curl printed.
/// </summary>
[TestClass]
public sealed class CommandLineConfigFileTests
{
    private const string Url = "http://example.com/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    [TestMethod]
    [DataRow("url = \"http://a/\"")]
    [DataRow("url=http://a/")]
    [DataRow("url: http://a/")]
    [DataRow("url http://a/")]
    [DataRow("url\t=: http://a/")]
    [DataRow("--url http://a/")]
    [DataRow("--url=http://a/")]
    public void Parse_ConfigFileUrlLine_AddsTheUrl(string line)
    {
        CommandLineParseResult result = Parse(["-K", "a.cfg"], ("a.cfg", line + "\n"));

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "http://a/" }, result.Options.Urls.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ConfigFileLines_AreAppliedInPlaceOfTheOption()
    {
        CommandLineParseResult result = Parse(["-o", "first", "--config", "a.cfg", "-o", "last", Url], ("a.cfg", "output = middle\n"));

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "first", "middle", "last" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileShortOptions_BundleAndTakeTheRestOrTheParameter()
    {
        CommandLineParseResult result = Parse(["-K", "a.cfg", Url], ("a.cfg", "-sS\n-ofile\n-o other\n"));

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsTrue(result.Options.ShowError);
        CollectionAssert.AreEqual(new[] { "file", "other" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileNegatedLongName_TurnsTheFlagOff()
    {
        CommandLineParseResult result = Parse(["-s", "-K", "a.cfg", Url], ("a.cfg", "no-silent\n"));

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.Silent);
    }

    [TestMethod]
    public void Parse_ConfigFileCommentsAndBlankLines_AreSkipped()
    {
        CommandLineParseResult result = Parse(["-K", "a.cfg"], ("a.cfg", "\n# comment\n  \t# indented comment\n \t \nurl = http://a/ # trailing comment\n"));

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "http://a/" }, result.Options.Urls.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ConfigFileWithoutFinalLineFeed_ReadsTheLastLine()
    {
        CommandLineParseResult result = Parse(["-K", "a.cfg"], ("a.cfg", "url http://a/"));

        CollectionAssert.AreEqual(new[] { "http://a/" }, result.Options!.Urls.ToArray());
    }

    /// <summary>
    /// <c>printf 'silent\r\nbogus\r\n'</c>: <c>silent</c> is applied (so the unknown-option line is
    /// hidden) and only <c>curl: option -K: found an unknown config option</c> and try-help print.
    /// </summary>
    [TestMethod]
    public void Parse_ConfigFileWithCrLf_ReadsCrLfAsLineEnd()
    {
        CommandLineParseResult result = Parse(["-K", "crlf.cfg"], ("crlf.cfg", "silent\r\nbogus\r\n"));

        CollectionAssert.AreEqual(
            new[] { "curl: option -K: found an unknown config option", TryHelp },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    /// <summary>
    /// <c>data = "a&lt;TAB&gt;b\"c\qd\e"</c> with <c>-G</c> gave the query <c>a&lt;TAB&gt;b"cqde</c>, and
    /// <c>data = "unterminated\</c> gave <c>unterminated</c>.
    /// </summary>
    [TestMethod]
    [DataRow("\"a\tb\\\"c\\qd\\e\"", "a\tb\"cqde")]
    [DataRow("\"unterminated\\", "unterminated")]
    [DataRow("\"unterminated", "unterminated")]
    [DataRow("\"\\t\\n\\r\\v\"", "\t\n\r\v")]
    [DataRow("\"a\" junk", "a")]
    [DataRow("\"a b\"", "a b")]
    public void Parse_ConfigFileQuotedParameter_IsUnescapedToTheClosingQuote(string parameter, string expected)
    {
        CommandLineParseResult result = Parse(["-K", "q.cfg", Url], ("q.cfg", $"request = {parameter}\n"));

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.RequestMethod);
        Assert.IsEmpty(result.WarningLines);
    }

    /// <summary><c>data = ""</c> with <c>-G</c> gave <c>http://127.0.0.1:1/</c>: an empty quoted parameter is a parameter.</summary>
    [TestMethod]
    public void Parse_ConfigFileEmptyQuotedParameter_IsAnEmptyValue()
    {
        CommandLineParseResult result = Parse(["-K", "e.cfg", Url], ("e.cfg", "user-agent = \"\"\n"));

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.UserAgent);
    }

    /// <summary><c>url = x y</c>: the URL is <c>x</c> and curl warns in two lines.</summary>
    [TestMethod]
    public void Parse_ConfigFileUnquotedParameterWithMoreText_WarnsAndTakesTheFirstWord()
    {
        CommandLineParseResult result = Parse(["-K", "ws.cfg"], ("ws.cfg", "url = x y\n"));

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "x" }, result.Options.Urls.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: ws.cfg:1 Option 'url' uses argument with unquoted whitespace. This ",
                "Warning: may cause side-effects. Consider double quotes.",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("url = x \t")]
    [DataRow("url = x\t\r")]
    [DataRow("url = x #y")]
    public void Parse_ConfigFileUnquotedParameterWithOnlyBlanksOrACommentAfter_DoesNotWarn(string line)
    {
        CommandLineParseResult result = Parse(["-K", "a.cfg"], ("a.cfg", line));

        CollectionAssert.AreEqual(new[] { "x" }, result.Options!.Urls.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    /// <summary><c>output 'x'</c>: two warning lines, the file name keeps its quotes, then no URL.</summary>
    [TestMethod]
    public void Parse_ConfigFileParameterInSingleQuotes_WarnsAndKeepsTheQuotes()
    {
        CommandLineParseResult result = Parse(["-K", "sq.cfg", Url], ("sq.cfg", "output 'x'\n"));

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "'x'" }, result.Options.OutputFiles.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: sq.cfg:1 Option 'output' uses argument with leading single quote. It ",
                "Warning: is probably a mistake. Consider double quotes.",
            },
            result.WarningLines.ToArray());
    }

    /// <summary><c>--output : x</c>: an option starting with <c>-</c> takes no <c>:</c> separator, so the file is <c>:</c>.</summary>
    [TestMethod]
    public void Parse_ConfigFileDashedOption_TakesColonAsTheParameter()
    {
        CommandLineParseResult result = Parse(["-K", "sep2.cfg", Url], ("sep2.cfg", "--output : x\n"));

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { ":" }, result.Options.OutputFiles.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: sep2.cfg:1 Option '--output' uses argument with unquoted whitespace. ",
                "Warning: This may cause side-effects. Consider double quotes.",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileWarningAfterSilent_IsDropped()
    {
        CommandLineParseResult result = Parse(["-s", "-K", "ws.cfg"], ("ws.cfg", "url = x y\n"));

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_NestedConfigFile_IsApplied()
    {
        CommandLineParseResult result = Parse(["-K", "outer.cfg"], ("outer.cfg", "-K inner.cfg\n"), ("inner.cfg", "url = \"http://127.0.0.1:1/in\"\n"));

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:1/in" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileDash_ReadsStandardInputAndNamesItStdin()
    {
        RecordingDataFileReader reader = new() { StandardInput = Encoding.UTF8.GetBytes("bogus\n") };

        CommandLineParseResult result = CommandLineParser.Parse(["-K", "-"], _ => true, new UnexpectedPasswordPrompt(), reader);

        CollectionAssert.AreEqual(new[] { "-" }, reader.Reads);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: <stdin>:1 config file option 'bogus' is unknown",
                "curl: option -K: found an unknown config option",
                TryHelp,
            },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileUnknownOption_IsRefusedWithTheFileAndLine()
    {
        CommandLineParseResult result = Parse(["-K", "b.cfg"], ("b.cfg", "bogus\n"));

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: b.cfg:1 config file option 'bogus' is unknown",
                "curl: option -K: found an unknown config option",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    /// <summary>
    /// <c>printf '\n# c\n  bogus = 1\n'</c>: the option runs from the start of the line, so it is empty
    /// and <c>bogus</c> is its parameter, and the line is numbered 1 because blank and comment lines are
    /// not counted.
    /// </summary>
    [TestMethod]
    public void Parse_ConfigFileIndentedOption_IsTheEmptyOptionOnLineOne()
    {
        CommandLineParseResult result = Parse(["-K", "c.cfg"], ("c.cfg", "\n# c\n  bogus = 1\n"));

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: c.cfg:1 Option '' uses argument with unquoted whitespace. This may ",
                "Warning: cause side-effects. Consider double quotes.",
            },
            result.WarningLines.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: c.cfg:1 config file option '' is unknown",
                "curl: option -K: found an unknown config option",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("-")]
    [DataRow("--")]
    public void Parse_ConfigFileUnknownDashedOption_IsRefusedAsUnknown(string option)
    {
        CommandLineParseResult result = Parse(["-K", "a.cfg"], ("a.cfg", option + "\n"));

        Assert.AreEqual($"curl: a.cfg:1 config file option '{option}' is unknown", result.Refusal!.StandardErrorLines[0]);
    }

    /// <summary><c>K u.cfg</c>: an option without a dash is a long name, and there is no <c>--K</c>.</summary>
    [TestMethod]
    public void Parse_ConfigFileUndashedShortLetter_IsAnUnknownLongName()
    {
        CommandLineParseResult result = Parse(["-K", "n3.cfg"], ("n3.cfg", "K u.cfg\n"), ("u.cfg", "bogus\n"));

        CollectionAssert.AreEqual(
            new[]
            {
                "curl: n3.cfg:1 config file option 'K' is unknown",
                "curl: option -K: found an unknown config option",
                TryHelp,
            },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_UnknownOptionInNestedConfigFile_IsReportedByEachFile()
    {
        CommandLineParseResult result = Parse(["-K", "n4.cfg"], ("n4.cfg", "-K u.cfg\n"), ("u.cfg", "bogus\n"));

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: u.cfg:1 config file option 'bogus' is unknown",
                "curl: n4.cfg:1 config file option '-K' found an unknown config option",
                "curl: option -K: found an unknown config option",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileOptionWithoutItsParameter_IsRefusedAsRequiringOne()
    {
        CommandLineParseResult result = Parse(["-K", "r.cfg"], ("r.cfg", "output\n"));

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: r.cfg:1 config file option 'output' requires parameter",
                "curl: option -K: requires parameter",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    /// <summary><c>output ""</c>: curl wraps the error line at 79 columns.</summary>
    [TestMethod]
    public void Parse_ConfigFileBlankParameter_IsRefusedWithAWrappedLine()
    {
        CommandLineParseResult result = Parse(["-K", "r2.cfg"], ("r2.cfg", "output \"\"\n"));

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: r2.cfg:1 config file option 'output' blank argument where content is ",
                "curl: expected",
                "curl: option -K: blank argument where content is expected",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    /// <summary>
    /// <c>-K</c> and a missing file named with 80 <c>f</c>s: the line is cut after the last space that
    /// fits, and the name, with no space to cut at, at 73 bytes, the width after <c>curl: </c>.
    /// </summary>
    [TestMethod]
    public void Parse_UnreadableConfigFileWithALongName_WrapsTheLineAsCurlDoes()
    {
        string file = new('f', 80);

        CommandLineParseResult result = Parse(["-K", file]);

        CollectionAssert.AreEqual(
            new[]
            {
                "curl: cannot read config from ",
                "curl: '" + new string('f', 72),
                "curl: ffffffff'",
                "curl: option -K: error encountered when reading a file",
                TryHelp,
            },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    /// <summary><c>--output=x y</c>: the attached value is used, the parameter is not.</summary>
    [TestMethod]
    public void Parse_ConfigFileParameterTheOptionDoesNotUse_IsRefusedAsTrailingGarbage()
    {
        CommandLineParseResult result = Parse(["-K", "g3.cfg"], ("g3.cfg", "--output=x y\n"));

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: g3.cfg:1 config file option '--output=x' had unsupported trailing garbage",
                "curl: option -K: had unsupported trailing garbage",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    /// <summary><c>silent foo</c>: <c>silent</c> is applied first, so its own error line is hidden.</summary>
    [TestMethod]
    public void Parse_ConfigFileFlagGivenAParameter_IsRefusedWithTheErrorLineHiddenBySilent()
    {
        CommandLineParseResult result = Parse(["-K", "g.cfg"], ("g.cfg", "silent foo\n"));

        CollectionAssert.AreEqual(
            new[] { "curl: option -K: had unsupported trailing garbage", TryHelp },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--output=x")]
    [DataRow("silent \"\"")]
    public void Parse_ConfigFileLineWithoutAnUnusedParameter_IsAccepted(string line)
    {
        CommandLineParseResult result = Parse(["-K", "a.cfg", Url], ("a.cfg", line + "\n"));

        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    [DataRow("-K")]
    [DataRow("--config")]
    public void Parse_MissingConfigFile_IsRefusedWithReadError(string option)
    {
        CommandLineParseResult result = Parse([option, "nx.cfg", Url]);

        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: cannot read config from 'nx.cfg'",
                $"curl: option {option}: error encountered when reading a file",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("-K", "")]
    [DataRow("--config=", null)]
    public void Parse_EmptyConfigFileName_IsRefusedAsUnreadable(string option, string? value)
    {
        CommandLineParseResult result = Parse(value is null ? [option] : [option, value]);

        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: cannot read config from ''",
                $"curl: option {option}: error encountered when reading a file",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_SilentBeforeMissingConfigFile_HidesTheCannotReadLine()
    {
        CommandLineParseResult result = Parse(["-s", "-K", "nx.cfg"]);

        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option -K: error encountered when reading a file", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_SilentAndShowErrorBeforeMissingConfigFile_KeepsTheCannotReadLine()
    {
        CommandLineParseResult result = Parse(["-s", "-S", "-K", "nx.cfg"]);

        Assert.AreEqual("curl: cannot read config from 'nx.cfg'", result.Refusal!.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_SilentBeforeConfigFileUnknownOption_HidesTheFileLine()
    {
        CommandLineParseResult result = Parse(["-s", "-K", "u.cfg"], ("u.cfg", "bogus\n"));

        CollectionAssert.AreEqual(
            new[] { "curl: option -K: found an unknown config option", TryHelp },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_MissingNestedConfigFile_IsReportedByEachFileWithReadError()
    {
        CommandLineParseResult result = Parse(["-K", "n1.cfg"], ("n1.cfg", "-K nx.cfg\n"));

        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: cannot read config from 'nx.cfg'",
                "curl: n1.cfg:1 config file option '-K' error encountered when reading a file",
                "curl: cannot read config from 'n1.cfg'",
                "curl: option -K: error encountered when reading a file",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileDataAtMissingFile_IsRefusedWithReadError()
    {
        CommandLineParseResult result = Parse(["-K", "d.cfg", Url], ("d.cfg", "data @nx\n"));

        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: Failed to open nx",
                "curl: d.cfg:1 config file option 'data' error encountered when reading a file",
                "curl: cannot read config from 'd.cfg'",
                "curl: option -K: error encountered when reading a file",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    /// <summary><c>config n2.cfg</c> inside <c>n2.cfg</c>: five files are read, the sixth is refused.</summary>
    [TestMethod]
    public void Parse_ConfigFileIncludingItself_StopsAtFiveLevels()
    {
        CommandLineParseResult result = Parse(["-K", "n2.cfg"], ("n2.cfg", "config n2.cfg\n"));

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: Max config file recursion level reached (5)",
                "curl: n2.cfg:1 config file option 'config' is badly used here",
                "curl: n2.cfg:1 config file option 'config' is badly used here",
                "curl: n2.cfg:1 config file option 'config' is badly used here",
                "curl: n2.cfg:1 config file option 'config' is badly used here",
                "curl: n2.cfg:1 config file option 'config' is badly used here",
                "curl: option -K: is badly used here",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigFileIncludingItselfAfterSilent_HidesEveryErrorLine()
    {
        CommandLineParseResult result = Parse(["-s", "-K", "n2.cfg"], ("n2.cfg", "config n2.cfg\n"));

        CollectionAssert.AreEqual(
            new[] { "curl: option -K: is badly used here", TryHelp },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_TwoConfigFilesOneAfterTheOther_AreBothRead()
    {
        CommandLineParseResult result = Parse(
            ["-K", "a.cfg", "-K", "a.cfg", "-K", "a.cfg", "-K", "a.cfg", "-K", "a.cfg", "-K", "b.cfg"],
            ("a.cfg", "-K b.cfg\n"),
            ("b.cfg", "url http://a/\n"));

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(6, result.Options.Urls);
    }

    [TestMethod]
    public void Parse_ConfigFileNameThatLooksLikeAFlag_Warns()
    {
        CommandLineParseResult result = Parse(["-K", "-bogus"]);

        CollectionAssert.AreEqual(new[] { "Warning: The filename argument '-bogus' looks like a flag." }, result.WarningLines.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: cannot read config from '-bogus'",
                "curl: option -K: error encountered when reading a file",
                TryHelp,
            },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ConfigOptionAsLastArgument_RequiresAParameter()
    {
        CommandLineParseResult result = Parse(["-K"]);

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option -K: requires parameter", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--no-config")]
    [DataRow("--no-config=x")]
    public void Parse_NegatedConfig_IsRefusedAsNotReversible(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {argument}: the given option cannot be reversed with a --no- prefix", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, params (string Name, string Text)[] files)
    {
        RecordingDataFileReader reader = new();
        foreach ((string name, string text) in files)
        {
            reader.Files[name] = Encoding.UTF8.GetBytes(text);
        }

        return CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader);
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
