using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Curl.Mcp.Protocol.Server;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class McpStdioHostTests
{
    private const string PingRequest = """{"jsonrpc":"2.0","id":1,"method":"ping"}""";

    private const string HangRequest = """{"jsonrpc":"2.0","id":2,"method":"test/hang"}""";

    private static readonly McpServerInfo ServerInfo = new("curl", "8.0.0");

    [TestMethod]
    public async Task RunAsync_InputEnds_ReturnsZeroAndWritesOnlyProtocolLinesToTheOutput()
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(PingRequest + "\n"));
        using MemoryStream output = new();
        using StringWriter error = new();
        RecordingSignals signals = new();
        using McpStdioHost host = CreateHost(input, output, error, _ => null, new ManualTimeProvider(), signals, []);

        int exitCode = await host.RunAsync(CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        string[] lines = Encoding.UTF8.GetString(output.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(1, lines);
        Assert.AreEqual(1, JsonDocument.Parse(lines[0]).RootElement.GetProperty("id").GetInt32());
        StringAssert.StartsWith(error.ToString(), "2026-10-23T12:00:00.000Z Curl.Mcp is serving on standard input and output.");
        StringAssert.EndsWith(error.ToString(), "Curl.Mcp stopped." + Environment.NewLine);
        Assert.IsTrue(signals.Unregistered);
    }

    [TestMethod]
    public async Task RunAsync_CancellationTokenCancelled_ReturnsZero()
    {
        using ChannelInputStream input = new();
        using MemoryStream output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        using McpStdioHost host = CreateHost(input, output, error, _ => null, new ManualTimeProvider(), new RecordingSignals(), []);

        Task<int> running = host.RunAsync(cancellation.Token);
        await cancellation.CancelAsync();
        int exitCode = await running;

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, output.Length);
        StringAssert.EndsWith(error.ToString(), "Curl.Mcp stopped." + Environment.NewLine);
    }

    [TestMethod]
    public async Task RunAsync_StopSignalArrives_CancelsTheSignalsDefaultAndReturnsZero()
    {
        using ChannelInputStream input = new();
        using MemoryStream output = new();
        using StringWriter error = new();
        RecordingSignals signals = new();
        using McpStdioHost host = CreateHost(input, output, error, _ => null, new ManualTimeProvider(), signals, []);
        PosixSignalContext context = new(PosixSignal.SIGTERM);

        Task<int> running = host.RunAsync(CancellationToken.None);
        signals.Handler!(context);
        int exitCode = await running;

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(context.Cancel);
        Assert.IsTrue(signals.Unregistered);
    }

    [TestMethod]
    public async Task RunAsync_BadMaxResponseBytes_ThrowsBeforeReadingAnyInput()
    {
        using ChannelInputStream input = new();
        using MemoryStream output = new();
        using StringWriter error = new();
        bool handlersCreated = false;
        using McpStdioHost host = new(
            ServerInfo,
            (_, _) =>
            {
                handlersCreated = true;
                return [];
            },
            input,
            output,
            new McpStandardErrorLog(error, new ManualTimeProvider()),
            name => name == McpResponseCap.VariableName ? "0" : null,
            new ManualTimeProvider(),
            new RecordingSignals().Register);

        await Assert.ThrowsExactlyAsync<McpConfigurationException>(() => host.RunAsync(CancellationToken.None));

        Assert.IsFalse(input.WasRead);
        Assert.IsFalse(handlersCreated);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task RunAsync_RootSet_MakesItTheCurrentDirectoryAndPassesTheConfigurationToTheHandlers()
    {
        string before = Directory.GetCurrentDirectory();
        string root = Directory.CreateTempSubdirectory("curl-mcp-host-").FullName;
        try
        {
            using MemoryStream input = new();
            using MemoryStream output = new();
            using StringWriter error = new();
            string? directoryWhenHandlersCreated = null;
            McpResponseCap? capSeen = null;
            using McpStdioHost host = new(
                ServerInfo,
                (_, cap) =>
                {
                    directoryWhenHandlersCreated = Directory.GetCurrentDirectory();
                    capSeen = cap;
                    return [];
                },
                input,
                output,
                new McpStandardErrorLog(error, new ManualTimeProvider()),
                name => name switch
                {
                    McpConfinementRoot.VariableName => root,
                    McpResponseCap.VariableName => "2048",
                    _ => null,
                },
                new ManualTimeProvider(),
                new RecordingSignals().Register);

            int exitCode = await host.RunAsync(CancellationToken.None);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(new McpConfinementRoot(root).ResolvedRoot, directoryWhenHandlersCreated);
            Assert.AreEqual(2048, capSeen!.MaxBytes);
        }
        finally
        {
            Directory.SetCurrentDirectory(before);
            Directory.Delete(root);
        }
    }

    [TestMethod]
    public async Task RunAsync_ReadingTheInputFails_LogsTheFailureAndReturnsOne()
    {
        using FailingInputStream input = new();
        using MemoryStream output = new();
        using StringWriter error = new();
        using McpStdioHost host = CreateHost(input, output, error, _ => null, new ManualTimeProvider(), new RecordingSignals(), []);

        int exitCode = await host.RunAsync(CancellationToken.None);

        Assert.AreEqual(1, exitCode);
        StringAssert.Contains(error.ToString(), "Curl.Mcp stopped because reading its input failed: The pipe broke.");
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task RunAsync_RequestOutlivesTheShutdownTimeout_LogsItAndReturnsOne()
    {
        using ChannelInputStream input = new();
        using MemoryStream output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellation = new();
        ManualTimeProvider time = new();
        HangingHandler hanging = new();
        using McpStdioHost host = CreateHost(input, output, error, _ => null, time, new RecordingSignals(), [hanging]);
        try
        {
            Task<int> running = host.RunAsync(cancellation.Token);
            input.Send(HangRequest + "\n");
            await hanging.Started.Task;
            await cancellation.CancelAsync();
            await time.TimerCreated.Task;
            time.Advance(McpStdioHost.ShutdownTimeout);
            int exitCode = await running;

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Curl.Mcp is stopping; requests in flight have 30 seconds to finish.");
            StringAssert.Contains(error.ToString(), "Curl.Mcp stopped with requests still in flight after 30 seconds.");
            Assert.AreEqual(0, output.Length);
        }
        finally
        {
            hanging.Release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task RunAsync_StopSignalWhileReadIgnoresItsToken_ReturnsZeroWithoutWaitingTheShutdownTimeout()
    {
        TokenIgnoringInputStream input = new();
        using MemoryStream output = new();
        using StringWriter error = new();
        RecordingSignals signals = new();
        ManualTimeProvider time = new();
        using McpStdioHost host = CreateHost(input, output, error, _ => null, time, signals, []);

        Task<int> running = host.RunAsync(CancellationToken.None);
        await input.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        signals.Handler!(new PosixSignalContext(PosixSignal.SIGTERM));
        int exitCode = await running.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(0, exitCode);
        StringAssert.EndsWith(error.ToString(), "Curl.Mcp stopped." + Environment.NewLine);
        Assert.AreEqual(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero), time.GetUtcNow());
    }

    [TestMethod]
    public void RegisterProcessStopSignals_Disposed_UnregistersWithoutRaisingAnySignal()
    {
        int calls = 0;

        IDisposable registration = McpStdioHost.RegisterProcessStopSignals(_ => calls++);
        registration.Dispose();

        Assert.AreEqual(0, calls);
    }

    private static McpStdioHost CreateHost(
        Stream input,
        Stream output,
        TextWriter error,
        Func<string, string?> readEnvironmentVariable,
        ManualTimeProvider time,
        RecordingSignals signals,
        IMcpMethodHandler[] handlers) =>
        new(
            ServerInfo,
            (_, _) => handlers,
            input,
            output,
            new McpStandardErrorLog(error, time),
            readEnvironmentVariable,
            time,
            signals.Register);

    private sealed class RecordingSignals
    {
        public Action<PosixSignalContext>? Handler { get; private set; }

        public bool Unregistered { get; private set; }

        public IDisposable Register(Action<PosixSignalContext> handler)
        {
            Handler = handler;
            return new Unregistration(this);
        }

        private sealed class Unregistration(RecordingSignals signals) : IDisposable
        {
            public void Dispose() => signals.Unregistered = true;
        }
    }

    private sealed class HangingHandler : IMcpMethodHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Method => "test/hang";

        // Ignores its token, as a request that will not cancel does.
        public async ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Release.Task;
            return JsonDocument.Parse("{}").RootElement;
        }
    }

    // An input that stays open until disposed, delivering what the test sends.
    private sealed class ChannelInputStream : Stream
    {
        private readonly Channel<byte[]> chunks = Channel.CreateUnbounded<byte[]>();

        public bool WasRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public void Send(string text) => chunks.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WasRead = true;
            byte[] chunk = await chunks.Reader.ReadAsync(cancellationToken);
            chunk.CopyTo(buffer);
            return chunk.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // An input whose read blocks and never looks at its token, as the console's standard input does.
    private sealed class TokenIgnoringInputStream : Stream
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult();
            await new TaskCompletionSource().Task;
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FailingInputStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("The pipe broke."));

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // A clock that moves only when the test advances it, and fires the timers then due.
    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly Lock gate = new();

        private readonly List<ManualTimer> timers = [];

        private DateTimeOffset now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

        public TaskCompletionSource TimerCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override DateTimeOffset GetUtcNow()
        {
            lock (gate)
            {
                return now;
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ManualTimer timer = new(callback, state);
            lock (gate)
            {
                timer.DueAt = now + dueTime;
                timers.Add(timer);
            }

            TimerCreated.TrySetResult();
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            ManualTimer[] due;
            lock (gate)
            {
                now += by;
                due = [.. timers.Where(timer => timer.DueAt <= now)];
                timers.RemoveAll(due.Contains);
            }

            foreach (ManualTimer timer in due)
            {
                timer.Fire();
            }
        }

        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            public DateTimeOffset DueAt { get; set; }

            public void Fire() => callback(state);

            public bool Change(TimeSpan dueTime, TimeSpan period) => false;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
