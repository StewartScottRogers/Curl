using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Plugin;
using static Curl.Conformance.Extended.UnitTests.Plugin.PluginRun;

namespace Curl.Conformance.Extended.UnitTests.Plugin;

/// <summary>ADR-1142 "Decision": the <c>capabilities</c> exchange and the <c>get</c> and <c>put</c> commands.</summary>
[TestClass]
public sealed class PluginExchangeCases
{
    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Capabilities-1")]
    [SpecRequirement("Plugin:ADR1142-Commands-1")]
    [SpecRequirement("Plugin:ADR1142-Commands-3")]
    [SpecRequirement("Plugin:ADR1142-Reply-1")]
    public async Task Get_WritesCapabilitiesThenGetAndWritesTheBody()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["get", "put"], "ok", "line one\nline two\n");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual($"capabilities\nget {Url}\n", Text(helper.Received));
        Assert.AreEqual("line one\nline two\n", Text(result.StandardOutput));
        Assert.AreEqual(string.Empty, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Commands-2")]
    [SpecRequirement("Plugin:ADR1142-Commands-3")]
    public async Task PutWithData_WritesPutThenTheData()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["put"], "ok", "stored");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, "-d", "a=b&c", Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual($"capabilities\nput {Url}\na=b&c", Text(helper.Received));
        Assert.AreEqual("stored", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Commands-2")]
    public async Task PutWithUploadFile_WritesPutThenTheFile()
    {
        using HelperDirectory files = new();
        string upload = Path.Combine(files.FullName, "upload.bin");
        File.WriteAllBytes(upload, Encoding.UTF8.GetBytes("file\r\ncontent"));
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["get", "put"], "ok");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, "-T", upload, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual($"capabilities\nput {Url}\nfile\r\ncontent", Text(helper.Received));
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Capabilities-2")]
    public async Task UnknownOptionalCapability_IsIgnored()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["option", "get", "connect"], "ok", "body");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("body", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Capabilities-3")]
    public async Task UnknownMandatoryCapability_Exit1BeforeAnyCommand()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["*get", "*connect"], "ok", "body");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("capabilities\n", Text(helper.Received));
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual($"curl: (1) {DemoName} requires the unknown capability connect" + Environment.NewLine, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("Plugin:ADR1142-Capabilities-3")]
    public async Task KnownMandatoryCapability_IsAccepted()
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering(["*get", "*put"], "ok", "body");

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("body", Text(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("get", "put", DisplayName = "A get without the get capability")]
    [DataRow("put", "get", DisplayName = "A put without the put capability")]
    [SpecRequirement("Plugin:ADR1142-Commands-4")]
    public async Task MissingCapabilityTheTransferNeeds_Exit1(string command, string offered)
    {
        PluginReferenceHelper helper = PluginReferenceHelper.Answering([offered], "ok", "body");
        string[] arguments = command == "put" ? ["-d", "x", Url] : [Url];

        (CurlRunResult result, _, _) = await RunDemoAsync(helper, arguments);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("capabilities\n", Text(helper.Received));
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual($"curl: (1) {DemoName} does not offer the {command} capability" + Environment.NewLine, Text(result.StandardError));
    }
}
