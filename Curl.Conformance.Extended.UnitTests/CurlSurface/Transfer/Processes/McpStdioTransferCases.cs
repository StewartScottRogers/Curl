using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.McpClient;
using Curl.Conformance.Extended.McpClient.Stdio;
using static Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Processes.ProcessTransferRun;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Processes;

/// <summary>
/// curl's transfer options for <c>mcp+stdio</c>, which starts the server program through the stand-in starter
/// (ADR-1031, ADR-1203). Expected values come from curl 8.22.0's manual page ("-v", "-o", "-O", "-w", "--fail",
/// "-m", "EXIT CODES") and, for a transfer with no connection, ADR-0119.
/// </summary>
[TestClass]
public sealed class McpStdioTransferCases
{
    private const string Url = "mcp+stdio://localhost/reference-server?--stdio";

    private const string Listing = "echo\tEchoes its text\n";

    private const string ServerExitedMessage = "The MCP server exited before it answered request 2.";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-McpStdio-1")]
    public async Task Verbose_NoConnectionLinesAndStandardOutputUnchanged()
    {
        CurlRunResult result = await RunAsync(Starter(new McpStdioServerBehaviour()), null, "-v", Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual(Listing, Text(result.StandardOutput));
        Assert.AreEqual(string.Empty, Text(result.StandardError));
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-McpStdio-1")]
    public async Task OutputFile_HoldsWhatStandardOutputWouldAndStandardOutputIsEmpty()
    {
        using OutputDirectory directory = new();
        string file = directory.PathOf("listing.txt");

        CurlRunResult result = await RunAsync(Starter(new McpStdioServerBehaviour()), null, "-o", file, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        Assert.IsEmpty(result.StandardError);
        Assert.AreEqual(Listing, File.ReadAllText(file));
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-McpStdio-1")]
    public async Task RemoteName_WritesTheFileNamedAfterTheLastPathSegment()
    {
        using OutputDirectory directory = new();

        CurlRunResult result = await RunAsync(Starter(new McpStdioServerBehaviour()), null, "--output-dir", directory.FullName, "-O", Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        Assert.AreEqual(Listing, File.ReadAllText(directory.PathOf("reference-server")));
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-McpStdio-1")]
    public async Task WriteOut_SuccessfulTransfer_PrintsTheUrlSchemeSizeAndNoConnection()
    {
        CurlRunResult result = await RunAsync(Starter(new McpStdioServerBehaviour()), null, "-w", WriteOut, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual($"{Listing}0||{Url}|{Url}|mcp+stdio||-1|21|0.000000", Text(result.StandardOutput));
        Assert.IsEmpty(result.StandardError);
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-McpStdio-2")]
    public async Task WriteOut_FailedTransfer_PrintsTheExitCodeAndErrorMessage()
    {
        CurlRunResult result = await RunAsync(Starter(new McpStdioServerBehaviour { ExitBeforeAnsweringMessage = 3 }), null, "-w", WriteOut, Url);

        Assert.AreEqual(8, result.ExitCode);
        Assert.AreEqual($"8|{ServerExitedMessage}|{Url}|{Url}|mcp+stdio||-1|0|0.000000", Text(result.StandardOutput));
        Assert.AreEqual($"curl: (8) {ServerExitedMessage}" + Environment.NewLine, Text(result.StandardError));
    }

    [TestMethod]
    [DataRow("--fail")]
    [DataRow("--fail-with-body")]
    [SpecRequirement("CurlSurface:Fail-McpStdio-1")]
    public async Task Fail_LeavesTheTransfersOwnExitCodeAndOutput(string option)
    {
        CurlRunResult succeeded = await RunAsync(Starter(new McpStdioServerBehaviour()), null, option, Url);
        CurlRunResult failed = await RunAsync(Starter(new McpStdioServerBehaviour { ExitBeforeAnsweringMessage = 3 }), null, option, Url);

        Assert.AreEqual(0, succeeded.ExitCode, Text(succeeded.StandardError));
        Assert.AreEqual(Listing, Text(succeeded.StandardOutput));
        Assert.AreEqual(8, failed.ExitCode);
        Assert.IsEmpty(failed.StandardOutput);
        Assert.AreEqual($"curl: (8) {ServerExitedMessage}" + Environment.NewLine, Text(failed.StandardError));
    }

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-McpStdio-1")]
    public async Task MaxTime_ServerNeverAnswers_Exit28AtTheDeadline()
    {
        CurlRunResult result = await RunUntilMaxTimeAsync(new SilentProcessStarter(new Dictionary<string, string>()), null, Url, TestContext.CancellationToken);

        Assert.AreEqual(28, result.ExitCode, Text(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        StringAssert.StartsWith(Text(result.StandardError), "curl: (28) ");
    }

    private static McpStdioReferenceProcessStarter Starter(McpStdioServerBehaviour behaviour) =>
        new(new McpReferenceToolServer([McpClient.McpClientRun.Echo]), behaviour);
}
