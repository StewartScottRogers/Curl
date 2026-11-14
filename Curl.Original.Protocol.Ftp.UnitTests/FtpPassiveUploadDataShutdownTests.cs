using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins BL-5110: a plain passive upload ends its data connection by shutting its sending side
/// down, so a server that waits for end of data sees it at once and the upload does not wait out
/// the two-second shutdown timeout; a connection whose shutdown does nothing still ends after it.
/// </summary>
[TestClass]
public sealed class FtpPassiveUploadDataShutdownTests
{
    private const string Replies =
        "220 ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n" +
        "229 Entering Extended Passive Mode (|||64396|)\r\n200 Type set\r\n150 Ok to send data\r\n226 Transfer complete\r\n221 Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_PlainPassiveUpload_ShutsTheSendingSideDownAndFinishesWithoutTheTimeout()
    {
        var data = new PeerWaitingConnection { ShutDownEndsTheRead = true };
        TransferResult result = await RunAsync(data, new NeverFiringTimeProvider());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(1, data.ShutDowns);
        Assert.AreEqual("up1\n", data.Written.ToString());
        Assert.IsTrue(data.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_PlainPassiveUploadWhoseShutdownDoesNothing_EndsAfterTheTwoSecondTimeout()
    {
        var data = new PeerWaitingConnection { ShutDownEndsTheRead = false };
        var time = new ImmediateTimerTimeProvider();

        TransferResult result = await RunAsync(data, time);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(1, data.ShutDowns);
        Assert.IsTrue(data.Disposed);
        CollectionAssert.Contains(time.Waits, TimeSpan.FromSeconds(2));
    }

    private static async Task<TransferResult> RunAsync(IConnection data, TimeProvider time)
    {
        var connector = new QueuedConnector(
            ConnectResult.Connected(ScriptedConnection.FromReplies(Replies)),
            ConnectResult.Connected(data));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ftp://127.0.0.1/u.txt"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.Latin1.GetBytes("up1\n")),
            TimeProvider = time,
        };

        return await new FtpProtocolHandler(connector).ExecuteAsync(context);
    }

    /// <summary>A clock whose timers never fire, so only the shutdown can end the upload's wait.</summary>
    private sealed class NeverFiringTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            new IdleTimer();

        private sealed class IdleTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>A data connection whose read waits for the sender's shutdown, or never ends.</summary>
    private sealed class PeerWaitingConnection : IConnection
    {
        private readonly TaskCompletionSource _shutDown = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ShutDownEndsTheRead { get; init; }

        public int ShutDowns { get; private set; }

        public bool Disposed { get; private set; }

        public StringBuilder Written { get; } = new();

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await _shutDown.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            Written.Append(Encoding.Latin1.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask ShutDownSendingAsync(CancellationToken cancellationToken)
        {
            ShutDowns++;
            if (ShutDownEndsTheRead)
            {
                _shutDown.SetResult();
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
