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
/// Pins BL-6053: <c>-m</c> passing during an FTP passive data connect writes curl 8.22.0's two closing <c>-v</c>
/// lines and reports <c>%{size_header}</c> 0, as measured 2026-11-26 with <c>Record-CurlExchange.ps1 -Ftp</c>.
/// </summary>
[TestClass]
public sealed class FtpDataConnectMaxTimeTests
{
    private const string Replies =
        "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n" +
        "500 no: refused\r\n227 Entering: (10,255,255,1,10,10)\r\n";

    [TestMethod]
    public async Task PerformAsync_MaxTimePassesDuringTheDataConnect_WritesTheTimeoutAndClosingLinesAndNoHeaderSize()
    {
        SteppingTimeProvider clock = new();
        QueuedConnection control = new();
        control.Enqueue(Encoding.ASCII.GetBytes(Replies));
        StallingDataConnector connector = new(control);
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
        await connector.DataDialStarted.Task;
        clock.Advance(TimeSpan.FromSeconds(8));
        CurlCode code = await perform;

        Assert.AreEqual(CurlCode.OperationTimedOut, code);
        string[] lastLines = [.. events.Info.TakeLast(2)];
        string[] expected = ["Operation timed out after 8000 milliseconds with 0 bytes received", "closing connection #0"];
        CollectionAssert.AreEqual(expected, lastLines);
        Assert.AreEqual(0, easy.LastResult!.Report!.HeaderSize);
    }

    /// <summary>Gives the control connection first, then a data dial that ends only when cancelled.</summary>
    private sealed class StallingDataConnector(IConnection control) : IConnector
    {
        private int dials;

        public TaskCompletionSource DataDialStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref dials) == 1)
            {
                return ConnectResult.Connected(control);
            }

            DataDialStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return ConnectResult.Connected(control);
        }
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
