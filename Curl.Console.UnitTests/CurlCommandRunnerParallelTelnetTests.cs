using System.Text;

using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that under <c>-Z</c> a <c>telnet</c> session holds back every later transfer until it ends, as
/// curl 8.22.0 runs the whole session inside one call of its parallel loop (STD-183, BL-6017).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerParallelTelnetTests
{
    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);

    [TestMethod]
    public async Task RunAsync_TelnetThenHttpUnderParallel_StartsTheHttpTransferOnlyWhenTheSessionHasEnded()
    {
        TaskCompletionSource sessionStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource sessionEnds = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingProtocolHandler telnet = new("telnet", async _ =>
        {
            sessionStarted.SetResult();
            await sessionEnds.Task;
            return TransferResult.Success(0);
        });
        RecordingProtocolHandler http = new("http", async context =>
        {
            await context.Output.WriteAsync("hello\n"u8.ToArray(), context.CancellationToken);
            return TransferResult.Success(6);
        });

        Task<int> run = new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([telnet, http])),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(["-sS", "-Z", "-w", "[%{exitcode}]\n", "telnet://h.test", "--next", "-w", "[%{exitcode}]\n", "http://h.test/hello"]);
        await sessionStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        int startedDuringTheSession = http.Contexts.Count;
        sessionEnds.SetResult();
        int exitCode = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(0, startedDuringTheSession);
        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, http.Contexts);
        Assert.AreEqual("[0]\nhello\n[0]\n", StandardOutputText);
    }
}
