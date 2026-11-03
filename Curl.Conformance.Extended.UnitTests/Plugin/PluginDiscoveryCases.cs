using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Plugin;
using static Curl.Conformance.Extended.UnitTests.Plugin.PluginRun;

namespace Curl.Conformance.Extended.UnitTests.Plugin;

/// <summary>ADR-1142 "Decision": finding a helper on <c>PATH</c>, the schemes it never takes, and <c>CURL_EXTENSIONS=0</c>.</summary>
[TestClass]
public sealed class PluginDiscoveryCases
{
    private static readonly string NotSupported = "curl: (1) Protocol \"demo\" not supported" + Environment.NewLine;

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Discovery-1")]
    [SpecRequirement("Plugin:ADR1142-Discovery-4")]
    [SpecRequirement("Plugin:ADR1142-Discovery-5")]
    [SpecRequirement("Plugin:ADR1142-Invocation-1")]
    public async Task FirstPathDirectoryWins_EmptyAndMissingEntriesSkipped()
    {
        using HelperDirectory first = new();
        using HelperDirectory second = new();
        string winner = first.Add(DemoName);
        second.Add(DemoName);
        string missing = Path.Combine(first.FullName, "missing");
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["get"], "ok", "body");
        PluginReferenceHelperStarter starter = new(PathOf(string.Empty, missing, first.FullName, second.FullName), helper);

        CurlRunResult result = await RunAsync(starter, null, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual(1, starter.Starts.Count);
        Assert.AreEqual(winner, starter.Starts[0].Program);
        CollectionAssert.AreEqual(new[] { Url }, starter.Starts[0].Arguments.ToArray());
        Assert.AreEqual("body", Text(result.StandardOutput));
        Assert.AreEqual(string.Empty, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Discovery-2")]
    public async Task SchemeWithUppercaseLettersDigitsAndPunctuation_IsServedLowercased()
    {
        using HelperDirectory directory = new();
        string program = directory.Add(ProgramFileName("Demo2+x-y.z"));
        PluginReferenceHelperStarter starter = new(PathOf(directory.FullName), PluginReferenceHelper.Answering(["get"], "ok", "body"));

        CurlRunResult result = await RunAsync(starter, null, "demo2+x-y.z://example/item");

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual(program, starter.Starts[0].Program);
        Assert.AreEqual("body", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Discovery-2")]
    public async Task NameAfterThePrefixIsNoScheme_ServesNothing()
    {
        using HelperDirectory directory = new();
        directory.Add(ProgramFileName("1demo"));
        PluginReferenceHelperStarter starter = new(PathOf(directory.FullName), PluginReferenceHelper.Answering(["get"], "ok"));

        CurlRunResult result = await RunAsync(starter, null, "1demo://example/item");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.AreEqual(0, starter.Starts.Count);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SpecRequirement("Plugin:ADR1142-Discovery-3")]
    public async Task Windows_PrefixMatchesWithoutCaseAndPathextExtensionIsDropped()
    {
        using HelperDirectory directory = new();
        string program = directory.Add("CURL-PROTO-demo.Exe");
        directory.Add("curl-proto-other");
        PluginReferenceHelperStarter starter = new(PathOf(directory.FullName), PluginReferenceHelper.Answering(["get"], "ok", "body"));

        CurlRunResult served = await RunAsync(starter, null, Url);
        CurlRunResult unserved = await RunAsync(starter, null, "other://example/item");

        Assert.AreEqual(0, served.ExitCode, Text(served.StandardError));
        Assert.AreEqual(program, starter.Starts[0].Program);
        Assert.AreEqual(1, unserved.ExitCode, "A name without a PATHEXT extension is no helper on Windows.");
        Assert.AreEqual(1, starter.Starts.Count);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SpecRequirement("Plugin:ADR1142-Discovery-3")]
    public async Task Windows_PathextNamesTheExtensions()
    {
        using HelperDirectory directory = new();
        directory.Add("curl-proto-demo.cmd");
        Dictionary<string, string> environment = PathOf(directory.FullName);
        environment["PATHEXT"] = ".EXE";
        PluginReferenceHelperStarter starter = new(environment, PluginReferenceHelper.Answering(["get"], "ok"));

        CurlRunResult result = await RunAsync(starter, null, Url);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(NotSupported, Text(result.StandardError));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [SpecRequirement("Plugin:ADR1142-Discovery-3")]
    public async Task Posix_PrefixMatchesWithCaseAndTheWholeRestIsTheScheme()
    {
        using HelperDirectory directory = new();
        directory.Add("CURL-PROTO-demo");
        string program = directory.Add("curl-proto-other.cmd");
        PluginReferenceHelperStarter starter = new(PathOf(directory.FullName), PluginReferenceHelper.Answering(["get"], "ok", "body"));

        CurlRunResult unserved = await RunAsync(starter, null, Url);
        CurlRunResult served = await RunAsync(starter, null, "other.cmd://example/item");

        Assert.AreEqual(1, unserved.ExitCode, "The prefix matches with case.");
        Assert.AreEqual(0, served.ExitCode, Text(served.StandardError));
        Assert.AreEqual(program, starter.Starts.Single().Program);
    }

    [TestMethod]
    [DataRow("http", "http://localhost:9/")]
    [DataRow("mcp", "mcp://localhost:9/mcp")]
    [SpecRequirement("Plugin:ADR1142-Reserved-1")]
    [SpecRequirement("Plugin:ADR1142-Reserved-2")]
    public async Task HelperForCurlsOrAnotherExtensionsScheme_IsIgnored(string scheme, string url)
    {
        using HelperDirectory directory = new();
        directory.Add(ProgramFileName(scheme));
        PluginReferenceHelperStarter starter = new(PathOf(directory.FullName), PluginReferenceHelper.Answering(["get"], "ok", "helper"));

        CurlRunResult result = await RunAsync(starter, null, "-m", "30", url);

        Assert.AreEqual(0, starter.Starts.Count);
        Assert.AreNotEqual("helper", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Reserved-3")]
    public async Task NoHelperForTheScheme_Exit1ProtocolNotSupported()
    {
        using HelperDirectory directory = new();
        PluginReferenceHelperStarter starter = new(PathOf(directory.FullName), PluginReferenceHelper.Answering(["get"], "ok"));

        CurlRunResult result = await RunAsync(starter, null, Url);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(NotSupported, Text(result.StandardError));
        Assert.AreEqual(0, starter.Starts.Count);
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Reserved-4")]
    public async Task CurlExtensionsZero_Exit1ProtocolNotSupported()
    {
        using HelperDirectory directory = new();
        directory.Add(DemoName);
        PluginReferenceHelperStarter starter = new(PathOf(directory.FullName), PluginReferenceHelper.Answering(["get"], "ok"));

        CurlRunResult result = await RunAsync(starter, new Dictionary<string, string> { ["CURL_EXTENSIONS"] = "0" }, Url);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(NotSupported, Text(result.StandardError));
        Assert.AreEqual(0, starter.Starts.Count);
    }
}
