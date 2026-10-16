namespace Curl.Mcp;

[TestClass]
public sealed class McpToolRunnerTests
{
    private static readonly McpToolRunner Runner = new(new McpResponseCap(4));

    [TestMethod]
    public async Task RunAsync_TokenCancelledBefore_GivesTheCancelledErrorWithoutRunning()
    {
        using CancellationTokenSource source = new();
        await source.CancelAsync();
        bool ran = false;

        McpToolResult result = await Runner.RunAsync(
            _ =>
            {
                ran = true;
                return Task.FromResult(new McpCurlRunOutput(0, [1], string.Empty));
            },
            source.Token);

        Assert.IsFalse(ran);
        Assert.IsTrue(result.IsError);
        Assert.AreSame(McpToolFailure.Cancelled, result.Failure);
        Assert.IsNull(result.Output);
    }

    [TestMethod]
    public async Task RunAsync_TokenCancelledDuringARunThatThrows_GivesTheCancelledErrorAndNoOutput()
    {
        using CancellationTokenSource source = new();

        McpToolResult result = await Runner.RunAsync(
            async token =>
            {
                await source.CancelAsync();
                token.ThrowIfCancellationRequested();
                return new McpCurlRunOutput(0, [1], string.Empty);
            },
            source.Token);

        Assert.AreSame(McpToolFailure.Cancelled, result.Failure);
        Assert.IsNull(result.Output);
    }

    [TestMethod]
    public async Task RunAsync_TokenCancelledDuringARunThatReturnsPartialOutput_GivesNoOutput()
    {
        using CancellationTokenSource source = new();

        McpToolResult result = await Runner.RunAsync(
            async _ =>
            {
                await source.CancelAsync();
                return new McpCurlRunOutput(0, "partial output past the cap"u8.ToArray(), string.Empty);
            },
            source.Token);

        Assert.AreSame(McpToolFailure.Cancelled, result.Failure);
        Assert.IsNull(result.Output);
    }

    [TestMethod]
    public async Task RunAsync_RunCancelledByItsOwnToken_LetsTheExceptionThrough()
    {
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Runner.RunAsync(
            _ => throw new OperationCanceledException(),
            CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_ExitZero_GivesTheCappedOutput()
    {
        McpToolResult result = await Runner.RunAsync(
            _ => Task.FromResult(new McpCurlRunOutput(0, "abcdef"u8.ToArray(), string.Empty)),
            CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.IsNotNull(result.Output);
        Assert.AreEqual("abcd", result.Output.Content);
        Assert.IsTrue(result.Output.IsTruncated);
    }

    [TestMethod]
    public async Task RunAsync_NonzeroExit_GivesTheToolErrorAndNoOutput()
    {
        McpToolResult result = await Runner.RunAsync(
            _ => Task.FromResult(new McpCurlRunOutput(7, "x"u8.ToArray(), "curl: (7) Failed to connect")),
            CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.IsNull(result.Output);
        Assert.AreEqual("curl: (7) Failed to connect\ncurl exit code 7 (CouldntConnect)", result.Failure?.Text);
    }

    [TestMethod]
    public async Task RunAsync_NoRun_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Runner.RunAsync(null!, CancellationToken.None));
    }
}
