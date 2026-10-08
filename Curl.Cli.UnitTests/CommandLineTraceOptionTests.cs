using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-v</c> / <c>--verbose</c>, <c>--trace</c>, <c>--trace-ascii</c>, <c>--trace-time</c> and
/// <c>--stderr</c> as curl 8.21.0 parses them, measured against the local curl 8.21.0 (mingw,
/// Windows) on 2026-09-26 with <c>curl &lt;arguments&gt; http://127.0.0.1:1/</c>, reading standard
/// error, the exit code and which trace files were written. The commands and what they printed are
/// in BL-195's Notes.
/// </summary>
[TestClass]
public sealed class CommandLineTraceOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string VerboseOverridesTrace = "Warning: -v, --verbose overrides an earlier trace option";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoTraceOption_TracesNothing()
    {
        CommandLineOptions options = Accept(Url);

        Diagnostics.Assert("trace", TraceKind.None, options.Trace);
        Diagnostics.Assert("trace file", null, options.TraceFile);
        Diagnostics.Assert("verbosity", 0, options.Verbosity);
        Diagnostics.Assert("trace time", false, options.TraceTime);
        Diagnostics.Assert("stderr file", null, options.StandardErrorFile);
        Assert.AreEqual(TraceKind.None, options.Trace);
        Assert.IsNull(options.TraceFile);
        Assert.AreEqual(0, options.Verbosity);
        Assert.IsFalse(options.TraceTime);
        Assert.IsNull(options.StandardErrorFile);
    }

    [TestMethod]
    [DataRow("-v", 1)]
    [DataRow("--verbose", 1)]
    [DataRow("--verbose=x", 1)]
    [DataRow("-v -v", 1)]
    [DataRow("--verbose --verbose", 1)]
    [DataRow("-vv", 2)]
    [DataRow("-vsv", 2)]
    [DataRow("-svv", 2)]
    [DataRow("-vvv", 3)]
    [DataRow("-vvvv", 4)]
    [DataRow("-vvvvv", 4)]
    [DataRow("-vv -v", 1)]
    [DataRow("-vv --verbose", 1)]
    [DataRow("-vv -sv", 3)]
    [DataRow("-vv --no-verbose -v", 1)]
    public void Parse_Verbose_CountsVerbosityAsCurlDoes(string arguments, int expectedVerbosity)
    {
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("trace", TraceKind.Verbose, options.Trace);
        Diagnostics.Assert("trace file", null, options.TraceFile);
        Diagnostics.Assert("verbosity", expectedVerbosity, options.Verbosity);
        Assert.AreEqual(TraceKind.Verbose, options.Trace);
        Assert.IsNull(options.TraceFile);
        Assert.AreEqual(expectedVerbosity, options.Verbosity);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("--trace-time -v")]
    [DataRow("-vv -v")]
    [DataRow("-vv --no-trace-time")]
    [DataRow("--trace-time --no-verbose -sv")]
    [DataRow("--trace-time -v --no-verbose")]
    public void Parse_TraceTimeResetOrNeverSet_ShowsNoTimes(string arguments)
    {
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("trace time", false, options.TraceTime);
        Assert.IsFalse(options.TraceTime);
    }

    [TestMethod]
    [DataRow("--trace-time")]
    [DataRow("--no-trace-time --trace-time")]
    [DataRow("-v --trace-time")]
    [DataRow("--trace-time -sv")]
    [DataRow("-vv")]
    [DataRow("--trace-time -vv")]
    [DataRow("--trace t1 --trace-time")]
    public void Parse_TraceTimeOrSecondV_ShowsTimes(string arguments)
    {
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("trace time", true, options.TraceTime);
        Assert.IsTrue(options.TraceTime);
    }

    // Measured 2026-09-29 (BL-648 Notes): --trace-ids -v prints no IDs, --trace-ids -sv and -vv do,
    // and -vvv --no-trace-ids does not.
    [TestMethod]
    [DataRow("-v")]
    [DataRow("--trace-ids -v")]
    [DataRow("-vv -v")]
    [DataRow("-vvv --no-trace-ids")]
    [DataRow("--trace-ids --no-trace-ids")]
    [DataRow("--trace-ids -v --no-verbose")]
    public void Parse_TraceIdsResetOrNeverSet_ShowsNoIds(string arguments)
    {
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("trace IDs", false, options.TraceIds);
        Assert.IsFalse(options.TraceIds);
    }

    [TestMethod]
    [DataRow("--trace-ids")]
    [DataRow("--no-trace-ids --trace-ids")]
    [DataRow("-v --trace-ids")]
    [DataRow("--trace-ids -sv")]
    [DataRow("-vv")]
    [DataRow("-vvv")]
    [DataRow("--trace-ascii - --trace-ids")]
    public void Parse_TraceIdsOrSecondV_ShowsIds(string arguments)
    {
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("trace IDs", true, options.TraceIds);
        Assert.IsTrue(options.TraceIds);
    }

    [TestMethod]
    public void Parse_TraceIdsInFirstGroup_ReachesTheSecondGroup()
    {
        CommandLineParseResult result = Parse("--trace-ids " + Url + " --next");

        Diagnostics.Act("group count", result.Groups.Count);
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("second group trace IDs", true, result.Groups.Count > 1 ? result.Groups[1].TraceIds : null);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Groups[1].TraceIds);
    }

    [TestMethod]
    [DataRow("--no-trace-time")]
    [DataRow("--no-trace-time=x")]
    [DataRow("--trace-time --no-trace-time")]
    public void Parse_NoTraceTimeLast_ShowsNoTimes(string arguments)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("trace time", false, Recorded(result)?.TraceTime);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.TraceTime);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--no-verbose")]
    [DataRow("--no-verbose=x")]
    [DataRow("-v --no-verbose")]
    [DataRow("-vvv --no-verbose")]
    [DataRow("--trace t1 --no-verbose")]
    [DataRow("--trace-ascii t2 --no-verbose")]
    public void Parse_NoVerboseLast_TracesNothing(string arguments)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("trace", TraceKind.None, Recorded(result)?.Trace);
        Diagnostics.Assert("trace file", null, Recorded(result)?.TraceFile);
        Diagnostics.Assert("verbosity", 0, Recorded(result)?.Verbosity);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TraceKind.None, result.Options.Trace);
        Assert.IsNull(result.Options.TraceFile);
        Assert.AreEqual(0, result.Options.Verbosity);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--trace t1", TraceKind.HexDump, "t1")]
    [DataRow("--trace=t1", TraceKind.HexDump, "t1")]
    [DataRow("--trace -", TraceKind.HexDump, "-")]
    [DataRow("--trace t1 --trace t3", TraceKind.HexDump, "t3")]
    [DataRow("--trace-ascii t2", TraceKind.AsciiDump, "t2")]
    [DataRow("--trace-ascii -", TraceKind.AsciiDump, "-")]
    [DataRow("--trace-ascii t1 --trace-ascii t3", TraceKind.AsciiDump, "t3")]
    [DataRow("--no-verbose --trace t1", TraceKind.HexDump, "t1")]
    [DataRow("-v --no-verbose --trace t1", TraceKind.HexDump, "t1")]
    public void Parse_TraceWithoutOverride_WritesDumpToFileWithoutWarning(string arguments, TraceKind expectedTrace, string expectedFile)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("trace", expectedTrace, Recorded(result)?.Trace);
        Diagnostics.Assert("trace file", expectedFile, Recorded(result)?.TraceFile);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedTrace, result.Options.Trace);
        Assert.AreEqual(expectedFile, result.Options.TraceFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-v --trace t1", TraceKind.HexDump, "t1", "--trace")]
    [DataRow("-vv --trace t4", TraceKind.HexDump, "t4", "--trace")]
    [DataRow("--trace-ascii t2 --trace t1", TraceKind.HexDump, "t1", "--trace")]
    [DataRow("-v --trace-ascii t2", TraceKind.AsciiDump, "t2", "--trace-ascii")]
    [DataRow("--trace t1 --trace-ascii t2", TraceKind.AsciiDump, "t2", "--trace-ascii")]
    public void Parse_TraceAfterOtherKind_LastWinsAfterWarning(string arguments, TraceKind expectedTrace, string expectedFile, string longName)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("trace", expectedTrace, Recorded(result)?.Trace);
        Diagnostics.Assert("trace file", expectedFile, Recorded(result)?.TraceFile);
        AssertWarningLines([$"Warning: {longName} overrides an earlier trace/verbose option"], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedTrace, result.Options.Trace);
        Assert.AreEqual(expectedFile, result.Options.TraceFile);
        CollectionAssert.AreEqual(
            new[] { $"Warning: {longName} overrides an earlier trace/verbose option" },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_VerboseThenAsciiThenHexTrace_WarnsTwice()
    {
        CommandLineParseResult result = Parse("-v --trace-ascii t6 --trace t7");

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("trace", TraceKind.HexDump, Recorded(result)?.Trace);
        Diagnostics.Assert("trace file", "t7", Recorded(result)?.TraceFile);
        AssertWarningLines(
            [
                "Warning: --trace-ascii overrides an earlier trace/verbose option",
                "Warning: --trace overrides an earlier trace/verbose option",
            ],
            result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TraceKind.HexDump, result.Options.Trace);
        Assert.AreEqual("t7", result.Options.TraceFile);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: --trace-ascii overrides an earlier trace/verbose option",
                "Warning: --trace overrides an earlier trace/verbose option",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--trace t1 -v", 1)]
    [DataRow("--trace-ascii t2 -v", 1)]
    [DataRow("--trace t4 -vv", 2)]
    [DataRow("--trace t8 -v -v", 1)]
    [DataRow("--trace t1 --verbose", 1)]
    public void Parse_VerboseAfterTrace_LastWinsAfterWarning(string arguments, int expectedVerbosity)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("trace", TraceKind.Verbose, Recorded(result)?.Trace);
        Diagnostics.Assert("trace file", null, Recorded(result)?.TraceFile);
        Diagnostics.Assert("verbosity", expectedVerbosity, Recorded(result)?.Verbosity);
        AssertWarningLines([VerboseOverridesTrace], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TraceKind.Verbose, result.Options.Trace);
        Assert.IsNull(result.Options.TraceFile);
        Assert.AreEqual(expectedVerbosity, result.Options.Verbosity);
        CollectionAssert.AreEqual(new[] { VerboseOverridesTrace }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_VerboseAfterTraceThenNoVerbose_DoesNotWarnForNextVerbose()
    {
        CommandLineParseResult result = Parse("--trace t1 --no-verbose -v");

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("trace", TraceKind.Verbose, Recorded(result)?.Trace);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TraceKind.Verbose, result.Options.Trace);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-s -v --trace t5", TraceKind.HexDump)]
    [DataRow("-s --trace t5 -v", TraceKind.Verbose)]
    public void Parse_OverrideAfterSilent_DropsWarning(string arguments, TraceKind expectedTrace)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("trace", expectedTrace, Recorded(result)?.Trace);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedTrace, result.Options.Trace);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_VerboseThenTraceToFlagLikeName_WarnsForNameThenOverride()
    {
        CommandLineParseResult result = Parse("-v --trace -x");

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("trace file", "-x", Recorded(result)?.TraceFile);
        AssertWarningLines(
            [
                "Warning: The filename argument '-x' looks like a flag.",
                "Warning: --trace overrides an earlier trace/verbose option",
            ],
            result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-x", result.Options.TraceFile);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: The filename argument '-x' looks like a flag.",
                "Warning: --trace overrides an earlier trace/verbose option",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    [DataRow("--stderr")]
    public void Parse_FileNameThatLooksLikeFlag_WarnsAndTakesIt(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "-x", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarningLines(["Warning: The filename argument '-x' looks like a flag."], result);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "Warning: The filename argument '-x' looks like a flag." }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    public void Parse_EmptyTraceFile_IsRefusedAsBlank(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, string.Empty, Url]);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    [DataRow("--stderr")]
    public void Parse_FileOptionAsLastArgument_IsRefusedAsRequiringParameter(string spelledOption)
    {
        CommandLineParseResult result = Parse([Url, spelledOption]);

        AssertRefused(result, $"curl: option {spelledOption}: requires parameter");
    }

    [TestMethod]
    [DataRow("--no-trace")]
    [DataRow("--no-trace-ascii")]
    [DataRow("--no-stderr")]
    public void Parse_NoSpellingOfFileOption_IsRefusedAsNotReversible(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, Url]);

        AssertRefused(result, $"curl: option {spelledOption}: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    [DataRow("--stderr se", "se")]
    [DataRow("--stderr=se", "se")]
    [DataRow("--stderr -", "-")]
    [DataRow("--stderr se --stderr se2", "se2")]
    public void Parse_Stderr_KeepsLastFileName(string arguments, string expectedFile)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("stderr file", expectedFile, Recorded(result)?.StandardErrorFile);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedFile, result.Options.StandardErrorFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_EmptyStderr_IsKeptNotRefused()
    {
        CommandLineParseResult result = Parse(["--stderr", string.Empty, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("stderr file", string.Empty, Recorded(result)?.StandardErrorFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.StandardErrorFile);
    }

    [TestMethod]
    public void Parse_NoStderr_HasNoStandardErrorRedirects()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("stderr redirect count", 0, result.StandardErrorRedirects.Count);
        Assert.IsEmpty(result.StandardErrorRedirects);
    }

    [TestMethod]
    public void Parse_StderrAmongWarnings_RecordsEachAtTheWarningLinesMetBeforeIt()
    {
        CommandLineParseResult result = Parse("-H nocolon --stderr a -H x --stderr b -s --stderr c");

        StandardErrorRedirect[] expected =
        [
            new StandardErrorRedirect("a", 1, false),
            new StandardErrorRedirect("b", 2, false),
            new StandardErrorRedirect("c", 2, true),
        ];
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("stderr redirects", string.Join(", ", expected.Select(redirect => redirect.ToString())), string.Join(", ", result.StandardErrorRedirects.Select(redirect => redirect.ToString())));
        Diagnostics.Assert("options share the redirects", true, ReferenceEquals(Recorded(result)?.StandardErrorRedirects, result.StandardErrorRedirects));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[]
            {
                new StandardErrorRedirect("a", 1, false),
                new StandardErrorRedirect("b", 2, false),
                new StandardErrorRedirect("c", 2, true),
            },
            result.StandardErrorRedirects.ToArray());
        Assert.AreSame(result.Options.StandardErrorRedirects, result.StandardErrorRedirects);
    }

    [TestMethod]
    public void Parse_RefusalAfterStderr_KeepsTheRedirect()
    {
        CommandLineParseResult result = Parse(["--stderr", "se", "--bogus", Url]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("stderr redirects", new StandardErrorRedirect("se", 0, false).ToString(), string.Join(", ", result.StandardErrorRedirects.Select(redirect => redirect.ToString())));
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { new StandardErrorRedirect("se", 0, false) }, result.StandardErrorRedirects.ToArray());
    }

    /// <summary>
    /// Returns the parsed options, or null for a refusal, for diagnostic lines written before the test asserts
    /// acceptance, without making the compiler treat <see cref="CommandLineParseResult.Options"/> as possibly null.
    /// </summary>
    private static CommandLineOptions? Recorded(CommandLineParseResult result) => result.Options;

    private CommandLineParseResult Parse(string arguments) =>
        Parse([.. arguments.Split(' '), Url]);

    /// <summary>Parses <paramref name="arguments"/>, writing them, the outcome and the trace option values as diagnostics.</summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("trace", result.Options.Trace);
            Diagnostics.Act("trace file", result.Options.TraceFile);
            Diagnostics.Act("verbosity", result.Options.Verbosity);
            Diagnostics.Act("trace time", result.Options.TraceTime);
            Diagnostics.Act("trace IDs", result.Options.TraceIds);
            Diagnostics.Act("stderr file", result.Options.StandardErrorFile);
        }

        return result;
    }

    private CommandLineOptions Accept(string arguments)
    {
        CommandLineParseResult result = arguments == Url ? Parse([Url]) : Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private void AssertWarningLines(IEnumerable<string> expected, CommandLineParseResult result) =>
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));

    private void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [expectedFirstLine, CommandLineRefusal.TryHelpLine]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
