using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-Z</c> / <c>--parallel</c>, <c>--parallel-immediate</c>, <c>--parallel-max</c> and
/// <c>--parallel-max-host</c> as curl 8.21.0 reads them, measured with the local curl 8.21.0 on
/// 2026-09-28 (BL-517 Notes): every value from zero up is accepted, a negative one is refused as not
/// positive, a malformed or blank one as not a proper number, and the two flags take a <c>--no-</c>
/// prefix while the two limits do not. All four are global, so they reach every <c>--next</c> group.
/// </summary>
[TestClass]
public sealed class CommandLineParallelOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string TryHelpLine = "curl: try 'curl --help' or 'curl --manual' for more information";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = Parse(arguments);
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted, result.IsAccepted ? string.Empty : result.Refusal.StandardErrorLines[0]);
        return result.Options;
    }

    private void AssertRefused(string expectedLine, params string[] arguments)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("first stderr line", expectedLine, CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines[0]);
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { expectedLine, TryHelpLine }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoParallelOption_UsesCurlDefaults()
    {
        CommandLineOptions options = Accept(Url);

        Assert.IsFalse(options.Parallel);
        Assert.IsFalse(options.ParallelImmediate);
        Assert.AreEqual(50, options.ParallelMax);
        Assert.AreEqual(0, options.ParallelMaxHost);
    }

    [TestMethod]
    [DataRow("-Z")]
    [DataRow("--parallel")]
    [DataRow("--parallel=x")]
    public void Parse_ParallelSpelling_TurnsParallelOn(string spelling)
    {
        Assert.IsTrue(Accept(spelling, Url).Parallel);
    }

    [TestMethod]
    [DataRow("-Z", "--no-parallel", false)]
    [DataRow("--no-parallel", "-Z", true)]
    [DataRow("--parallel", "--no-parallel", false)]
    public void Parse_TwoParallelSpellings_LastOneWins(string first, string second, bool parallel)
    {
        Assert.AreEqual(parallel, Accept(first, second, Url).Parallel);
    }

    [TestMethod]
    public void Parse_SilentAndParallelBundle_SetsBoth()
    {
        CommandLineOptions options = Accept("-sZ", Url);

        Assert.IsTrue(options.Silent);
        Assert.IsTrue(options.Parallel);
    }

    [TestMethod]
    [DataRow(new[] { "--parallel-immediate" }, true)]
    [DataRow(new[] { "--no-parallel-immediate" }, false)]
    [DataRow(new[] { "--parallel-immediate", "--no-parallel-immediate" }, false)]
    [DataRow(new[] { "--no-parallel-immediate", "--parallel-immediate" }, true)]
    public void Parse_ParallelImmediateSpellings_LastOneWins(string[] spellings, bool immediate)
    {
        Assert.AreEqual(immediate, Accept([.. spellings, Url]).ParallelImmediate);
    }

    [TestMethod]
    [DataRow("1", 1)]
    [DataRow("0", 50)]
    [DataRow("-0", 50)]
    [DataRow("300", 300)]
    [DataRow("301", 301)]
    [DataRow("65535", 65535)]
    [DataRow("65536", 65535)]
    [DataRow("2147483647", 65535)]
    public void Parse_ParallelMax_SetsLimit(string value, int expected)
    {
        Assert.AreEqual(expected, Accept("-Z", "--parallel-max", value, Url).ParallelMax);
    }

    [TestMethod]
    public void Parse_ParallelMaxTwice_LastOneWins()
    {
        Assert.AreEqual(7, Accept("--parallel-max", "3", "--parallel-max", "7", Url).ParallelMax);
    }

    [TestMethod]
    [DataRow("1", 1)]
    [DataRow("0", 0)]
    [DataRow("6", 6)]
    [DataRow("65535", 65535)]
    [DataRow("99999", 65535)]
    public void Parse_ParallelMaxHost_SetsLimit(string value, int expected)
    {
        Assert.AreEqual(expected, Accept("-Z", "--parallel-max-host", value, Url).ParallelMaxHost);
    }

    [TestMethod]
    [DataRow("--parallel-max")]
    [DataRow("--parallel-max-host")]
    public void Parse_NegativeLimit_IsRefusedAsNotPositive(string option)
    {
        AssertRefused($"curl: option {option}: expected a positive numerical parameter", "-Z", option, "-1", Url);
    }

    [TestMethod]
    [DataRow("--parallel-max", "")]
    [DataRow("--parallel-max", "abc")]
    [DataRow("--parallel-max", "1.5")]
    [DataRow("--parallel-max", "99999999999999999999")]
    [DataRow("--parallel-max-host", "")]
    public void Parse_MalformedLimit_IsRefusedAsNotANumber(string option, string value)
    {
        AssertRefused($"curl: option {option}: expected a proper numerical parameter", "-Z", option, value, Url);
    }

    [TestMethod]
    [DataRow("--no-parallel-max")]
    [DataRow("--no-parallel-max-host")]
    public void Parse_NoPrefixedLimit_CannotBeReversed(string spelling)
    {
        AssertRefused($"curl: option {spelling}: the given option cannot be reversed with a --no- prefix", spelling, "5", Url);
    }

    [TestMethod]
    public void Parse_ParallelOptionsAfterNext_ApplyToEveryGroup()
    {
        CommandLineParseResult result = Parse(
            [Url, "--next", "-Z", "--parallel-immediate", "--parallel-max", "4", "--parallel-max-host", "2", Url]);

        Diagnostics.Assert("groups", 2, result.Groups.Count);
        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Groups);
        foreach (CommandLineOptions group in result.Groups)
        {
            Assert.IsTrue(group.Parallel);
            Assert.IsTrue(group.ParallelImmediate);
            Assert.AreEqual(4, group.ParallelMax);
            Assert.AreEqual(2, group.ParallelMaxHost);
        }
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        foreach (CommandLineOptions group in result.Groups)
        {
            Diagnostics.Act(
                "group",
                $"parallel {group.Parallel}, parallel immediate {group.ParallelImmediate}, parallel max {group.ParallelMax}, parallel max host {group.ParallelMaxHost}, silent {group.Silent}");
        }

        return result;
    }
}
