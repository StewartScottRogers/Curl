using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Plugin;

/// <summary>
/// Checks the conversation with a helper against the protocol of ADR-1142, git's remote-helper model
/// (gitremote-helpers) adapted, with recorded helper output and no process.
/// </summary>
[TestClass]
public sealed class HelperProtocolHandlerTests
{
    private const string Program = "/bin/curl-proto-demo";

    [TestMethod]
    public void SupportedSchemes_AreTheSchemesHelpersWereFoundFor() =>
        CollectionAssert.AreEquivalent(new[] { "demo", "other" }, HandlerOver(null, ("other", "/bin/curl-proto-other")).SupportedSchemes.ToArray());

    [TestMethod]
    public async Task Execute_WithoutContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await HandlerOver(null).ExecuteAsync(null!));

    // gitremote-helpers, "INVOCATION": the helper is run with the URL as its argument.
    [TestMethod]
    public async Task Execute_StartsTheSchemesProgramWithTheUrlAsTyped()
    {
        ScriptedHelperProcessStarter starter = new(new ScriptedHelperProcess("get\n\nok\n"));

        await new HelperProtocolHandler(Programs(), starter).ExecuteAsync(Context("DEMO://host/a%20b"));

        Assert.AreEqual(Program, starter.StartedProgram);
        Assert.AreEqual("DEMO://host/a%20b", starter.StartedUrl);
    }

    [TestMethod]
    public async Task Execute_WhenTheProgramCannotStart_FailsWithExit7()
    {
        TransferResult result = await HandlerOver(null).ExecuteAsync(Context("demo://host/a"));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to run curl-proto-demo: The system cannot find the file specified.", result.ErrorMessage);
    }

    // "Capabilities" and "Commands": capabilities first, then one get with the URL, then the end of input.
    [TestMethod]
    public async Task Execute_Get_SendsCapabilitiesThenGetAndWritesTheBodyAfterOk()
    {
        ScriptedHelperProcess helper = new("option\nget\n\nok\nhello\nworld");
        MemoryStream output = new();

        TransferResult result = await HandlerOver(helper).ExecuteAsync(Context("demo://host/a", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(11, result.BytesTransferred);
        Assert.AreEqual("capabilities\nget demo://host/a\n", helper.Received);
        Assert.AreEqual("hello\nworld", System.Text.Encoding.UTF8.GetString(output.ToArray()));
        Assert.IsTrue(helper.IsDisposed);
    }

    [TestMethod]
    public async Task Execute_HelperLinesEndingInCrLf_AreReadAsLines()
    {
        MemoryStream output = new();

        TransferResult result = await HandlerOver(new ScriptedHelperProcess("get\r\n\r\nok\r\nbody\r\n")).ExecuteAsync(Context("demo://host/a", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("body\r\n", System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task Execute_UploadFile_SendsPutAndTheFileThenWritesTheReplyBody()
    {
        ScriptedHelperProcess helper = new("get\nput\n\nok\nstored");
        MemoryStream output = new();
        var context = new TransferContext { Url = CurlUrl.Parse("demo://host/a"), Output = output, Upload = new MemoryStream("file bytes"u8.ToArray()) };

        TransferResult result = await HandlerOver(helper).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("capabilities\nput demo://host/a\nfile bytes", helper.Received);
        Assert.AreEqual("stored", System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task Execute_PostData_SendsPutAndTheData()
    {
        ScriptedHelperProcess helper = new("put\n\nok\n");
        var context = new TransferContext { Url = CurlUrl.Parse("demo://host/a"), Output = new MemoryStream(), PostData = "a=1"u8.ToArray() };

        TransferResult result = await HandlerOver(helper).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("capabilities\nput demo://host/a\na=1", helper.Received);
    }

    [TestMethod]
    [DataRow("put\n\nok\n", "curl-proto-demo does not offer the get capability")]
    [DataRow("", "curl-proto-demo does not offer the get capability")]
    [DataRow("get\n*connect\n\nok\n", "curl-proto-demo requires the unknown capability connect")]
    public async Task Execute_WithoutTheCapabilityTheTransferNeeds_FailsWithExit1(string reply, string message)
    {
        TransferResult result = await HandlerOver(new ScriptedHelperProcess(reply)).ExecuteAsync(Context("demo://host/a"));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    // "Capabilities": a mandatory capability Curl knows is accepted like an optional one.
    [TestMethod]
    public async Task Execute_MandatoryKnownCapability_IsAccepted() =>
        Assert.AreEqual(CurlExitCode.Ok, (await HandlerOver(new ScriptedHelperProcess("*get\n*put\n\nok\n")).ExecuteAsync(Context("demo://host/a"))).ExitCode);

    [TestMethod]
    [DataRow("get\n\nerror no such object\n", "curl-proto-demo: no such object")]
    [DataRow("get\n\nerror\n", "curl-proto-demo: error")]
    public async Task Execute_ErrorReply_FailsWithExit56AndTheHelpersMessage(string reply, string message)
    {
        TransferResult result = await HandlerOver(new ScriptedHelperProcess(reply)).ExecuteAsync(Context("demo://host/a"));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("get\n\n", "curl-proto-demo closed its output without a reply")]
    [DataRow("get\n\nyes\n", "curl-proto-demo replied \"yes\"; expected ok or error")]
    [DataRow("get\n\nerrors\n", "curl-proto-demo replied \"errors\"; expected ok or error")]
    public async Task Execute_MalformedReply_FailsWithExit8(string reply, string message)
    {
        TransferResult result = await HandlerOver(new ScriptedHelperProcess(reply)).ExecuteAsync(Context("demo://host/a"));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task Execute_OkEndingTheOutputWithoutANewline_IsAnEmptyBody()
    {
        TransferResult result = await HandlerOver(new ScriptedHelperProcess("get\n\nok")).ExecuteAsync(Context("demo://host/a"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, result.BytesTransferred);
    }

    [TestMethod]
    public async Task Execute_LineLongerThanTheLimit_FailsWithExit8()
    {
        string reply = new('a', HelperExchange.MaximumLineLength + 1);

        TransferResult result = await HandlerOver(new ScriptedHelperProcess(reply)).ExecuteAsync(Context("demo://host/a"));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual($"curl-proto-demo sent a line longer than {HelperExchange.MaximumLineLength} bytes", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Execute_HelperExitingWithAFailureStatus_FailsWithExit56AfterTheBody()
    {
        TransferResult result = await HandlerOver(new ScriptedHelperProcess("get\n\nok\nabc", exitStatus: 3)).ExecuteAsync(Context("demo://host/a"));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("curl-proto-demo exited with status 3", result.ErrorMessage);
        Assert.AreEqual(3, result.BytesTransferred);
    }

    [TestMethod]
    public async Task Execute_BrokenPipe_FailsWithExit56()
    {
        TransferResult result = await HandlerOver(new BrokenPipeHelperProcess()).ExecuteAsync(Context("demo://host/a"));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Failure talking to curl-proto-demo: Broken pipe", result.ErrorMessage);
    }

    private static Dictionary<string, string> Programs(params (string Scheme, string Program)[] more)
    {
        Dictionary<string, string> programs = new() { ["demo"] = Program };
        foreach ((string scheme, string program) in more)
        {
            programs[scheme] = program;
        }

        return programs;
    }

    private static HelperProtocolHandler HandlerOver(IHelperProcess? helper, params (string Scheme, string Program)[] more) =>
        new(Programs(more), new ScriptedHelperProcessStarter(helper));

    private static TransferContext Context(string url, Stream? output = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output ?? new MemoryStream() };

    private sealed class BrokenPipeHelperProcess : IHelperProcess
    {
        public Stream Input { get; } = new BrokenPipeStream();

        public Stream Output { get; } = new MemoryStream();

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BrokenPipeStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("Broken pipe");
    }
}
