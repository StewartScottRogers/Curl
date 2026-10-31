using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins CGR-010 (BL-3539): <c>--connect-timeout</c> holds the SSH handshake, so a server that
/// accepts the connection and then sends nothing, or only an identification line, ends the
/// transfer with exit 28 and <c>Operation timed out after N milliseconds with 0 bytes
/// received</c>, as curl 8.22.0 does (measured 2026-10-26), rather than waiting for the
/// server to close.
/// </summary>
[TestClass]
public sealed class Cgr010Tests
{
    private const string Url = "sftp://127.0.0.1:21531/x";

    [TestMethod]
    [DataRow("sftp", "", DisplayName = "sftp, silent")]
    [DataRow("scp", "", DisplayName = "scp, silent")]
    [DataRow("sftp", "SSH-1.5-old\r\n", DisplayName = "sftp, identification only")]
    public async Task ExecuteAsync_ServerSilentPastTheConnectTimeout_IsExit28OperationTimedOut(string scheme, string sent)
    {
        ManualClock clock = new();
        SilentConnection connection = new(clock, Encoding.ASCII.GetBytes(sent));

        TransferResult result = await RunAsync(clock, $"{scheme}://127.0.0.1:21531/x", connection, TimeSpan.FromSeconds(3), CancellationToken.None);

        Assert.AreEqual(TimeSpan.FromSeconds(3), clock.DueTime);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 3012 milliseconds with 0 bytes received", result.ErrorMessage);
        Assert.AreEqual(sent.Length > 0, Encoding.Latin1.GetString(connection.Written.ToArray()).Contains(SshProtocolHandler.DisconnectDescription, StringComparison.Ordinal), "DISCONNECT only once the server's identification arrived, as measured");
    }

    [TestMethod]
    [DataRow(null, DisplayName = "not given")]
    [DataRow(0, DisplayName = "0")]
    public async Task ExecuteAsync_NoConnectTimeout_HoldsTheHandshakeToCurlsDefault300Seconds(int? seconds)
    {
        ManualClock clock = new();
        TransferResult result = await RunAsync(clock, Url, string.Empty, seconds is { } given ? TimeSpan.FromSeconds(given) : null, CancellationToken.None);

        Assert.AreEqual(TimeSpan.FromSeconds(300), clock.DueTime);
        Assert.AreEqual("Operation timed out after 300012 milliseconds with 0 bytes received", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferCancelledDuringTheHandshake_ThrowsRatherThanTimingOut()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await RunAsync(new ManualClock(), Url, string.Empty, TimeSpan.FromSeconds(3), cancelled.Token));
    }

    private static Task<TransferResult> RunAsync(ManualClock clock, string url, string sent, TimeSpan? connectTimeout, CancellationToken cancellationToken) =>
        RunAsync(clock, url, new SilentConnection(clock, Encoding.ASCII.GetBytes(sent)), connectTimeout, cancellationToken);

    private static async Task<TransferResult> RunAsync(ManualClock clock, string url, SilentConnection connection, TimeSpan? connectTimeout, CancellationToken cancellationToken)
    {
        SshProtocolHandler handler = new(new FixedConnector(connection), new InMemoryKeyFileSystem(new Dictionary<string, string>()), SshAlgorithmPreferences.OpenSslReference, Encoding.UTF8);
        return await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            ConnectTimeout = connectTimeout,
            TimeProvider = clock,
            CancellationToken = cancellationToken,
        });
    }

    private sealed class FixedConnector(IConnection connection) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(connection));
    }

    // A clock whose one timer fires, 12 ms late, when the server goes silent.
    private sealed class ManualClock : TimeProvider
    {
        private long now;

        private TimerCallback? callback;

        private object? state;

        public TimeSpan DueTime { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            this.callback = callback;
            this.state = state;
            DueTime = dueTime;
            return new IdleTimer();
        }

        public void FireTimer()
        {
            now += (DueTime + TimeSpan.FromMilliseconds(12)).Ticks;
            callback?.Invoke(state);
        }
    }

    private sealed class IdleTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // A server that sends its bytes, if any, then nothing: a read past them waits until it
    // is cancelled, or for five seconds, after which the server closes.
    private sealed class SilentConnection(ManualClock clock, byte[] sent) : IConnection
    {
        private int position;

        public MemoryStream Written { get; } = new();

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (position < sent.Length)
            {
                int count = Math.Min(buffer.Length, sent.Length - position);
                sent.AsMemory(position, count).CopyTo(buffer);
                position += count;
                return count;
            }

            clock.FireTimer();
            using CancellationTokenSource closed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            closed.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, closed.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }

            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Written.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
