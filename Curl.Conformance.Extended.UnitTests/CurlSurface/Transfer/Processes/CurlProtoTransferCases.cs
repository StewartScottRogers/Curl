using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Plugin;
using Curl.Conformance.Extended.UnitTests.Plugin;
using static Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Processes.ProcessTransferRun;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Processes;

/// <summary>
/// curl's transfer options for a <c>curl-proto-&lt;scheme&gt;</c> helper scheme (<c>demo://</c> served by
/// <c>curl-proto-demo</c>), started through the stand-in starter (ADR-1142, ADR-1203). Expected values come from
/// curl 8.22.0's manual page ("-v", "-o", "-O", "-w", "--fail", "-m", "EXIT CODES") and, for a transfer with no
/// connection, ADR-0119.
/// </summary>
[TestClass]
public sealed class CurlProtoTransferCases
{
    private const string Url = PluginRun.Url;

    private const string Body = "body\n";

    public TestContext TestContext { get; set; } = null!;

    private static string ErrorMessage => $"{PluginRun.DemoName}: no such item";

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-CurlProto-1")]
    public async Task Verbose_NoConnectionLinesAndStandardOutputUnchanged()
    {
        CurlRunResult result = await RunDemoAsync(Answering(), "-v", Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual(Body, Text(result.StandardOutput));
        Assert.AreEqual(string.Empty, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-CurlProto-1")]
    public async Task OutputFile_HoldsWhatStandardOutputWouldAndStandardOutputIsEmpty()
    {
        using OutputDirectory directory = new();
        string file = directory.PathOf("item.txt");

        CurlRunResult result = await RunDemoAsync(Answering(), "-o", file, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        Assert.IsEmpty(result.StandardError);
        Assert.AreEqual(Body, File.ReadAllText(file));
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-CurlProto-1")]
    public async Task RemoteName_WritesTheFileNamedAfterTheLastPathSegment()
    {
        using OutputDirectory directory = new();

        CurlRunResult result = await RunDemoAsync(Answering(), "--output-dir", directory.FullName, "-O", Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        Assert.AreEqual(Body, File.ReadAllText(directory.PathOf("item")));
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-CurlProto-1")]
    public async Task WriteOut_SuccessfulTransfer_PrintsTheUrlSchemeSizeAndNoConnection()
    {
        CurlRunResult result = await RunDemoAsync(Answering(), "-w", WriteOut, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual($"{Body}0||{Url}|{Url}|demo||-1|5|0.000000", Text(result.StandardOutput));
        Assert.IsEmpty(result.StandardError);
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-CurlProto-2")]
    public async Task WriteOut_FailedTransfer_PrintsTheExitCodeAndErrorMessage()
    {
        CurlRunResult result = await RunDemoAsync(Refusing(), "-w", WriteOut, Url);

        Assert.AreEqual(56, result.ExitCode);
        Assert.AreEqual($"56|{ErrorMessage}|{Url}|{Url}|demo||-1|0|0.000000", Text(result.StandardOutput));
        Assert.AreEqual($"curl: (56) {ErrorMessage}" + Environment.NewLine, Text(result.StandardError));
    }

    [TestMethod]
    [DataRow("--fail")]
    [DataRow("--fail-with-body")]
    [SpecRequirement("CurlSurface:Fail-CurlProto-1")]
    public async Task Fail_LeavesTheTransfersOwnExitCodeAndOutput(string option)
    {
        CurlRunResult succeeded = await RunDemoAsync(Answering(), option, Url);
        CurlRunResult failed = await RunDemoAsync(Refusing(), option, Url);

        Assert.AreEqual(0, succeeded.ExitCode, Text(succeeded.StandardError));
        Assert.AreEqual(Body, Text(succeeded.StandardOutput));
        Assert.AreEqual(56, failed.ExitCode);
        Assert.IsEmpty(failed.StandardOutput);
        Assert.AreEqual($"curl: (56) {ErrorMessage}" + Environment.NewLine, Text(failed.StandardError));
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-CurlProto-1")]
    public async Task MaxTime_HelperNeverAnswers_Exit28AtTheDeadline()
    {
        using PluginRun.HelperDirectory helpers = new();
        helpers.Add(PluginRun.DemoName);

        CurlRunResult result = await RunUntilMaxTimeAsync(new SilentProcessStarter(PluginRun.PathOf(helpers.FullName)), null, Url, TestContext.CancellationToken);

        Assert.AreEqual(28, result.ExitCode, Text(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        StringAssert.StartsWith(Text(result.StandardError), "curl: (28) ");
    }

    private static PluginReferenceHelper Answering() => PluginReferenceHelper.Answering(["get"], "ok", Body);

    private static PluginReferenceHelper Refusing() => PluginReferenceHelper.Answering(["get"], "error no such item");

    /// <summary>Runs <paramref name="arguments" /> with one directory on <c>PATH</c> holding <c>curl-proto-demo</c>, answered by <paramref name="helper" />.</summary>
    private static async Task<CurlRunResult> RunDemoAsync(PluginReferenceHelper helper, params string[] arguments)
    {
        using PluginRun.HelperDirectory helpers = new();
        helpers.Add(PluginRun.DemoName);
        PluginReferenceHelperStarter starter = new(PluginRun.PathOf(helpers.FullName), helper);
        return await RunAsync(starter, null, arguments);
    }
}
