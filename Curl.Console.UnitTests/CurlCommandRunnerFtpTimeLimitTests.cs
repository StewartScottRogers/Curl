using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>-m</c> and <c>--connect-timeout</c> ending an <c>ftp://</c> transfer end to end
/// (BL-512): the production handler set over a connector whose server answers from a script
/// and then goes silent, on a <see cref="SteppingTimeProvider" /> so no second is real.
/// Measured on 2026-09-28 with curl 8.21.0 (mingw, Schannel) through
/// <c>Record-CurlExchange.ps1 -Ftp</c> (BL-512 Notes): every stall after the TCP connect -
/// before the greeting, after <c>USER</c>, after <c>EPSV</c> or <c>PASV</c>, while the data
/// connection connects, and while an active-mode data connection is awaited - ended with exit
/// 28 and <c>Operation timed out after N milliseconds with 0 bytes received</c>, and a stall
/// mid-<c>RETR</c> after 5 of the 100 bytes <c>SIZE</c> announced with <c>with 5 out of 100
/// bytes received</c>.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerFtpTimeLimitTests
{
    private const string Greeting = "220 Recorder ready\r\n";

    private const string LoggedIn = Greeting + "331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly SteppingTimeProvider clock = new();

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_NoGreetingAtMaxTime_EndsWithTheOperationMessage()
    {
        ScriptedFtpConnector connector = new([]);

        int exitCode = await RunUntilStalledAsync(connector, TimeSpan.FromSeconds(1), "-sS", "-m", "1", "ftp://127.0.0.1/f.txt");

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual("curl: (28) Operation timed out after 1000 milliseconds with 0 bytes received" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_UserUnansweredAtTheConnectTimeout_EndsWithTheOperationMessage()
    {
        ScriptedFtpConnector connector = new([Greeting]);

        int exitCode = await RunUntilStalledAsync(connector, TimeSpan.FromSeconds(1), "-sS", "--connect-timeout", "1", "ftp://127.0.0.1/f.txt");

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual("curl: (28) Operation timed out after 1000 milliseconds with 0 bytes received" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_EpsvUnansweredAtMaxTime_EndsWithTheOperationMessage()
    {
        ScriptedFtpConnector connector = new([LoggedIn]);

        int exitCode = await RunUntilStalledAsync(connector, TimeSpan.FromSeconds(1), "-sS", "-m", "1", "ftp://127.0.0.1/f.txt");

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual("curl: (28) Operation timed out after 1000 milliseconds with 0 bytes received" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DataConnectStillRunningAtMaxTime_EndsWithTheOperationMessage()
    {
        ScriptedFtpConnector connector = new([LoggedIn + "227 Entering Passive Mode (127,0,0,1,4,1)\r\n"], stallDataConnect: true);

        int exitCode = await RunUntilStalledAsync(connector, TimeSpan.FromSeconds(1), "-sS", "--disable-epsv", "-m", "1", "ftp://127.0.0.1/f.txt");

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual("curl: (28) Operation timed out after 1000 milliseconds with 0 bytes received" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RetrieveStalledMidwayAtMaxTime_EndsWithTheBytesReceivedOutOfTheSize()
    {
        ScriptedFtpConnector connector = new(
            [LoggedIn + "229 Entering Extended Passive Mode (|||63829|)\r\n200 Type set\r\n213 100\r\n150 Opening BINARY mode data connection\r\n"],
            ["hello"]);

        int exitCode = await RunUntilStalledAsync(connector, TimeSpan.FromSeconds(1), "-sS", "-m", "1", "ftp://127.0.0.1/f.txt");

        Assert.AreEqual((int)CurlExitCode.OperationTimedOut, exitCode);
        Assert.AreEqual("curl: (28) Operation timed out after 1000 milliseconds with 5 out of 100 bytes received" + NewLine, StandardErrorText);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    /// <summary>Runs <paramref name="arguments" /> and advances the clock by <paramref name="wait" /> once the server has stalled.</summary>
    private async Task<int> RunUntilStalledAsync(ScriptedFtpConnector connector, TimeSpan wait, params string[] arguments)
    {
        InMemoryFileSystem files = new();
        TransferDispatch dispatch = new(
            new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver())));
        Task<int> run = new CurlCommandRunner(
                _ => dispatch,
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                timeProvider: clock)
            .RunAsync(arguments);
        await connector.Stalled;
        clock.Advance(wait);
        return await run;
    }

    /// <summary>
    /// A connector whose first connection is the control connection and second the data
    /// connection, each answering its reads from a script and then going silent until its
    /// token is cancelled; with <c>stallDataConnect</c> the data connection's connect itself
    /// never completes.
    /// </summary>
    private sealed class ScriptedFtpConnector(string[] controlReads, string[]? dataReads = null, bool stallDataConnect = false) : IConnector
    {
        private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int connects;

        public Task Stalled => stalled.Task;

        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            bool control = connects++ == 0;
            if (!control && stallDataConnect)
            {
                stalled.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return ConnectResult.Connected(new ScriptedStallingConnection(new Queue<string>(control ? controlReads : dataReads ?? []), stalled));
        }
    }

    private sealed class ScriptedStallingConnection(Queue<string> pendingReads, TaskCompletionSource stalled) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (pendingReads.TryDequeue(out string? read))
            {
                return Encoding.Latin1.GetBytes(read, buffer.Span);
            }

            stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("An infinite delay ended without being cancelled.");
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
