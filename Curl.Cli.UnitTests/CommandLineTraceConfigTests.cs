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

        Assert.IsFalse(options.TraceIds);
        Assert.IsFalse(options.TraceTime);
    }

    [TestMethod]
    public void Parse_TraceConfigComponents_KeepsEachNameInLowerCase()
    {
        CommandLineOptions options = Accept("--trace-config", "TLS,http/1, Dns,bogus,ids", "-v");

        CollectionAssert.AreEquivalent(new[] { "tls", "http/1", "dns", "bogus" }, options.TraceComponents.ToArray());
    }

    [TestMethod]
    public void Parse_TraceConfigTwice_AddsToAndTakesFromTheComponents()
    {
        CommandLineOptions options = Accept("--trace-config", "tls,dns", "--trace-config", "-tls,+doh,-never-on");

        CollectionAssert.AreEquivalent(new[] { "dns", "doh" }, options.TraceComponents.ToArray());
    }

    [TestMethod]
    public void Parse_TraceConfigAll_TurnsOnEveryComponentUntilMinusAll()
    {
        Assert.IsTrue(Accept("--trace-config", "all").TraceComponents.Contains("all"));
        Assert.IsFalse(Accept("--trace-config", "all,-all").TraceComponents.Contains("all"));
    }

    [TestMethod]
    public void Parse_NoTraceConfig_TurnsOnNoComponent()
    {
        Assert.AreEqual(0, Accept("-v").TraceComponents.Count);
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
        CollectionAssert.AreEquivalent(expected, Accept(arguments).TraceComponents.ToArray());
    }

    [TestMethod]
    public void Parse_TraceConfigInFirstGroup_ReachesTheSecondGroup()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--trace-config", "ids,tls", Url, "--next", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Groups[1].TraceIds);
        Assert.IsTrue(result.Groups[1].TraceComponents.Contains("tls"));
    }

    [TestMethod]
    public void Parse_NoTraceConfigSpelling_IsRefused()
    {
        Assert.IsFalse(CommandLineParser.Parse(["--no-trace-config", Url]).IsAccepted);
    }

    private static CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }
}
