using System.Net;
using Curl.Extended.Protocol.SignalR.Transport;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR;

/// <summary>Tests that the negotiate request counts as the transfer, so <c>-m</c> during it reads "Operation timed out" as in curl.</summary>
[TestClass]
public sealed class SignalRNegotiateTimeoutTests
{
    [TestMethod]
    public async Task ExecuteAsync_NegotiateConnectionIsOpen_ReportsTheTransferStartedBeforeWaitingForTheReply()
    {
        var progress = new RecordingProgress();
        var connector = new Client.ScriptedConnector(ConnectResult.Connected(new SilentConnection()));
        var handler = new Client.SignalRProtocolHandler(connector, new RepeatingRandomSource(0));
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("signalr://example.com/hub"),
            Output = new MemoryStream(),
            Progress = progress,
            CancellationToken = cancellation.Token,
        }));

        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_DoesNotReportTheTransferStarted()
    {
        var progress = new RecordingProgress();
        var connector = new Client.ScriptedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));

        await new Client.SignalRProtocolHandler(connector, new RepeatingRandomSource(0)).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("signalr://example.com/hub"),
            Output = new MemoryStream(),
            Progress = progress,
        });

        Assert.AreEqual(0, progress.TransferStartedCount);
    }

    private sealed class SilentConnection : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingProgress : ITransferProgress
    {
        public int TransferStartedCount { get; private set; }

        public void ReportTransferStarted() => TransferStartedCount++;

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
        {
        }

        public void ReportUploaded(long bytesSoFar, long? expectedTotal)
        {
        }
    }
}
