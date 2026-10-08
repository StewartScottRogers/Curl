using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--trace-config &lt;list&gt;</c> as curl 8.21.0 (mingw, Schannel) parses it, measured on
/// 2026-10-01 with <c>curl -s &lt;arguments&gt; http://127.0.0.1:1/</c> and with
/// <c>Record-CurlExchange.ps1</c>, reading whether the verbose lines carried <c>[0-0]</c> IDs and times.
/// The commands and what they printed are in BL-649's Notes.
/// </summary>
[TestClass]
public sealed class CommandLineTraceConfigTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--trace-config", "ids", "-v")]
    [DataRow("-v", "--trace-config", "ids")]
    [DataRow("--trace-config", "+ids", "-v")]
    [DataRow("--trace-config", "IDS", "-v")]
    [DataRow("--trace-config", "ids", "-v", "-v")]
    [DataRow("-v", "--trace-config", "ids", "-v")]
    [DataRow("--trace-config", "ids", "-sv")]
    [DataRow("--trace-config", "bogus,ids", "-v")]
    public void Parse_TraceConfigIds_ShowsIdsEvenAfterAFirstV(params string[] arguments)
    {
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("trace IDs", true, options.TraceIds);
        Diagnostics.Assert("trace time", false, options.TraceTime);
        Assert.IsTrue(options.TraceIds);
        Assert.IsFalse(options.TraceTime);
    }

    [TestMethod]
    [DataRow("--trace-config", "time", "-v")]
    [DataRow("--trace-config", "Time", "-v")]
    [DataRow("--trace-config", "all,-ids", "-v")]
    public void Parse_TraceConfigTime_ShowsTimesWithoutIds(params string[] arguments)
    {
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("trace time", true, options.TraceTime);
        Diagnostics.Assert("trace IDs", false, options.TraceIds);
        Assert.IsTrue(options.TraceTime);
        Assert.IsFalse(options.TraceIds);
    }

    [TestMethod]
    [DataRow("--trace-config", "ids,time", "-v")]
    [DataRow("--trace-config", "ids, time", "-v")]
    [DataRow("--trace-config", "ids,,time", "-v")]
    [DataRow("--trace-config", "ids,bogus,time", "-v")]
    [DataRow("--trace-config", "all", "-v")]
    [DataRow("-v", "--trace-config", "time,ids", "-s")]
    public void Parse_TraceConfigIdsAndTime_ShowsBoth(params string[] arguments)
    {
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("trace IDs", true, options.TraceIds);
        Diagnostics.Assert("trace time", true, options.TraceTime);
        Assert.IsTrue(options.TraceIds);
        Assert.IsTrue(options.TraceTime);
    }

    [TestMethod]
    [DataRow("--trace-config", "ids time", "-v")]
    [DataRow("--trace-config", " ids", "-v")]
    [DataRow("--trace-config", "ids ", "-v")]
    [DataRow("--trace-config", "+ ids", "-v")]
    [DataRow("--trace-config", "ids;time", "-v")]
    [DataRow("--trace-config", "", "-v")]
    [DataRow("--trace-config", ",", "-v")]
    [DataRow("--trace-config", "bogus", "-v")]
    [DataRow("--trace-config", "ids", "--no-trace-ids", "-v")]
    [DataRow("--trace-config", "time", "--no-trace-time", "-v")]
    [DataRow("--trace-config", "ids,time", "--no-verbose", "-v")]
    [DataRow("--trace-config", "ids,time", "-vv", "--no-verbose", "-v")]
    [DataRow("--trace-config", "all", "--trace-config", "-all", "-v")]
    [DataRow("-vv", "--trace-config", "-ids,-time")]
    [DataRow("-vv", "--trace-config", "-all")]
    [DataRow("--trace-ids", "--trace-config", "-ids", "-sv")]
    public void Parse_TraceConfigWithoutIdsOrTime_ShowsNeither(params string[] arguments)
    {
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("trace IDs", false, options.TraceIds);
        Diagnostics.Assert("trace time", false, options.TraceTime);
        Assert.IsFalse(options.TraceIds);
        Assert.IsFalse(options.TraceTime);
    }

    [TestMethod]
    public void Parse_TraceConfigComponents_KeepsEachNameInLowerCase()
    {
        CommandLineOptions options = Accept("--trace-config", "TLS,http/1, Dns,bogus,ids", "-v");

        Diagnostics.Assert("trace components", SortedAndQuoted(["tls", "http/1", "dns", "bogus"]), SortedAndQuoted(options.TraceComponents));
        CollectionAssert.AreEquivalent(new[] { "tls", "http/1", "dns", "bogus" }, options.TraceComponents.ToArray());
    }

    [TestMethod]
    public void Parse_TraceConfigTwice_AddsToAndTakesFromTheComponents()
    {
        CommandLineOptions options = Accept("--trace-config", "tls,dns", "--trace-config", "-tls,+doh,-never-on");

        Diagnostics.Assert("trace components", SortedAndQuoted(["dns", "doh"]), SortedAndQuoted(options.TraceComponents));
        CollectionAssert.AreEquivalent(new[] { "dns", "doh" }, options.TraceComponents.ToArray());
    }

    [TestMethod]
    public void Parse_TraceConfigAll_TurnsOnEveryComponentUntilMinusAll()
    {
        bool allAfterAll = Accept("--trace-config", "all").TraceComponents.Contains("all");
        bool allAfterMinusAll = Accept("--trace-config", "all,-all").TraceComponents.Contains("all");

        Diagnostics.Assert("trace components contain all after all", true, allAfterAll);
        Diagnostics.Assert("trace components contain all after all,-all", false, allAfterMinusAll);
        Assert.IsTrue(allAfterAll);
        Assert.IsFalse(allAfterMinusAll);
    }

    [TestMethod]
    public void Parse_NoTraceConfig_TurnsOnNoComponent()
    {
        int componentCount = Accept("-v").TraceComponents.Count;

        Diagnostics.Assert("trace component count", 0, componentCount);
        Assert.AreEqual(0, componentCount);
    }

    [TestMethod]
    [DataRow(new[] { "-vv" }, new[] { "setup", "protocol" })]
    [DataRow(new[] { "-vsv" }, new[] { "setup", "protocol" })]
    [DataRow(new[] { "-vvv" }, new[] { "setup", "protocol", "read", "write" })]
    [DataRow(new[] { "-vvvv" }, new[] { "setup", "protocol", "read", "write", "all" })]
    [DataRow(new[] { "-vvvvv" }, new[] { "setup", "protocol", "read", "write", "all" })]
    [DataRow(new[] { "--trace-config", "-setup", "-vv" }, new[] { "setup", "protocol" })]
    [DataRow(new[] { "-vv", "--trace-config", "-network" }, new[] { "setup", "protocol" })]
    [DataRow(new[] { "-vvv", "--trace-config", "-read" }, new[] { "setup", "protocol", "write" })]
    [DataRow(new[] { "-vv", "--trace-config", "-setup" }, new[] { "protocol" })]
    [DataRow(new[] { "-vv", "--trace-config", "-all" }, new string[0])]
    [DataRow(new[] { "-vv", "-v" }, new string[0])]
    [DataRow(new[] { "-vv", "--no-verbose", "-v" }, new string[0])]
    [DataRow(new[] { "--trace-config", "setup", "-v", "--no-verbose", "-v" }, new string[0])]
    [DataRow(new[] { "--trace-config", "dns", "-v", "-v" }, new[] { "dns" })]
    [DataRow(new[] { "-vv", "--trace-config", "setup", "-v" }, new[] { "setup" })]
    public void Parse_VerbosityAndTraceConfig_TurnOnTheComponentsCurlTurnsOn(string[] arguments, string[] expected)
    {
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("trace components", SortedAndQuoted(expected), SortedAndQuoted(options.TraceComponents));
        CollectionAssert.AreEquivalent(expected, options.TraceComponents.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "-vvvv" }, new[] { "setup", "protocol", "read", "write", "all" })]
    [DataRow(new[] { "-vvvv", "--trace-config", "all" }, new[] { "setup", "protocol", "read", "write" })]
    [DataRow(new[] { "--trace-config", "all", "-vvvv" }, new[] { "setup", "protocol", "read", "write" })]
    [DataRow(new[] { "-v", "--trace-config", "socks" }, new string[0])]
    public void Parse_VerbosityAndTraceConfig_KeepsTheComponentsOnlyVerbosityTurnedOn(string[] arguments, string[] expected)
    {
        // -vvvv's all does not write [SOCKS] lines, --trace-config all does (BL-1191 Notes).
        CommandLineOptions options = Accept(arguments);

        Diagnostics.Assert("verbosity trace components", SortedAndQuoted(expected), SortedAndQuoted(options.VerbosityTraceComponents));
        CollectionAssert.AreEquivalent(expected, options.VerbosityTraceComponents.ToArray());
    }

    [TestMethod]
    public void Parse_TraceConfigInFirstGroup_ReachesTheSecondGroup()
    {
        CommandLineParseResult result = Parse(["--trace-config", "ids,tls", Url, "--next", Url]);

        Diagnostics.Act("group count", result.Groups.Count);
        Diagnostics.Assert("group 1 trace IDs", true, result.Groups.Count > 1 && result.Groups[1].TraceIds);
        Diagnostics.Assert("group 1 trace components contain tls", true, result.Groups.Count > 1 && result.Groups[1].TraceComponents.Contains("tls"));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Groups[1].TraceIds);
        Assert.IsTrue(result.Groups[1].TraceComponents.Contains("tls"));
    }

    [TestMethod]
    public void Parse_NoTraceConfigSpelling_IsRefused()
    {
        CommandLineParseResult result = Parse(["--no-trace-config", Url]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
    }

    private static string SortedAndQuoted(IEnumerable<string> components) =>
        CommandLineParseDiagnostics.QuoteEach(components.Order(StringComparer.Ordinal));

    private CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("trace IDs", result.Options.TraceIds);
            Diagnostics.Act("trace time", result.Options.TraceTime);
            Diagnostics.Act("trace components", SortedAndQuoted(result.Options.TraceComponents));
            Diagnostics.Act("verbosity trace components", SortedAndQuoted(result.Options.VerbosityTraceComponents));
        }

        return result;
    }
}
