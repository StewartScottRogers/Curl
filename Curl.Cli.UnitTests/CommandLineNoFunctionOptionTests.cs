using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser reads curl's nine no-function options (<c>--sslv2</c>/<c>-2</c>, <c>--sslv3</c>/<c>-3</c>,
/// <c>--metalink</c>, <c>--npn</c>, <c>--ntlm-wb</c>, <c>--false-start</c>, <c>--egd-file</c>, <c>--random-file</c>
/// and <c>--krb4</c>), measured against the local curl 8.21.0 (Windows) on 2026-09-28 with
/// <c>Record-CurlExchange.ps1</c> and a loopback 200 (BL-488): each prints
/// <c>Warning: --&lt;name&gt; is deprecated and has no function anymore</c> and the transfer carries on, exit 0;
/// the three value options take their value; <c>-s</c> before one drops the warning, after it does not;
/// <c>-2s</c>, <c>-2v</c> and <c>-23</c> are read as <c>-2</c> alone; <c>--no-metalink</c> and the other
/// negatable ones warn under their positive name; <c>--no-sslv2</c> and <c>--no-egd-file</c> are not
/// reversible; and a value option as the last argument requires its parameter, with no warning.
/// </summary>
[TestClass]
public sealed class CommandLineNoFunctionOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--sslv2", "sslv2")]
    [DataRow("--sslv3", "sslv3")]
    [DataRow("-2", "sslv2")]
    [DataRow("-3", "sslv3")]
    [DataRow("--metalink", "metalink")]
    [DataRow("--npn", "npn")]
    [DataRow("--ntlm-wb", "ntlm-wb")]
    [DataRow("--false-start", "false-start")]
    [DataRow("--no-metalink", "metalink")]
    [DataRow("--no-npn", "npn")]
    [DataRow("--no-ntlm-wb", "ntlm-wb")]
    [DataRow("--no-false-start", "false-start")]
    [DataRow("--metalink=x", "metalink")]
    public void Parse_NoFunctionFlag_WarnsAndChangesNothing(string argument, string longName)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach([Url]), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Options)?.Urls ?? []));
        CollectionAssert.AreEqual(new[] { Url }, result.Options.Urls.ToArray());
        AssertNothingElseSet(result.Options);
        CollectionAssert.AreEqual(new[] { Warning(longName) }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--egd-file", "egd-file")]
    [DataRow("--random-file", "random-file")]
    [DataRow("--krb4", "krb4")]
    public void Parse_NoFunctionValueOption_TakesItsValueAndWarns(string argument, string longName)
    {
        CommandLineParseResult result = Parse([argument, "x", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach([Url]), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Options)?.Urls ?? []));
        CollectionAssert.AreEqual(new[] { Url }, result.Options.Urls.ToArray());
        AssertNothingElseSet(result.Options);
        CollectionAssert.AreEqual(new[] { Warning(longName) }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--egd-file=x")]
    [DataRow("--egd-file=")]
    public void Parse_NoFunctionValueOptionWithAttachedValue_TakesItAndWarns(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach([Url]), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Options)?.Urls ?? []));
        CollectionAssert.AreEqual(new[] { Url }, result.Options.Urls.ToArray());
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach([Warning("egd-file")]), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        CollectionAssert.AreEqual(new[] { Warning("egd-file") }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoFunctionValueOptionWithEmptyValue_TakesItAndWarns()
    {
        CommandLineParseResult result = Parse(["--krb4", string.Empty, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach([Warning("krb4")]), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        CollectionAssert.AreEqual(new[] { Warning("krb4") }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoFunctionValueOptionBeforeTheUrl_TakesTheUrlAsItsValue()
    {
        CommandLineParseResult result = Parse(["--egd-file", Url]);

        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach([Warning("egd-file")]), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        CollectionAssert.AreEqual(new[] { Warning("egd-file") }, result.WarningLines.ToArray());
        Diagnostics.Assert("stderr lines", CommandLineParseDiagnostics.QuoteEach(new[] { "curl: (2) no URL specified", TryHelp }), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        CollectionAssert.AreEqual(
            new[] { "curl: (2) no URL specified", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--egd-file")]
    [DataRow("--random-file")]
    [DataRow("--krb4")]
    public void Parse_NoFunctionValueOptionLast_RequiresParameterWithoutWarning(string argument)
    {
        CommandLineParseResult result = Parse([Url, argument]);

        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsEmpty(result.WarningLines);
        Diagnostics.Assert("stderr lines", CommandLineParseDiagnostics.QuoteEach(new[] { $"curl: option {argument}: requires parameter", TryHelp }), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        CollectionAssert.AreEqual(
            new[] { $"curl: option {argument}: requires parameter", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--no-sslv2")]
    [DataRow("--no-sslv3")]
    [DataRow("--no-egd-file")]
    [DataRow("--no-random-file")]
    [DataRow("--no-krb4")]
    public void Parse_NegatedNoFunctionOptionThatCannotBeReversed_IsRefused(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        Diagnostics.Assert("stderr lines", CommandLineParseDiagnostics.QuoteEach(new[] { $"curl: option {argument}: the given option cannot be reversed with a --no- prefix", TryHelp }), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        CollectionAssert.AreEqual(
            new[] { $"curl: option {argument}: the given option cannot be reversed with a --no- prefix", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("-2s", "sslv2")]
    [DataRow("-2v", "sslv2")]
    [DataRow("-23", "sslv2")]
    [DataRow("-3s", "sslv3")]
    public void Parse_NoFunctionLetterInABundle_EndsTheBundle(string argument, string longName)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("Silent", false, CommandLineParseDiagnostics.Peek(result.Options)?.Silent);
        Assert.IsFalse(result.Options.Silent);
        Diagnostics.Assert("Verbosity", 0, CommandLineParseDiagnostics.Peek(result.Options)?.Verbosity);
        Assert.AreEqual(0, result.Options.Verbosity);
        CollectionAssert.AreEqual(new[] { Warning(longName) }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_LetterBeforeANoFunctionLetter_IsApplied()
    {
        CommandLineParseResult result = Parse(["-v2", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("Verbosity", 1, CommandLineParseDiagnostics.Peek(result.Options)?.Verbosity);
        Assert.AreEqual(1, result.Options.Verbosity);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach([Warning("sslv2")]), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        CollectionAssert.AreEqual(new[] { Warning("sslv2") }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--metalink")]
    [DataRow("-2")]
    [DataRow("--no-npn")]
    public void Parse_SilentThenNoFunctionFlag_DropsTheWarning(string argument)
    {
        CommandLineParseResult result = Parse(["-s", argument, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_SilentThenNoFunctionValueOption_DropsTheWarning()
    {
        CommandLineParseResult result = Parse(["-s", "--egd-file", "x", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_NoFunctionFlagThenSilent_KeepsTheWarning()
    {
        CommandLineParseResult result = Parse(["--metalink", "-s", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach([Warning("metalink")]), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        CollectionAssert.AreEqual(new[] { Warning("metalink") }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_EveryNoFunctionOptionTogether_WarnsForEachInOrder()
    {
        CommandLineParseResult result = Parse(
            ["-2", "-3", "--metalink", "--npn", "--ntlm-wb", "--egd-file", "e", "--random-file", "r", "--krb4", "k", "--false-start", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("warning count", 9, result.WarningLines.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                Warning("sslv2"), Warning("sslv3"), Warning("metalink"), Warning("npn"), Warning("ntlm-wb"),
                Warning("egd-file"), Warning("random-file"), Warning("krb4"), Warning("false-start"),
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_UnknownOption_IsStillRefusedAsUnknown()
    {
        CommandLineParseResult result = Parse(["--bogus", Url]);

        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        Diagnostics.Assert("stderr lines", CommandLineParseDiagnostics.QuoteEach(new[] { "curl: option --bogus: is unknown", TryHelp }), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        CollectionAssert.AreEqual(
            new[] { "curl: option --bogus: is unknown", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void NoFunctionRowsAndNext_EndTheBundleAndOnlyTheyDo()
    {
        Diagnostics.Arrange("rows", CommandLineOptionTable.Rows.Count);

        string[] endingBundle = CommandLineOptionTable.Rows.Where(row => row.EndsBundle).Select(row => row.LongName).ToArray();

        Diagnostics.Act("rows ending the bundle", CommandLineParseDiagnostics.QuoteEach(endingBundle));
        Diagnostics.Assert(
            "rows ending the bundle, sorted",
            "[\"false-start\", \"metalink\", \"next\", \"npn\", \"ntlm-wb\", \"sslv2\", \"sslv3\"]",
            CommandLineParseDiagnostics.QuoteEach(endingBundle.Order(StringComparer.Ordinal)));
        CollectionAssert.AreEquivalent(
            new[] { "sslv2", "sslv3", "metalink", "npn", "ntlm-wb", "false-start", "next" },
            endingBundle);
    }

    [TestMethod]
    public void NoFunctionFlag_NullLongName_Throws()
    {
        Diagnostics.Arrange("long name", null);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineOption.NoFunctionFlag(null!, null, negatable: false));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "longName", exception.ParamName);
    }

    [TestMethod]
    public void NoFunctionValue_NullLongName_Throws()
    {
        Diagnostics.Arrange("long name", null);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineOption.NoFunctionValue(null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "longName", exception.ParamName);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private static string Warning(string longName) => $"Warning: --{longName} is deprecated and has no function anymore";

    private static void AssertNothingElseSet(CommandLineOptions options)
    {
        CommandLineOptions defaults = CommandLineParser.Parse([Url]).Options!;
        Assert.AreEqual(defaults.MinimumTlsVersion, options.MinimumTlsVersion);
        Assert.AreEqual(defaults.Silent, options.Silent);
        Assert.AreEqual(defaults.Verbosity, options.Verbosity);
        Assert.AreEqual(defaults.UseAlpn, options.UseAlpn);
        Assert.AreEqual(defaults.AuthSchemes, options.AuthSchemes);
        Assert.IsEmpty(options.OutputFiles);
    }
}
