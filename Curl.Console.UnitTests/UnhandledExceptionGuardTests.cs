using System.Text;

namespace Curl.Console;

[TestClass]
public sealed class UnhandledExceptionGuardTests
{
    [TestMethod]
    public async Task RunAsync_WhenTheRunEnds_ReturnsItsExitCodeAndWritesNothing()
    {
        using var standardOutput = new MemoryStream();
        using var standardError = new MemoryStream();

        var exitCode = await UnhandledExceptionGuard.RunAsync(() => Task.FromResult(7), standardOutput, standardError);

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_WhenATransferThrows_WritesOneCurlLineAndReturnsExit2()
    {
        using var standardOutput = new FlushCountingStream();
        using var standardError = new MemoryStream();

        var exitCode = await UnhandledExceptionGuard.RunAsync(
            () => throw new InvalidOperationException("handler broke"), standardOutput, standardError);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("curl: (2) handler broke\n", Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual(1, standardOutput.Flushes);
    }

    [TestMethod]
    public async Task RunAsync_WhenStandardOutputCannotBeFlushed_StillWritesTheCurlLine()
    {
        using var standardOutput = new FlushCountingStream { FailsFlush = true };
        using var standardError = new MemoryStream();

        var exitCode = await UnhandledExceptionGuard.RunAsync(
            () => throw new InvalidOperationException("handler broke"), standardOutput, standardError);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("curl: (2) handler broke\n", Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_WhenTheRunIsCancelled_LetsTheCancellationEscape()
    {
        using var standardOutput = new MemoryStream();
        using var standardError = new MemoryStream();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => UnhandledExceptionGuard.RunAsync(() => throw new OperationCanceledException(), standardOutput, standardError));

        Assert.AreEqual(0, standardError.Length);
    }

    private sealed class FlushCountingStream : MemoryStream
    {
        public bool FailsFlush { get; init; }

        public int Flushes { get; private set; }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Flushes++;
            return FailsFlush ? Task.FromException(new IOException("The pipe has been ended.")) : Task.CompletedTask;
        }
    }
}
