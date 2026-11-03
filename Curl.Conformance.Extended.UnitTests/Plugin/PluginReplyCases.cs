using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Plugin;
using static Curl.Conformance.Extended.UnitTests.Plugin.PluginRun;

namespace Curl.Conformance.Extended.UnitTests.Plugin;

/// <summary>ADR-1142 "Decision": the reply, line endings and the line limit, and the helper's exit status.</summary>
[TestClass]
public sealed class PluginReplyCases
{
    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Reply-2")]
    public async Task ErrorReply_Exit56WithTheMessage()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["get"], "error no such item", "ignored");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(56, result.ExitCode);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual($"curl: (56) {DemoName}: no such item" + Environment.NewLine, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Reply-3")]
    public async Task ReplyNeitherOkNorError_Exit8()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["get"], "maybe", "body");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(8, result.ExitCode);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual($"curl: (8) {DemoName} replied \"maybe\"; expected ok or error" + Environment.NewLine, Text(result.StandardError));
    }

    [TestMethod]
    [DataRow("get\n\n", DisplayName = "Output ends before the reply")]
    [DataRow("get\n", DisplayName = "Output ends inside the capabilities")]
    [SpecRequirement("Plugin:ADR1142-Reply-3")]
    public async Task OutputEndsBeforeTheReply_Exit8(string output)
    {
        PluginReferenceHelper helper = new(Encoding.UTF8.GetBytes(output));

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(8, result.ExitCode);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual($"curl: (8) {DemoName} closed its output without a reply" + Environment.NewLine, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Reply-4")]
    public async Task CrlfLines_AreReadAsLfLines()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["get"], "ok", "body\r\n", lineEnd: "\r\n");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("body\r\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Reply-5")]
    public async Task LineOf8192Bytes_IsAccepted()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering([new string('x', 8192), "get"], "ok", "body");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("body", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Reply-5")]
    public async Task LineOf8193Bytes_Exit8()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering([new string('x', 8193), "get"], "ok", "body");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(8, result.ExitCode);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual($"curl: (8) {DemoName} sent a line longer than 8192 bytes" + Environment.NewLine, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Exit-1")]
    public async Task HelperExitsNonZeroAfterItsBody_Exit56()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["get"], "ok", "partial", exitStatus: 3);

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(56, result.ExitCode);
        Assert.AreEqual("partial", Text(result.StandardOutput));
        Assert.AreEqual($"curl: (56) {DemoName} exited with status 3" + Environment.NewLine, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Exit-2")]
    public async Task HelperThatCannotStart_Exit7()
    {
        (CurlRunResult result, PluginReferenceHelperStarter starter, string program) = await RunDemoAsync(null, Url);

        Assert.AreEqual(7, result.ExitCode);
        Assert.AreEqual(program, starter.Starts.Single().Program);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual($"curl: (7) Failed to run {DemoName}: {PluginReferenceHelperStarter.StartFailure}" + Environment.NewLine, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Exit-3")]
    public async Task PipeBreaksMidConversation_Exit56()
    {
        PluginReferenceHelper helper = new(Encoding.UTF8.GetBytes("get\n\nok\n"), inputBreaks: true);

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(56, result.ExitCode);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual($"curl: (56) Failure talking to {DemoName}: The pipe has been ended." + Environment.NewLine, Text(result.StandardError));
    }
}
