using System.Text;

namespace Curl.Mcp;

[TestClass]
public sealed class McpCurlToolTests
{
    private static readonly McpResponseCap Cap = new(64);

    [TestMethod]
    public async Task RunAsync_ExitZero_ReturnsStdoutStderrAndExitCode()
    {
        RecordingRunner runner = new(0, "body", "progress");

        McpCurlToolResult result = await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap)
            .RunAsync(["-s", "http://h/"], null, CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("body", result.StandardOutput!.Content);
        Assert.AreEqual("progress", result.StandardError!.Content);
        Assert.IsFalse(result.IsStandardOutputTruncated);
        Assert.IsFalse(result.IsStandardErrorTruncated);
    }

    [TestMethod]
    public async Task RunAsync_Exit22_GivesAToolErrorCarryingCurlsStderrMessage()
    {
        RecordingRunner runner = new(22, string.Empty, "curl: (22) The requested URL returned error: 404\n");

        McpCurlToolResult result = await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap)
            .RunAsync(["-f", "http://h/"], null, CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual(22, result.ExitCode);
        Assert.AreEqual(McpToolFailureKind.CurlExit, result.Failure!.Kind);
        StringAssert.Contains(result.Failure.Text, "curl: (22) The requested URL returned error: 404");
        Assert.AreEqual(22, result.Failure.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_NonzeroExitWithBinaryStderr_GivesTheExitCodeLineOnly()
    {
        RecordingRunner runner = new(7, string.Empty, string.Empty) { ErrorBytes = [0xFF, 0xFE] };

        McpCurlToolResult result = await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap)
            .RunAsync(["http://h/"], null, CancellationToken.None);

        Assert.IsTrue(result.StandardError!.IsBase64);
        Assert.IsTrue(result.Failure!.Text.StartsWith("curl exit code 7", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_PathOutsideRoot_NeverReachesTheRunnerAndNamesTheOption()
    {
        string folder = Directory.CreateTempSubdirectory("mcp-curl-tool-").FullName;
        try
        {
            RecordingRunner runner = new(0, string.Empty, string.Empty);
            string outside = Path.Combine(Path.GetDirectoryName(folder)!, "outside.txt");

            McpCurlToolResult result = await new McpCurlTool(runner, new McpConfinementRoot(folder), Cap)
                .RunAsync(["--output", outside, "http://h/"], null, CancellationToken.None);

            Assert.AreEqual(0, runner.Calls);
            Assert.AreEqual(McpToolFailureKind.PathRefused, result.Failure!.Kind);
            StringAssert.StartsWith(result.Failure.Text, "Refused --output:");
            Assert.IsNull(result.ExitCode);
            Assert.IsNull(result.StandardOutput);
            Assert.IsNull(result.StandardError);
            Assert.IsFalse(result.IsStandardOutputTruncated);
            Assert.IsFalse(result.IsStandardErrorTruncated);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunAsync_NoRootConfigured_RefusesFileArgumentsButRunsATransferToStdout()
    {
        RecordingRunner runner = new(0, "body", string.Empty);
        McpCurlTool tool = new(runner, McpConfinementRoot.NotConfigured, Cap);

        McpCurlToolResult refused = await tool.RunAsync(["-o", "x", "http://h/"], null, CancellationToken.None);
        Assert.AreEqual(0, runner.Calls);
        StringAssert.Contains(refused.Failure!.Text, "Refused -o:");
        StringAssert.Contains(refused.Failure.Text, "CURL_MCP_ROOT is not set");

        McpCurlToolResult ran = await tool.RunAsync(["-s", "http://h/"], null, CancellationToken.None);
        Assert.AreEqual(1, runner.Calls);
        Assert.AreEqual("body", ran.StandardOutput!.Content);
        Assert.IsNull(runner.WorkingDirectory);
    }

    [TestMethod]
    public async Task RunAsync_RootConfigured_PassesTheRootAsWorkingDirectoryWithoutChangingTheProcesses()
    {
        string folder = Directory.CreateTempSubdirectory("mcp-curl-tool-").FullName;
        try
        {
            string before = Environment.CurrentDirectory;
            McpConfinementRoot root = new(folder);
            RecordingRunner runner = new(0, string.Empty, string.Empty);

            await new McpCurlTool(runner, root, Cap).RunAsync(["-o", "inside.txt", "http://h/"], null, CancellationToken.None);

            Assert.AreEqual(root.ResolvedRoot, runner.WorkingDirectory);
            Assert.AreEqual(before, Environment.CurrentDirectory);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunAsync_LeadingCurl_IsPassedOnAsAUrlArgument()
    {
        RecordingRunner runner = new(6, string.Empty, "curl: (6) Could not resolve host: curl\n");

        McpCurlToolResult result = await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap)
            .RunAsync(["curl", "http://h/"], null, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "curl", "http://h/" }, runner.Arguments!.ToArray());
        Assert.AreEqual(6, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_StandardInput_IsEmptyUnlessGivenAndUtf8WhenGiven()
    {
        RecordingRunner runner = new(0, string.Empty, string.Empty);
        McpCurlTool tool = new(runner, McpConfinementRoot.NotConfigured, Cap);

        await tool.RunAsync(["http://h/"], null, CancellationToken.None);
        Assert.AreEqual(string.Empty, runner.StandardInput);

        await tool.RunAsync(["-d", "@-", "http://h/"], "naïve", CancellationToken.None);
        Assert.AreEqual("naïve", runner.StandardInput);
    }

    [TestMethod]
    public async Task RunAsync_OutputPastTheCap_IsTruncatedWithTheNote()
    {
        RecordingRunner runner = new(0, new string('a', 100), new string('e', 70));

        McpCurlToolResult result = await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap)
            .RunAsync(["http://h/"], null, CancellationToken.None);

        Assert.IsTrue(result.IsStandardOutputTruncated);
        Assert.IsTrue(result.IsStandardErrorTruncated);
        Assert.AreEqual(64, result.StandardOutput!.Content.Length);
        Assert.AreEqual(100, result.StandardOutput.TotalBytes);
        Assert.AreEqual("Output truncated: returned 64 of 100 bytes (CURL_MCP_MAX_RESPONSE_BYTES caps it).", result.StandardOutput.TruncationNote);
    }

    [TestMethod]
    public async Task RunAsync_RequestToken_ReachesTheRunner()
    {
        using CancellationTokenSource source = new();
        RecordingRunner runner = new(0, string.Empty, string.Empty);

        await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap).RunAsync(["http://h/"], null, source.Token);

        Assert.AreEqual(source.Token, runner.Token);
    }

    [TestMethod]
    public async Task RunAsync_TokenCancelledBefore_DoesNotRun()
    {
        using CancellationTokenSource source = new();
        await source.CancelAsync();
        RecordingRunner runner = new(0, string.Empty, string.Empty);

        McpCurlToolResult result = await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap).RunAsync(["http://h/"], null, source.Token);

        Assert.AreEqual(0, runner.Calls);
        Assert.AreSame(McpToolFailure.Cancelled, result.Failure);
    }

    [TestMethod]
    public async Task RunAsync_TokenCancelledDuringARunThatThrows_GivesTheCancelledError()
    {
        using CancellationTokenSource source = new();
        RecordingRunner runner = new(0, string.Empty, string.Empty) { OnRun = source.Cancel, ThrowWhenCancelled = true };

        McpCurlToolResult result = await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap).RunAsync(["http://h/"], null, source.Token);

        Assert.AreSame(McpToolFailure.Cancelled, result.Failure);
        Assert.IsNull(result.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_TokenCancelledDuringARunThatReturns_GivesTheCancelledError()
    {
        using CancellationTokenSource source = new();
        RecordingRunner runner = new(0, "partial", string.Empty) { OnRun = source.Cancel };

        McpCurlToolResult result = await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap).RunAsync(["http://h/"], null, source.Token);

        Assert.AreSame(McpToolFailure.Cancelled, result.Failure);
        Assert.IsNull(result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_OperationCanceledWithoutTheRequestToken_IsNotSwallowed()
    {
        RecordingRunner runner = new(0, string.Empty, string.Empty) { OnRun = () => throw new OperationCanceledException() };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap).RunAsync(["http://h/"], null, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("-s|http://a/|--next|-w|%{http_code}|http://b/")]
    [DataRow("--ai-help")]
    [DataRow("--ai-help|all")]
    [DataRow("--help|all")]
    public async Task RunAsync_NextWriteOutAndHelp_PassThroughUnchanged(string joined)
    {
        string[] arguments = joined.Split('|');
        string help = "# curl --ai-help\n\n| Option | Meaning |\n";
        RecordingRunner runner = new(0, help, string.Empty);

        McpCurlToolResult result = await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, new McpResponseCap(4096))
            .RunAsync(arguments, null, CancellationToken.None);

        Assert.IsFalse(result.IsError, result.Failure?.Text);
        CollectionAssert.AreEqual(arguments, runner.Arguments!.ToArray());
        Assert.AreEqual(help, result.StandardOutput!.Content);
    }

    [TestMethod]
    public async Task RunAsync_VeryLongArgumentList_ReachesTheRunnerWhole()
    {
        string[] arguments = [.. Enumerable.Range(0, 5000).SelectMany(index => new[] { "-H", $"X-Header-{index}: {index}" }), "http://h/"];
        RecordingRunner runner = new(0, string.Empty, string.Empty);

        McpCurlToolResult result = await new McpCurlTool(runner, McpConfinementRoot.NotConfigured, Cap)
            .RunAsync(arguments, null, CancellationToken.None);

        Assert.IsFalse(result.IsError, result.Failure?.Text);
        Assert.AreEqual(10001, runner.Arguments!.Count);
    }

    [TestMethod]
    public async Task RunAsync_NullArguments_Throws()
    {
        McpCurlTool tool = new(new RecordingRunner(0, string.Empty, string.Empty), McpConfinementRoot.NotConfigured, Cap);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => tool.RunAsync(null!, null, CancellationToken.None));
    }

    private sealed class RecordingRunner(int exitCode, string output, string error) : ICurlCommandRunner
    {
        public int Calls { get; private set; }

        public IReadOnlyList<string>? Arguments { get; private set; }

        public string? StandardInput { get; private set; }

        public string? WorkingDirectory { get; private set; }

        public CancellationToken Token { get; private set; }

        public byte[]? ErrorBytes { get; init; }

        public Action? OnRun { get; init; }

        public bool ThrowWhenCancelled { get; init; }

        public async Task<int> RunAsync(
            IReadOnlyList<string> arguments,
            Stream standardInput,
            Stream standardOutput,
            Stream standardError,
            string? workingDirectory,
            CancellationToken cancellationToken)
        {
            Calls++;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            Token = cancellationToken;
            using StreamReader reader = new(standardInput, Encoding.UTF8, leaveOpen: true);
            StandardInput = await reader.ReadToEndAsync(cancellationToken);
            await standardOutput.WriteAsync(Encoding.UTF8.GetBytes(output), cancellationToken);
            await standardError.WriteAsync(ErrorBytes ?? Encoding.UTF8.GetBytes(error), cancellationToken);
            OnRun?.Invoke();
            if (ThrowWhenCancelled)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return exitCode;
        }
    }
}
