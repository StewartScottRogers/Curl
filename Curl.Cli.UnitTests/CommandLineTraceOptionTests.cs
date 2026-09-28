using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Parse_NoTraceOption_TracesNothing()
    {
        CommandLineOptions options = Accept(Url);

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
        Assert.IsFalse(Accept(arguments).TraceTime);
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
        Assert.IsTrue(Accept(arguments).TraceTime);
    }

    [TestMethod]
    [DataRow("--no-trace-time")]
    [DataRow("--no-trace-time=x")]
    [DataRow("--trace-time --no-trace-time")]
    public void Parse_NoTraceTimeLast_ShowsNoTimes(string arguments)
    {
        CommandLineParseResult result = Parse(arguments);

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

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedTrace, result.Options.Trace);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_VerboseThenTraceToFlagLikeName_WarnsForNameThenOverride()
    {
        CommandLineParseResult result = Parse("-v --trace -x");

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
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, "-x", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "Warning: The filename argument '-x' looks like a flag." }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    public void Parse_EmptyTraceFile_IsRefusedAsBlank(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, string.Empty, Url]);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    [DataRow("--stderr")]
    public void Parse_FileOptionAsLastArgument_IsRefusedAsRequiringParameter(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, spelledOption]);

        AssertRefused(result, $"curl: option {spelledOption}: requires parameter");
    }

    [TestMethod]
    [DataRow("--no-trace")]
    [DataRow("--no-trace-ascii")]
    [DataRow("--no-stderr")]
    public void Parse_NoSpellingOfFileOption_IsRefusedAsNotReversible(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

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

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedFile, result.Options.StandardErrorFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_EmptyStderr_IsKeptNotRefused()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--stderr", string.Empty, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.StandardErrorFile);
    }

    [TestMethod]
    public void Parse_NoStderr_HasNoStandardErrorRedirects()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsEmpty(result.StandardErrorRedirects);
    }

    [TestMethod]
    public void Parse_StderrAmongWarnings_RecordsEachAtTheWarningLinesMetBeforeIt()
    {
        CommandLineParseResult result = Parse("-H nocolon --stderr a -H x --stderr b -s --stderr c");

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
        CommandLineParseResult result = CommandLineParser.Parse(["--stderr", "se", "--bogus", Url]);

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { new StandardErrorRedirect("se", 0, false) }, result.StandardErrorRedirects.ToArray());
    }

    private static CommandLineParseResult Parse(string arguments) =>
        CommandLineParser.Parse([.. arguments.Split(' '), Url]);

    private static CommandLineOptions Accept(string arguments)
    {
        CommandLineParseResult result = arguments == Url ? CommandLineParser.Parse([Url]) : Parse(arguments);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private static void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
