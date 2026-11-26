using System.Net;
using Curl.Original.Protocol.Smb.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Smb;

/// <summary>
/// Pins BL-5329: an SMB <c>-m</c> timeout counts as started once connected, so the engine's exit 28
/// message is <c>Operation timed out ... with 0 bytes received</c> while the session is set up and
/// <c>with 0 out of N bytes received</c> after the open, and the handler writes that line and
/// <c>closing connection #0</c> under <c>-v</c> as curl's <c>failf</c> does.
/// </summary>
[TestClass]
public sealed class SmbTimeoutMessageTests
{
    [TestMethod]
    public async Task ExecuteAsync_TimeoutStallsAtSessionSetup_ReportsStartedAndNoSize()
    {
        var progress = new RecordingDownloadProgress();
        var events = new TranscriptTransferEvents();
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await RunTimedOutAsync(connection, progress, events));

        Assert.IsTrue(progress.Started);
        Assert.AreEqual(0, progress.Downloads.Count);
        CollectionAssert.AreEqual(
            new[] { "* Operation timed out after 3000 milliseconds with 0 bytes received", "* closing connection #0" },
            events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TimeoutStallsAtReadAfterOpen_ReportsTheFileSize()
    {
        var progress = new RecordingDownloadProgress();
        var events = new TranscriptTransferEvents();
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccepted);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await RunTimedOutAsync(connection, progress, events));

        CollectionAssert.AreEqual(new[] { (0L, (long?)11) }, progress.Downloads.ToArray());
        Assert.IsTrue(progress.Started);
        CollectionAssert.AreEqual(
            new[] { "* Operation timed out after 3000 milliseconds with 0 out of 11 bytes received", "* closing connection #0" },
            events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWithoutMaxTime_WritesNoTimeoutLine()
    {
        var events = new TranscriptTransferEvents();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.DownloadUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("User", "Password"),
            Events = events,
            CancellationToken = cancellation.Token,
        };
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new SmbProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), SmbCurlOperatingSystem.Linux).ExecuteAsync(context));

        Assert.AreEqual(0, events.Transcript.Count);
    }

    private static async Task<TransferResult> RunTimedOutAsync(ScriptedConnection connection, RecordingDownloadProgress progress, TranscriptTransferEvents events)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.DownloadUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("User", "Password"),
            Events = events,
            Progress = progress,
            MaxTime = TimeSpan.FromSeconds(3),
            OperationStarted = 0,
            TimeProvider = new ThreeSecondsLaterTimeProvider(),
            CancellationToken = cancellation.Token,
        };
        var stalling = new CancelsWhenNothingIsLeftConnection(connection, cancellation);
        return await new SmbProtocolHandler(new RecordingConnector(ConnectResult.Connected(stalling)), SmbCurlOperatingSystem.Linux).ExecuteAsync(context);
    }

    // Stands for the server that goes quiet: once the scripted replies are read, the -m limit passes.
    private sealed class CancelsWhenNothingIsLeftConnection(ScriptedConnection inner, CancellationTokenSource cancellation) : IConnection
    {
        public bool IsSecure => inner.IsSecure;

        public System.Net.EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int read = await inner.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                await cancellation.CancelAsync();
            }

            return read;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => inner.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class ThreeSecondsLaterTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => 3000;
    }

    private sealed class RecordingDownloadProgress : ITransferProgress
    {
        public bool Started { get; private set; }

        public List<(long BytesSoFar, long? ExpectedTotal)> Downloads { get; } = [];

        public void ReportTransferStarted() => Started = true;

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Downloads.Add((bytesSoFar, expectedTotal));

        public void ReportUploaded(long bytesSoFar, long? expectedTotal)
        {
        }
    }
}
