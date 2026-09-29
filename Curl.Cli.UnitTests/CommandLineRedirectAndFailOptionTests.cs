using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>-L</c> / <c>--location</c>, <c>--location-trusted</c>,
/// <c>--max-redirs</c>, <c>--post301</c>, <c>--post302</c>, <c>--post303</c>, <c>-i</c> /
/// <c>--show-headers</c> / <c>--include</c>, <c>-I</c> / <c>--head</c>, <c>-f</c> / <c>--fail</c>,
/// <c>--fail-with-body</c> and <c>--fail-early</c>. Every refusal, warning and <c>--no-</c> spelling
/// was measured against the local curl 8.21.0 (mingw, Schannel) on 2026-09-26 with
/// <c>curl &lt;arguments&gt; http://127.0.0.1:1/ -o /dev/null</c>, reading standard error and the exit code.
/// </summary>
[TestClass]
public sealed class CommandLineRedirectAndFailOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    private const string ProperNumber = "expected a proper numerical parameter";

    private static readonly string[] HeadAfterGetWarning =
    [
        "Warning: You can only select one HTTP request method! You asked for both HEAD (-I, --head) and GET (-G, --get).",
    ];

    private static readonly string[] GetAfterHeadWarning =
    [
        "Warning: You can only select one HTTP request method! You asked for both GET (-G, --get) and HEAD (-I, --head).",
    ];

    // ---- defaults -----------------------------------------------------------------

    [TestMethod]
    public void Parse_NoneOfTheOptions_LeavesDefaults()
    {
        CommandLineOptions options = Accept(Url);

        Assert.IsFalse(options.FollowRedirects);
        Assert.IsFalse(options.SendCredentialsToRedirectHosts);
        Assert.AreEqual(50, options.MaxRedirects);
        Assert.IsFalse(options.KeepPostAfter301);
        Assert.IsFalse(options.KeepPostAfter302);
        Assert.IsFalse(options.KeepPostAfter303);
        Assert.IsFalse(options.ShowHeaders);
        Assert.IsFalse(options.NoBody);
        Assert.AreEqual(HttpFailMode.None, options.FailMode);
        Assert.IsFalse(options.FailEarly);
    }

    // ---- -L, --location-trusted -----------------------------------------------------

    [TestMethod]
    [DataRow("-L")]
    [DataRow("--location")]
    public void Parse_Location_FollowsRedirectsWithoutSendingCredentialsOn(string spelling)
    {
        CommandLineOptions options = Accept(spelling, Url);

        Assert.IsTrue(options.FollowRedirects);
        Assert.IsFalse(options.SendCredentialsToRedirectHosts);
    }

    [TestMethod]
    public void Parse_LocationTrusted_FollowsRedirectsAndSendsCredentialsOn()
    {
        CommandLineOptions options = Accept("--location-trusted", Url);

        Assert.IsTrue(options.FollowRedirects);
        Assert.IsTrue(options.SendCredentialsToRedirectHosts);
    }

    [TestMethod]
    public void Parse_LocationThenNoLocation_DoesNotFollow()
    {
        CommandLineOptions options = Accept("-L", "--no-location", Url);

        Assert.IsFalse(options.FollowRedirects);
    }

    [TestMethod]
    public void Parse_LocationTrustedThenNoLocationTrusted_TurnsOffBoth()
    {
        CommandLineOptions options = Accept("--location-trusted", "--no-location-trusted", Url);

        Assert.IsFalse(options.FollowRedirects);
        Assert.IsFalse(options.SendCredentialsToRedirectHosts);
    }

    // ---- --follow (curl 8.21.0, measured, BL-627 Notes) ------------------------------

    [TestMethod]
    public void Parse_NoneOfTheOptions_DoesNotFollowPerSpec()
    {
        Assert.IsFalse(Accept(Url).FollowRedirectsPerSpec);
    }

    [TestMethod]
    public void Parse_Follow_FollowsRedirectsPerSpec()
    {
        CommandLineOptions options = Accept("--follow", Url);

        Assert.IsTrue(options.FollowRedirects);
        Assert.IsTrue(options.FollowRedirectsPerSpec);
        Assert.IsFalse(options.SendCredentialsToRedirectHosts);
    }

    [TestMethod]
    [DataRow("-L")]
    [DataRow("--location")]
    public void Parse_Location_DoesNotFollowPerSpec(string spelling)
    {
        Assert.IsFalse(Accept(spelling, Url).FollowRedirectsPerSpec);
    }

    [TestMethod]
    [DataRow("--follow", "-L")]
    [DataRow("--follow", "--location-trusted")]
    [DataRow("--follow", "--no-follow", "-L")]
    [DataRow("--follow", "--no-location", "-L")]
    [DataRow("--follow", "--no-location", "--location-trusted")]
    public void Parse_LocationAfterFollow_FollowsAsLocation(params string[] arguments)
    {
        CommandLineOptions options = Accept([.. arguments, Url]);

        Assert.IsTrue(options.FollowRedirects);
        Assert.IsFalse(options.FollowRedirectsPerSpec);
    }

    [TestMethod]
    [DataRow("-L", "--follow")]
    [DataRow("--location-trusted", "--follow")]
    [DataRow("--no-location", "--follow")]
    public void Parse_FollowAfterLocation_FollowsPerSpec(params string[] arguments)
    {
        CommandLineOptions options = Accept([.. arguments, Url]);

        Assert.IsTrue(options.FollowRedirects);
        Assert.IsTrue(options.FollowRedirectsPerSpec);
    }

    [TestMethod]
    [DataRow("--follow", "--no-follow")]
    [DataRow("-L", "--no-follow")]
    [DataRow("--follow", "--no-location")]
    [DataRow("--follow", "--no-location-trusted")]
    public void Parse_NoFollowOrNoLocationLast_DoesNotFollow(params string[] arguments)
    {
        CommandLineOptions options = Accept([.. arguments, Url]);

        Assert.IsFalse(options.FollowRedirects);
        Assert.IsFalse(options.FollowRedirectsPerSpec);
    }

    [TestMethod]
    public void Parse_LocationTrustedAfterFollow_KeepsSendingCredentialsOn()
    {
        CommandLineOptions options = Accept("--follow", "--location-trusted", Url);

        Assert.IsTrue(options.SendCredentialsToRedirectHosts);
    }

    // ---- --max-redirs -------------------------------------------------------------

    [TestMethod]
    [DataRow("-1", -1)]
    [DataRow("-01", -1)]
    [DataRow("-0", 0)]
    [DataRow("0", 0)]
    [DataRow("5", 5)]
    [DataRow("007", 7)]
    [DataRow("2147483647", int.MaxValue)]
    public void Parse_MaxRedirs_RecordsLimit(string value, int expected)
    {
        CommandLineOptions options = Accept("--max-redirs", value, Url);

        Assert.AreEqual(expected, options.MaxRedirects);
    }

    [TestMethod]
    public void Parse_MaxRedirsMinusOne_MeansNoLimit()
    {
        CommandLineOptions options = Accept("--max-redirs=-1", Url);

        Assert.AreEqual(-1, options.MaxRedirects);
    }

    [TestMethod]
    public void Parse_MaxRedirsTwice_LastWins()
    {
        CommandLineOptions options = Accept("--max-redirs", "3", "--max-redirs", "9", Url);

        Assert.AreEqual(9, options.MaxRedirects);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-2")]
    [DataRow("--1")]
    [DataRow("-")]
    [DataRow("abc")]
    [DataRow("5x")]
    [DataRow(" 5")]
    [DataRow("+5")]
    [DataRow("0x10")]
    [DataRow("1.5")]
    [DataRow("-2147483648")]
    [DataRow("99999999999999999999")]
    public void Parse_MaxRedirsUnreadable_IsRefusedAsNotProperNumber(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--max-redirs", value, Url]);

        AssertRefused(result, "curl: option --max-redirs: " + ProperNumber, TryHelp);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Parse_OnWindows_MaxRedirsPastTwoToThe31_IsRefusedAsNotProperNumber()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--max-redirs", "2147483648", Url]);

        AssertRefused(result, "curl: option --max-redirs: " + ProperNumber, TryHelp);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("2147483648")]
    [DataRow("9223372036854775807")]
    public void Parse_OnLinuxOrMacOS_MaxRedirsPastTwoToThe31_RecordsIntMaximum(string value)
    {
        CommandLineOptions options = Accept("--max-redirs", value, Url);

        Assert.AreEqual(int.MaxValue, options.MaxRedirects);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Parse_OnLinuxOrMacOS_MaxRedirsPastTwoToThe63_IsRefusedAsNotProperNumber()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--max-redirs", "9223372036854775808", Url]);

        AssertRefused(result, "curl: option --max-redirs: " + ProperNumber, TryHelp);
    }

    [TestMethod]
    public void Parse_MaxRedirsLast_IsRefusedAsNeedingParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, "--max-redirs"]);

        AssertRefused(result, "curl: option --max-redirs: requires parameter", TryHelp);
    }

    [TestMethod]
    [DataRow("--no-max-redirs")]
    [DataRow("--no-max-redirs=x")]
    public void Parse_NoMaxRedirs_IsRefusedAsNotReversible(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url]);

        AssertRefused(result, $"curl: option {spelling}: the given option cannot be reversed with a --no- prefix", TryHelp);
    }

    // ---- --post301, --post302, --post303 -------------------------------------------

    [TestMethod]
    public void Parse_Post301_KeepsPostAfter301Only()
    {
        CommandLineOptions options = Accept("--post301", Url);

        Assert.IsTrue(options.KeepPostAfter301);
        Assert.IsFalse(options.KeepPostAfter302);
        Assert.IsFalse(options.KeepPostAfter303);
    }

    [TestMethod]
    public void Parse_Post302_KeepsPostAfter302Only()
    {
        CommandLineOptions options = Accept("--post302", Url);

        Assert.IsFalse(options.KeepPostAfter301);
        Assert.IsTrue(options.KeepPostAfter302);
        Assert.IsFalse(options.KeepPostAfter303);
    }

    [TestMethod]
    public void Parse_Post303_KeepsPostAfter303Only()
    {
        CommandLineOptions options = Accept("--post303", Url);

        Assert.IsFalse(options.KeepPostAfter301);
        Assert.IsFalse(options.KeepPostAfter302);
        Assert.IsTrue(options.KeepPostAfter303);
    }

    [TestMethod]
    public void Parse_Post301ThenNoPost301_SwitchesToGet()
    {
        Assert.IsFalse(Accept("--post301", "--no-post301", Url).KeepPostAfter301);
    }

    [TestMethod]
    public void Parse_Post302ThenNoPost302_SwitchesToGet()
    {
        Assert.IsFalse(Accept("--post302", "--no-post302", Url).KeepPostAfter302);
    }

    [TestMethod]
    public void Parse_Post303ThenNoPost303_SwitchesToGet()
    {
        Assert.IsFalse(Accept("--post303", "--no-post303", Url).KeepPostAfter303);
    }

    // ---- -i, --show-headers, --include ---------------------------------------------

    [TestMethod]
    [DataRow("-i")]
    [DataRow("--show-headers")]
    [DataRow("--include")]
    public void Parse_ShowHeaders_ShowsHeadersAndKeepsBody(string spelling)
    {
        CommandLineOptions options = Accept(spelling, Url);

        Assert.IsTrue(options.ShowHeaders);
        Assert.IsFalse(options.NoBody);
    }

    [TestMethod]
    public void Parse_ShowHeadersThenNoShowHeaders_HidesHeaders()
    {
        Assert.IsFalse(Accept("-i", "--no-show-headers", Url).ShowHeaders);
    }

    [TestMethod]
    public void Parse_IncludeThenNoInclude_HidesHeaders()
    {
        Assert.IsFalse(Accept("--include", "--no-include", Url).ShowHeaders);
    }

    // ---- -I, --head ---------------------------------------------------------------

    [TestMethod]
    [DataRow("-I")]
    [DataRow("--head")]
    [DataRow("--head=x")]
    public void Parse_Head_AsksForNoBodyAndShowsHeaders(string spelling)
    {
        CommandLineOptions options = Accept(spelling, Url);

        Assert.IsTrue(options.NoBody);
        Assert.IsTrue(options.ShowHeaders);
    }

    [TestMethod]
    public void Parse_HeadTwice_IsAccepted()
    {
        Assert.IsTrue(Accept("-I", "-I", Url).NoBody);
    }

    [TestMethod]
    public void Parse_NoHeadTwice_IsAccepted()
    {
        CommandLineOptions options = Accept("--no-head", "--no-head", Url);

        Assert.IsFalse(options.NoBody);
        Assert.IsFalse(options.ShowHeaders);
    }

    [TestMethod]
    public void Parse_HeadAndShowHeaders_AskForNoBody()
    {
        CommandLineOptions options = Accept("-i", "-I", Url);

        Assert.IsTrue(options.NoBody);
        Assert.IsTrue(options.ShowHeaders);
    }

    [TestMethod]
    [DataRow("--no-head")]
    [DataRow("--no-head=x")]
    public void Parse_HeadThenNoHead_WarnsAndIsRefused(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-I", spelling, Url]);

        AssertRefused(result, $"curl: option {spelling}: is badly used here", TryHelp);
        CollectionAssert.AreEqual(GetAfterHeadWarning, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("-I")]
    [DataRow("--head")]
    [DataRow("--head=x")]
    [DataRow("-Is")]
    public void Parse_NoHeadThenHead_WarnsAndIsRefused(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-head", spelling, Url]);

        AssertRefused(result, $"curl: option {spelling}: is badly used here", TryHelp);
        CollectionAssert.AreEqual(HeadAfterGetWarning, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoHeadThenSilentHeadBundle_IsRefusedWithoutWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-head", "-sI", Url]);

        AssertRefused(result, "curl: option -sI: is badly used here", TryHelp);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-s", "-I", "--no-head")]
    [DataRow("-s", "-S", "-I", "--no-head")]
    [DataRow("-sI", "--no-head")]
    public void Parse_SilentThenConflictingHead_IsRefusedWithoutWarning(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        AssertRefused(result, "curl: option --no-head: is badly used here", TryHelp);
        Assert.IsEmpty(result.WarningLines);
    }

    // ---- -f, --fail-with-body, --fail-early ------------------------------------------

    [TestMethod]
    [DataRow("-f")]
    [DataRow("--fail")]
    public void Parse_Fail_FailsWithoutBody(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url]);

        Assert.AreEqual(HttpFailMode.Fail, result.Options!.FailMode);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_FailWithBody_FailsWithBody()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--fail-with-body", Url]);

        Assert.AreEqual(HttpFailMode.FailWithBody, result.Options!.FailMode);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_FailThenFailWithBody_WarnsAndFailsWithBody()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-f", "--fail-with-body", Url]);

        Assert.AreEqual(HttpFailMode.FailWithBody, result.Options!.FailMode);
        CollectionAssert.AreEqual(new[] { "Warning: --fail-with-body deselects --fail here" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_FailWithBodyThenFail_WarnsAndFailsWithoutBody()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--fail-with-body", "-f", Url]);

        Assert.AreEqual(HttpFailMode.Fail, result.Options!.FailMode);
        CollectionAssert.AreEqual(new[] { "Warning: --fail deselects --fail-with-body here" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("-s", "-f", "--fail-with-body")]
    [DataRow("-sf", "--fail-with-body")]
    [DataRow("-S", "-s", "--fail-with-body", "-f")]
    public void Parse_SilentThenBothFailModes_DoesNotWarn(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_BothFailModesThenSilent_StillWarns()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--fail-with-body", "-f", "-s", Url]);

        CollectionAssert.AreEqual(new[] { "Warning: --fail deselects --fail-with-body here" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("-f", "--fail")]
    [DataRow("--fail-with-body", "--fail-with-body")]
    public void Parse_SameFailModeTwice_DoesNotWarn(string first, string second)
    {
        CommandLineParseResult result = CommandLineParser.Parse([first, second, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-f", "--no-fail")]
    [DataRow("-f", "--no-fail-with-body")]
    [DataRow("--fail-with-body", "--no-fail")]
    [DataRow("--fail-with-body", "--no-fail-with-body")]
    public void Parse_FailModeThenEitherNegation_TurnsFailingOff(string failMode, string negation)
    {
        CommandLineOptions options = Accept(failMode, negation, Url);

        Assert.AreEqual(HttpFailMode.None, options.FailMode);
    }

    [TestMethod]
    [DataRow("--fail-with-body", "--no-fail", "-f")]
    [DataRow("-f", "--no-fail", "--fail-with-body")]
    [DataRow("-f", "--no-fail-with-body", "--fail-with-body")]
    [DataRow("--fail-with-body", "--no-fail-with-body", "-f")]
    public void Parse_NegationBetweenFailModes_DoesNotWarn(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_FailEarly_StopsAtFirstFailure()
    {
        Assert.IsTrue(Accept("--fail-early", Url).FailEarly);
    }

    [TestMethod]
    public void Parse_FailEarlyThenNoFailEarly_GoesOn()
    {
        Assert.IsFalse(Accept("--fail-early", "--no-fail-early", Url).FailEarly);
    }

    // ---- --no- spellings, each accepted on its own ----------------------------------

    [TestMethod]
    [DataRow("--no-location")]
    [DataRow("--no-location-trusted")]
    [DataRow("--no-post301")]
    [DataRow("--no-post302")]
    [DataRow("--no-post303")]
    [DataRow("--no-show-headers")]
    [DataRow("--no-include")]
    [DataRow("--no-head")]
    [DataRow("--no-fail")]
    [DataRow("--no-fail-with-body")]
    [DataRow("--no-fail-early")]
    [DataRow("--no-location=x")]
    [DataRow("--no-fail=x")]
    public void Parse_NoSpellingAlone_IsAcceptedAndLeavesDefault(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
        Assert.IsFalse(result.Options.FollowRedirects);
        Assert.AreEqual(HttpFailMode.None, result.Options.FailMode);
    }

    private static CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private static void AssertRefused(CommandLineParseResult result, params string[] standardErrorLines)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(standardErrorLines, result.Refusal.StandardErrorLines.ToArray());
    }
}
