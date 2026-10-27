using System.Net;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Console;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins CGR-144 (BL-3673): a curl tool perform (<see cref="ManagedEasyTransfer.ToolEvents" /> set) that a <c>-Y</c>/<c>-y</c>
/// watchdog ends after the response head keeps the response code, HTTP version, connection count, request size and body
/// bytes on its result's report, so <c>-w</c> prints <c>[1.1 200 1 100 80]</c> as curl 8.22.0 does, not
/// <c>[0 000 0 100 0]</c>; over HTTP/2 the version is 2.
/// </summary>
[TestClass]
public sealed class Cgr144Tests
{
    private static readonly ConnectionOpenedEvent Opened = new() { HostName = "h", RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80), LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000), ConnectionNumber = 1 };

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\n", 1, 1)]
    [DataRow("HTTP/2 200\r\n", 2, 0)]
    public async Task PerformAsync_ToolEventsSetAndTooSlowAfterTheHead_KeepsTheHeadOnTheReport(string statusLine, int major, int minor)
    {
        SteppingTimeProvider clock = new();
        TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] request = Encoding.ASCII.GetBytes("GET /x HTTP/1.1\r\nHost: h\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n");
        RecordingProtocolHandler handler = new("http", async context =>
        {
            context.Events.ReportConnectionOpened(Opened);
            context.Events.ReportRequestHeader(request);
            context.Events.ReportResponseHeader(Encoding.ASCII.GetBytes(statusLine));
            context.Events.ReportResponseHeader(Encoding.ASCII.GetBytes("\r\n"));
            context.Events.ReportDataReceived(new byte[100]);
            stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return TransferResult.Success(0);
        });
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)new ManagedCurlEngine(clock, (_, _, _) => new ProtocolDispatcher([handler])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Transfer = new EasyTransferOptions { LowSpeedLimit = 1000, LowSpeedTime = TimeSpan.FromSeconds(1) },
        };
        easy.ToolEvents = new EngineToolTransferEvents(NoTransferEvents.Instance, new EngineTransferState(), () => -2);

        Task<CurlCode> perform = easy.PerformAsync(CancellationToken.None).AsTask();
        await stalled.Task;
        clock.Advance(TimeSpan.FromSeconds(3));
        CurlCode code = await perform;

        Assert.AreEqual(CurlCode.OperationTimedOut, code);
        TransferReport report = easy.LastResult!.Report!;
        Assert.AreEqual(200, report.ResponseCode);
        Assert.AreEqual(new Version(major, minor), report.HttpVersion);
        Assert.AreEqual(1, report.ConnectionCount);
        Assert.AreEqual(request.Length, report.RequestSize);
        Assert.AreEqual(100, report.DownloadSize);
    }

    [TestMethod]
    [DataRow(0L, 1, true)]
    [DataRow(0L, 0, false)]
    public async Task PerformAsync_ToolEventsSet_RecordsTheHeadOnlyUnderALowSpeedOption(long lowSpeedLimit, int lowSpeedSeconds, bool recorded)
    {
        ITransferEvents? seen = null;
        RecordingProtocolHandler handler = new("http", context =>
        {
            seen = context.Events;
            return ValueTask.FromResult(TransferResult.Success(0));
        });
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)new ManagedCurlEngine(new SteppingTimeProvider(), (_, _, _) => new ProtocolDispatcher([handler])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Transfer = new EasyTransferOptions { LowSpeedLimit = lowSpeedLimit, LowSpeedTime = TimeSpan.FromSeconds(lowSpeedSeconds) },
        };
        easy.ToolEvents = new EngineToolTransferEvents(NoTransferEvents.Instance, new EngineTransferState(), () => -2);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(recorded, seen is AbortedHeadRecordingTransferEvents);
    }

    [TestMethod]
    public void ReportOn_TooSlowAfterAStatusLineWhoseVersionDoesNotParse_KeepsTheCodeWithNoVersion()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("HTTP/x 200 OK\r\n"));

        TransferReport report = events.ReportOn(TransferResult.Failure(CurlExitCode.OperationTimedOut, "too slow", 0))!.Report!;

        Assert.AreEqual(200, report.ResponseCode);
        Assert.IsNull(report.HttpVersion);
    }
}
