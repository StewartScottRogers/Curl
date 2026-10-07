using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// The <see cref="TransferTimings" /> the handler reports, taken where curl 8.21.0 takes them
/// (measured, BL-287 Notes, ADR-0075), on <see cref="SteppingTimeProvider" /> so every
/// timestamp is distinct and in the order it was taken.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_Exchange_ReportsTheConnectorsTimingsAndEachStepOfTheRequestInOrder()
    {
        ConnectTimings connectTimings = new(1, 2, 3, null);
        QueueConnector connector = new(ConnectResult.Connected(Connection(Head + "hello", 65536), connectTimings));
        Diagnostics.Arrange("connect timings", "1, 2, 3, none; clock steps from 100");

        TransferResult result = await Handler(connector).ExecuteAsync(TimedContext("http://127.0.0.1/", new SteppingTimeProvider(100)));

        TransferTimings timings = result.Report!.Timings!;
        WriteResult(result);
        WriteTimings(timings);
        Diagnostics.Assert("started", 100L, timings.Started);
        Diagnostics.Assert("connect timings are the connector's", true, ReferenceEquals(connectTimings, timings.Connect));
        Assert.AreEqual(100L, timings.Started);
        Assert.AreSame(connectTimings, timings.Connect);
        Assert.IsTrue(timings.Started < timings.RequestReady, "pretransfer is after the start.");
        Assert.IsTrue(timings.RequestReady < timings.RequestSent, "posttransfer is after pretransfer.");
        Assert.IsTrue(timings.RequestSent < timings.FirstByteReceived, "starttransfer is after posttransfer.");
        Assert.IsTrue(timings.FirstByteReceived < timings.Completed, "The total is after starttransfer.");
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesWithoutAReply_ReportsNoFirstByte()
    {
        // curl 8.21.0 against a server that closed without a reply: exit 52, st=0.000000.
        Diagnostics.Arrange("scripted response", "(none, the server closes)");
        TransferResult result = await Handler(QueueConnector.For(Connection(string.Empty, 65536)))
            .ExecuteAsync(TimedContext("http://127.0.0.1/", new SteppingTimeProvider(100)));

        TransferTimings timings = result.Report!.Timings!;
        WriteResult(result);
        WriteTimings(timings);
        Diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        Diagnostics.Assert("first byte received", "(none)", timings.FirstByteReceived?.ToString(CultureInfo.InvariantCulture) ?? "(none)");
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.IsNotNull(timings.RequestSent);
        Assert.IsNull(timings.FirstByteReceived);
    }

    [TestMethod]
    [DataRow(CurlExitCode.CouldntResolveHost, DisplayName = "6")]
    [DataRow(CurlExitCode.CouldntConnect, DisplayName = "7")]
    public async Task ExecuteAsync_ConnectFails_ReportsPreAndPostAndStartTransferAsTheMomentItFailed(CurlExitCode exitCode)
    {
        // curl 8.21.0, http://127.0.0.1:1/: exit 7, c=0.000000|pre=2.030259|post=2.030259|st=2.030259|t=2.030262.
        Diagnostics.Arrange("connect result", $"failed, {exitCode}");
        TransferResult result = await Handler(new QueueConnector(ConnectResult.Failed(exitCode, "failed")))
            .ExecuteAsync(TimedContext("http://127.0.0.1:1/", new SteppingTimeProvider(100)));

        TransferTimings timings = result.Report!.Timings!;
        WriteResult(result);
        WriteTimings(timings);
        Diagnostics.Assert("started", 100L, timings.Started);
        Diagnostics.Assert("sent, first byte and completed equal ready", true, timings.RequestReady == timings.RequestSent && timings.RequestReady == timings.FirstByteReceived && timings.RequestReady == timings.Completed);
        Assert.AreEqual(100L, timings.Started);
        Assert.IsNull(timings.Connect);
        Assert.IsTrue(timings.RequestReady > timings.Started, "pretransfer is not zero.");
        Assert.AreEqual(timings.RequestReady, timings.RequestSent);
        Assert.AreEqual(timings.RequestReady, timings.FirstByteReceived);
        Assert.AreEqual(timings.RequestReady, timings.Completed);
        Assert.IsFalse(result.Report.UsedProxy);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefusedAfterALookup_ReportsTheConnectorsStartAndLookupWithNoConnect()
    {
        // curl 8.21.0, http://127.0.0.1:1/: exit 7, ns=0.000048|c=0.000000 (measured 2026-09-27, BL-382).
        var connectTimings = new ConnectTimings(105, 106, null, null);
        Diagnostics.Arrange("connect result", "refused, connect timings 105, 106, none, none");
        TransferResult result = await Handler(new QueueConnector(ConnectResult.Refused("refused", connectTimings)))
            .ExecuteAsync(TimedContext("http://127.0.0.1:1/", new SteppingTimeProvider(100)));

        TransferTimings timings = result.Report!.Timings!;
        WriteResult(result);
        WriteTimings(timings);
        Diagnostics.Assert("name resolved", 106L, timings.Connect!.NameResolved);
        Diagnostics.Assert("connection refused", true, result.IsConnectionRefused);
        Assert.AreSame(connectTimings, timings.Connect);
        Assert.AreEqual(106L, timings.Connect!.NameResolved);
        Assert.IsNull(timings.Connect.Connected);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    private void WriteTimings(TransferTimings timings) =>
        Diagnostics.Act(
            "timings",
            $"started {timings.Started}, ready {Timestamp(timings.RequestReady)}, sent {Timestamp(timings.RequestSent)}, first byte {Timestamp(timings.FirstByteReceived)}, completed {Timestamp(timings.Completed)}, connect {(timings.Connect is null ? "(none)" : "reported")}");

    private static string Timestamp(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "(none)";

    private static TransferContext TimedContext(string url, TimeProvider timeProvider) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), TimeProvider = timeProvider };
}
