using System.Net;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Console;
using Curl.Core;
using Curl.Original.Protocol.Ftp;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins BL-6064: <c>-m</c> passing while an FTP <c>SIZE</c> reply is awaited writes curl 8.22.0's two closing
/// <c>-v</c> lines and reports <c>%{size_request}</c> 0, as measured 2026-11-26 with
/// <c>Record-CurlExchange.ps1 -Ftp -FtpReply 'SIZE=STALL'</c>.
/// </summary>
[TestClass]
public sealed class FtpMaxTimeReplyWaitClosingLinesTests
{
    private const string RepliesBeforeSize =
        "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n" +
        "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n";

    [TestMethod]
    public async Task PerformAsync_MaxTimePassesWhileTheSizeReplyIsAwaited_WritesTheTimeoutAndClosingLinesAndNoRequestSize()
    {
        SteppingTimeProvider clock = new();
        SizeSignallingConnection control = new();
        control.Replies.Enqueue(Encoding.ASCII.GetBytes(RepliesBeforeSize));
        QueuedDataConnector connector = new(control, new QueuedConnection());
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)new ManagedCurlEngine(clock, (_, _, _) => new ProtocolDispatcher([new FtpProtocolHandler(connector)])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "ftp://h/f.txt" },
            Transfer = new EasyTransferOptions { Timeout = TimeSpan.FromSeconds(8) },
        };
        InfoRecordingEvents events = new();
        easy.ToolEvents = new EngineToolTransferEvents(events, new EngineTransferState(), () => 0);
        easy.ToolProgress = new EngineToolTransferProgress(new IgnoringProgress(), CancellationToken.None);

        Task<CurlCode> perform = easy.PerformAsync(CancellationToken.None).AsTask();
        await control.SizeSent.Task;
        clock.Advance(TimeSpan.FromSeconds(8));
        CurlCode code = await perform;

        Assert.AreEqual(CurlCode.OperationTimedOut, code);
        string[] lastLines = [.. events.Info.TakeLast(2)];
        string[] expected = ["Operation timed out after 8000 milliseconds with 0 bytes received", "closing connection #0"];
        CollectionAssert.AreEqual(expected, lastLines);
        Assert.AreEqual(0, easy.LastResult!.Report!.RequestSize);
    }

    /// <summary>A control connection that says when <c>SIZE</c> has been written to it.</summary>
    private sealed class SizeSignallingConnection : IConnection
    {
        public QueuedConnection Replies { get; } = new();

        public TaskCompletionSource SizeSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => Replies.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (buffer.Span.StartsWith("SIZE "u8))
            {
                SizeSent.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Gives the control connection first, then the data connection.</summary>
    private sealed class QueuedDataConnector(IConnection control, IConnection data) : IConnector
    {
        private int dials;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(Interlocked.Increment(ref dials) == 1 ? control : data));
    }

    private sealed class IgnoringProgress : ITransferProgress
    {
        public void ReportTransferStarted()
        {
        }

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
        {
        }

        public void ReportUploaded(long bytesSoFar, long? expectedTotal)
        {
        }
    }

    private sealed class InfoRecordingEvents : ITransferEvents
    {
        public List<string> Info { get; } = [];

        public void ReportInfo(string text) => Info.Add(text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
