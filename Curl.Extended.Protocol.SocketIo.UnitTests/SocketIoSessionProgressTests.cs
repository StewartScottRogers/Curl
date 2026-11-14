using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extended.Protocol.SocketIo;

/// <summary>
/// Proves the Socket.IO session tells the transfer's progress the bytes it has written, so a <c>-m</c> timeout
/// reports <c>with N bytes received</c> and <c>%{size_download}</c> equals them (EXT-036).
/// </summary>
[TestClass]
public sealed class SocketIoSessionProgressTests
{
    private const string Open = "0{\"sid\":\"s1\",\"upgrades\":[],\"pingInterval\":25000,\"pingTimeout\":20000,\"maxPayload\":1000000}";

    [TestMethod]
    public async Task ExecuteAsync_TwoEvents_ReportTheRunningTotalOfBytesWritten()
    {
        RecordingProgress progress = new();

        TransferResult result = await RunAsync(Server("42[\"news\",{\"a\":1}]", "42[\"b\"]", "41"), progress, CancellationToken.None);

        CollectionAssert.AreEqual(new long[] { 17, 23 }, progress.Downloaded);
        Assert.AreEqual(23, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoEvents_ReportsNoBytes()
    {
        RecordingProgress progress = new();

        await RunAsync(Server("41"), progress, CancellationToken.None);

        Assert.AreEqual(0, progress.Downloaded.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_TimeoutAfterOneEvent_HasReportedSeventeenBytes()
    {
        RecordingProgress progress = new();
        using CancellationTokenSource cancellation = new();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await RunAsync(new SilentAfter(Server("42[\"news\",{\"a\":1}]")), progress, cancellation.Token));

        CollectionAssert.AreEqual(new long[] { 17 }, progress.Downloaded);
    }

    private static ScriptedConnection Server(params string[] messages) =>
        new([.. ScriptedConnection.SwitchingProtocols(), .. new[] { Open, "40{\"sid\":\"x\"}" }.Concat(messages).SelectMany(message => ScriptedConnection.ServerFrame(WsOpcode.Text, message))]);

    private static ValueTask<TransferResult> RunAsync(IConnection connection, RecordingProgress progress, CancellationToken cancellationToken) =>
        new SocketIoProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection)), new ZeroRandomSource())
            .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("socketio://example.com/"), Output = new MemoryStream(), Progress = progress, CancellationToken = cancellationToken });

    private sealed class SilentAfter(ScriptedConnection inner) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int read = await inner.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return read;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => inner.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class RecordingProgress : ITransferProgress
    {
        public List<long> Downloaded { get; } = [];

        public void ReportTransferStarted()
        {
        }

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Downloaded.Add(bytesSoFar);

        public void ReportUploaded(long bytesSoFar, long? expectedTotal)
        {
        }
    }
}
