using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Adversarial black-box tests of <see cref="CommandLineParser"/> (BL-1493), by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c>: numeric options at and past their limits, a value-taking
/// option with no value, malformed clusters and <c>--</c>, <c>--next</c> in the wrong place, config files with an
/// unterminated quote, every escape, a byte order mark, CR-only line ends and a very long line, and the parser
/// called again and from many tasks at once. Every pinned answer was measured with curl 8.21.0 (mingw, Schannel)
/// on 2026-10-07 with <c>curl -q &lt;arguments&gt; http://127.0.0.1:1/</c> and <c>CURL_HOME</c> and <c>HOME</c>
/// pointing at an empty directory; values were read back with <c>-w '%{url_effective}'</c>.
/// </summary>
[TestClass]
public sealed class CommandLineParserAdversarialTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-o")]
    [DataRow("--max-time")]
    [DataRow("--url")]
    public void Parse_ValueTakingOptionAsTheLastArgument_RefusesAsRequiresParameter(string option)
    {
        CommandLineParseResult result = Parse([Url, option]);

        AssertRefused(result, CurlExitCode.FailedInit, $"curl: option {option}: requires parameter");
    }

    [TestMethod]
    public void Parse_RetryAtTheIntLimit_IsAccepted()
    {
        CommandLineParseResult result = Parse(["--retry", "2147483647", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("RetryCount", 2147483647L, result.Options.RetryCount);
        Assert.AreEqual(2147483647L, result.Options.RetryCount);
    }

    [TestMethod]
    [DataRow("1.5")]
    [DataRow("0x10")]
    [DataRow("")]
    public void Parse_RetryNotAnInteger_RefusesAsNotProperNumerical(string value)
    {
        CommandLineParseResult result = Parse(["--retry", value, Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --retry: expected a proper numerical parameter");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Parse_OnWindows_RetryOnePastTheWindowsLongLimit_RefusesAsNotProperNumerical()
    {
        CommandLineParseResult result = Parse(["--retry", "2147483648", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --retry: expected a proper numerical parameter");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Parse_OnLinuxOrMacOS_RetryOnePastTheLongLimit_RefusesAsNotProperNumerical()
    {
        CommandLineParseResult result = Parse(["--retry", "9223372036854775808", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --retry: expected a proper numerical parameter");
    }

    [TestMethod]
    public void Parse_RetryNegative_RefusesAsNotPositiveNumerical()
    {
        CommandLineParseResult result = Parse(["--retry", "-1", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --retry: expected a positive numerical parameter");
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("-1")]
    public void Parse_MaxTimeNotANonNegativeNumber_RefusesAsNotProperNumerical(string value)
    {
        CommandLineParseResult result = Parse(["--max-time", value, Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --max-time: expected a proper numerical parameter");
    }

    [TestMethod]
    public void Parse_MaxTimeFraction_IsAccepted()
    {
        CommandLineParseResult result = Parse(["--max-time", "1.5", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("MaxTime", TimeSpan.FromSeconds(1.5), result.Options.MaxTime);
        Assert.AreEqual(TimeSpan.FromSeconds(1.5), result.Options.MaxTime);
    }

    [TestMethod]
    public void Parse_LongOptionJoinedToItsValueWithEquals_TakesTheValue()
    {
        CommandLineParseResult result = Parse(["--max-time=5", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("MaxTime", TimeSpan.FromSeconds(5), result.Options.MaxTime);
        Assert.AreEqual(TimeSpan.FromSeconds(5), result.Options.MaxTime);
    }

    [TestMethod]
    public void Parse_NumericLongOptionWithEqualsAndNoValue_RefusesNamingTheOptionAsSpelled()
    {
        CommandLineParseResult result = Parse(["--max-time=", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --max-time=: expected a proper numerical parameter");
    }

    [TestMethod]
    public void Parse_OutputWithEqualsAndNoValue_RefusesAsBlankArgument()
    {
        CommandLineParseResult result = Parse(["--output=", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --output=: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_LimitRateWithGigabyteSuffix_IsAccepted()
    {
        CommandLineParseResult result = Parse(["--limit-rate", "1G", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("LimitRate", 1L << 30, result.Options.LimitRate);
        Assert.AreEqual(1L << 30, result.Options.LimitRate);
    }

    [TestMethod]
    [DataRow("--limit-rate", "1X")]
    [DataRow("--max-filesize", "1Q")]
    public void Parse_SizeWithAnUnknownSuffix_RefusesAsBadlyUsed(string option, string value)
    {
        CommandLineParseResult result = Parse([option, value, Url]);

        AssertRefused(result, CurlExitCode.FailedInit, $"curl: option {option}: is badly used here");
    }

    [TestMethod]
    public void Parse_LimitRateNegative_RefusesAsNotProperNumerical()
    {
        CommandLineParseResult result = Parse(["--limit-rate", "-1", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --limit-rate: expected a proper numerical parameter");
    }

    [TestMethod]
    public void Parse_MaxRedirsMinusTwo_RefusesAsNotProperNumerical()
    {
        CommandLineParseResult result = Parse(["--max-redirs", "-2", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --max-redirs: expected a proper numerical parameter");
    }

    [TestMethod]
    public void Parse_MaxRedirsMinusOne_IsAcceptedAsUnlimited()
    {
        CommandLineParseResult result = Parse(["--max-redirs", "-1", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    public void Parse_ProtoWithOnlyAnUnknownProtocolAfterEquals_WarnsAndRefusesAsBadlyUsed()
    {
        CommandLineParseResult result = Parse(["--proto", "=bogus", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --proto: is badly used here");
        CollectionAssert.AreEqual(new[] { "Warning: unrecognized protocol 'bogus'" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_ClusterWithAnUnknownLetter_RefusesNamingTheWholeCluster()
    {
        CommandLineParseResult result = Parse(["-sZ5", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option -sZ5: is unknown");
    }

    [TestMethod]
    [DataRow("-")]
    [DataRow("---x")]
    [DataRow("--bogus")]
    public void Parse_MalformedOrUnknownOptionName_RefusesAsUnknown(string option)
    {
        CommandLineParseResult result = Parse([option, Url]);

        AssertRefused(result, CurlExitCode.FailedInit, $"curl: option {option}: is unknown");
    }

    [TestMethod]
    [DataRow("-\uD800")]
    [DataRow("--max\0time")]
    public void Parse_OptionNameWithALoneSurrogateOrANul_RefusesAsUnknownWithoutThrowing(string option)
    {
        CommandLineParseResult result = Parse([option, Url]);

        AssertRefused(result, CurlExitCode.FailedInit, $"curl: option {option}: is unknown");
    }

    [TestMethod]
    public void Parse_OptionAfterDoubleDash_IsTakenAsAUrl()
    {
        CommandLineParseResult result = Parse(["--", "-o", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "-o", Url }, result.Options.Urls.ToArray());
        Assert.IsEmpty(result.Options.OutputFiles);
    }

    [TestMethod]
    public void Parse_DoubleDashAlone_RefusesAsNoUrl()
    {
        CommandLineParseResult result = Parse(["--"]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: (2) no URL specified");
    }

    [TestMethod]
    [DataRow("--next")]
    [DataRow("-:")]
    public void Parse_NextBeforeAnyUrl_RefusesAsMissingUrlThenBadlyUsed(string option)
    {
        CommandLineParseResult result = Parse([option, Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: missing URL before --next", $"curl: option {option}: is badly used here");
    }

    [TestMethod]
    public void Parse_NoPrefixOnANumericOption_RefusesAsNotReversible()
    {
        CommandLineParseResult result = Parse(["--no-max-time", "5", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, "curl: option --no-max-time: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_ConfigFileUnterminatedQuote_TakesTheRestOfTheLine()
    {
        CommandLineParseResult result = Parse(["-K", "a.cfg"], ("a.cfg", "url = \"http://a/\n"));

        AssertAcceptedUrls(result, "http://a/");
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ConfigFileQuotedValue_DecodesEveryEscapeAndDropsTheBackslashBeforeAnyOther()
    {
        CommandLineParseResult result = Parse(["-K", "a.cfg"], ("a.cfg", "url = \"http://h/\\t\\n\\\\\\\"\\q\\r\\v\"\n"));

        AssertAcceptedUrls(result, "http://h/\t\n\\\"q\r\v");
    }

    [TestMethod]
    public void Parse_ConfigFileTextAfterTheClosingQuote_IsIgnoredSilently()
    {
        CommandLineParseResult result = Parse(["-K", "a.cfg"], ("a.cfg", "url = \"http://h/d\" trailing\n"));

        AssertAcceptedUrls(result, "http://h/d");
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ConfigFileStartingWithAByteOrderMark_RefusesTheFirstOptionAsUnknown()
    {
        CommandLineParseResult result = ParseBytes(["-K", "c2"], ("c2", [0xEF, 0xBB, 0xBF, .. Encoding.ASCII.GetBytes("url = http://h/b\n")]));

        AssertRefused(
            result,
            CurlExitCode.FailedInit,
            "curl: c2:1 config file option '﻿url' is unknown",
            "curl: option -K: found an unknown config option");
    }

    [TestMethod]
    public void Parse_ConfigFileWithCarriageReturnOnlyLineEnds_ReadsOneLineWithTheCarriageReturnInTheOptionName()
    {
        CommandLineParseResult result = Parse(["-K", "c3"], ("c3", "silent\rurl = http://h/c\r"));

        AssertRefused(
            result,
            CurlExitCode.FailedInit,
            "curl: c3:1 config file option 'silent\rurl' is unknown",
            "curl: option -K: found an unknown config option");
    }

    [TestMethod]
    public void Parse_ConfigFileUrlWithNothingAfterTheEquals_RefusesAsRequiresParameter()
    {
        CommandLineParseResult result = Parse(["-K", "c4"], ("c4", "url = \n"));

        AssertRefused(
            result,
            CurlExitCode.FailedInit,
            "curl: c4:1 config file option 'url' requires parameter",
            "curl: option -K: requires parameter");
    }

    [TestMethod]
    public void Parse_ConfigFileOfOnlyBlankAndTabLines_AddsNothing()
    {
        CommandLineParseResult result = Parse(["-K", "c5", Url], ("c5", "   \n\t\n"));

        AssertAcceptedUrls(result, Url);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ConfigFileNumericOptionNotANumber_RefusesNamingTheFileLineAndOption()
    {
        CommandLineParseResult result = Parse(["-K", "c7", Url], ("c7", "max-time = abc\n"));

        AssertRefused(
            result,
            CurlExitCode.FailedInit,
            "curl: c7:1 config file option 'max-time' expected a proper numerical parameter",
            "curl: option -K: expected a proper numerical parameter");
    }

    [TestMethod]
    public void Parse_ConfigFileLineOfTwentyThousandBytes_KeepsTheWholeValue()
    {
        string longUrl = "http://h/" + new string('a', 20000);

        CommandLineParseResult result = Parse(["-K", "c11"], ("c11", "url = \"" + longUrl + "\"\n"));

        AssertAcceptedUrls(result, longUrl);
    }

    [TestMethod]
    public void Parse_SameRefusedArgumentsTwiceThenValidOnes_AnswersEachCallAfresh()
    {
        string[] refused = ["--retry", "abc", Url];

        CommandLineParseResult first = Parse(refused);
        CommandLineParseResult second = Parse(refused);
        CommandLineParseResult accepted = Parse(["-s", Url]);

        Diagnostics.Assert("second stderr", CommandLineParseDiagnostics.QuoteEach(first.Refusal!.StandardErrorLines), CommandLineParseDiagnostics.QuoteEach(second.Refusal?.StandardErrorLines ?? []));
        CollectionAssert.AreEqual(first.Refusal!.StandardErrorLines.ToArray(), second.Refusal!.StandardErrorLines.ToArray());
        Diagnostics.Assert("third accepted", true, accepted.IsAccepted);
        Assert.IsTrue(accepted.IsAccepted);
        Assert.IsTrue(accepted.Options.Silent);
        Assert.AreEqual(0L, accepted.Options.RetryCount);
    }

    [TestMethod]
    public async Task Parse_ManyCommandLinesOnManyTasksAtOnce_AnswerAsTheyDoOneAfterAnother()
    {
        const int Seed = 1493;
        Diagnostics.Arrange("seed", Seed);
        Random random = new(Seed);
        string[][] commandLines = [.. Enumerable.Range(0, 200).Select(_ => RandomCommandLine(random))];
        string[] sequential = [.. commandLines.Select(arguments => Describe(ParseQuietly(arguments)))];

        string[] concurrent = await Task.WhenAll(commandLines.Select(arguments => Task.Run(() => Describe(ParseQuietly(arguments)))));

        int firstDifference = Enumerable.Range(0, sequential.Length).FirstOrDefault(index => sequential[index] != concurrent[index], -1);
        Diagnostics.Assert("first differing command line", -1, firstDifference);
        CollectionAssert.AreEqual(sequential, concurrent);
    }

    private static string[] RandomCommandLine(Random random)
    {
        string[][] pieces =
        [
            ["-s"],
            ["--max-time", "1.5"],
            ["--retry", "abc"],
            ["--limit-rate", "1k"],
            ["-o", "out"],
            ["--bogus"],
            [Url],
            ["-K"],
        ];
        return [.. Enumerable.Range(0, random.Next(1, 6)).SelectMany(_ => pieces[random.Next(pieces.Length)])];
    }

    private static CommandLineParseResult ParseQuietly(IReadOnlyList<string> arguments) =>
        CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), new RecordingDataFileReader(), isWindows: true);

    private static string Describe(CommandLineParseResult result) =>
        result.Refusal is { } refusal
            ? $"refused {(int)refusal.ExitCode} {CommandLineParseDiagnostics.QuoteEach(refusal.StandardErrorLines)}"
            : $"accepted {CommandLineParseDiagnostics.QuoteEach(result.Options!.Urls)} {result.Options.MaxTime} {result.Options.LimitRate} {result.Options.Silent} {CommandLineParseDiagnostics.QuoteEach(result.Options.OutputFiles)}";

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, params (string Name, string Text)[] files) =>
        ParseBytes(arguments, [.. files.Select(file => (file.Name, Encoding.UTF8.GetBytes(file.Text)))]);

    private CommandLineParseResult ParseBytes(IReadOnlyList<string> arguments, params (string Name, byte[] Contents)[] files)
    {
        RecordingDataFileReader reader = new();
        foreach ((string name, byte[] contents) in files)
        {
            reader.Files[name] = contents;
            Diagnostics.Bytes("config file " + name, contents);
        }

        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader, isWindows: true);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertRefused(CommandLineParseResult result, CurlExitCode expectedExitCode, params string[] expectedLines)
    {
        string[] expected = [.. expectedLines, CommandLineRefusal.TryHelpLine];
        Diagnostics.AssertRefusal(result, expectedExitCode, expected);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(expectedExitCode, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(expected, result.Refusal.StandardErrorLines.ToArray());
    }

    private void AssertAcceptedUrls(CommandLineParseResult result, params string[] expectedUrls)
    {
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach(expectedUrls), CommandLineParseDiagnostics.QuoteEach(result.Options.Urls));
        CollectionAssert.AreEqual(expectedUrls, result.Options.Urls.ToArray());
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
