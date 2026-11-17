using System.Net;
using System.Text;
using Curl.Original.Protocol.Smb.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Smb;

/// <summary>
/// Pins the timings behind <c>%{time_*}</c> and <c>%{speed_*}</c> and the request size behind
/// <c>%{size_request}</c> that an <c>smb://</c> transfer reports (BL-5328).
/// </summary>
[TestClass]
public sealed class SmbWriteOutReportTests
{
    private static readonly NetworkCredential User = new("User", "Password");

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsTimingsAndTheBytesSent()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccepted,
            SmbRecordedExchange.ReadAccepted,
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.DownloadUrl),
            Output = new MemoryStream(),
            Credentials = User,
            TimeProvider = new AdvancingTimeProvider(),
        };

        TransferResult result = await Run(connection, context, ConnectResult.Connected(connection, new ConnectTimings(1, 2, 3, null)));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(connection.Sent.Length, result.Report!.RequestSize);
        Assert.IsGreaterThan(0, result.Report.RequestSize);
        AssertTimingsReached(result.Report.Timings!);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsTimingsAndTheBytesSentAndKeepsTheUploadSize()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.UploadOpenCreated,
            SmbRecordedExchange.WriteAccepted(11),
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.UploadUrl),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent)),
            Credentials = User,
            TimeProvider = new AdvancingTimeProvider(),
        };

        TransferResult result = await Run(connection, context, ConnectResult.Connected(connection));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(11L, result.Report!.UploadSize);
        Assert.AreEqual(connection.Sent.Length, result.Report.RequestSize);
        Assert.IsNull(result.Report.Timings!.Connect);
        Assert.IsNotNull(result.Report.Timings.RequestReady);
        Assert.IsNotNull(result.Report.Timings.FirstByteReceived);
        Assert.IsGreaterThan(result.Report.Timings.Started, result.Report.Timings.Completed);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedSessionSetup_ReturnsItsExitCodeWithTheTimingsReached()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.SessionSetupRefused);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.DownloadUrl),
            Output = new MemoryStream(),
            Credentials = User,
            TimeProvider = new AdvancingTimeProvider(),
        };

        TransferResult result = await Run(connection, context, ConnectResult.Connected(connection, new ConnectTimings(1, 2, 3, null)));

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("Login denied", result.ErrorMessage);
        Assert.IsGreaterThan(0, result.Report!.RequestSize);
        Assert.IsNotNull(result.Report.Timings!.RequestReady);
        Assert.IsNotNull(result.Report.Timings.FirstByteReceived);
        Assert.IsGreaterThan(result.Report.Timings.Started, result.Report.Timings.Completed);
    }

    [TestMethod]
    public async Task CountingConnection_PassesEverythingThroughToTheConnection()
    {
        var inner = new ScriptedConnection([1, 2]);
        var counting = new SmbCountingConnection(inner, new AdvancingTimeProvider());

        await counting.WriteAsync(new byte[3], CancellationToken.None);
        await counting.FlushAsync(CancellationToken.None);
        int read = await counting.ReadAsync(new byte[4], CancellationToken.None);
        await counting.DisposeAsync();

        Assert.AreEqual(3L, counting.BytesWritten);
        Assert.AreEqual(2, read);
        Assert.IsNotNull(counting.FirstReadAt);
        Assert.IsFalse(counting.IsSecure);
        Assert.IsNull(counting.RemoteEndPoint);
        Assert.IsTrue(inner.IsDisposed);
    }

    private static void AssertTimingsReached(TransferTimings timings)
    {
        Assert.AreEqual(new ConnectTimings(1, 2, 3, null), timings.Connect);
        Assert.IsGreaterThan(timings.Started, timings.RequestReady!.Value);
        Assert.IsGreaterThan(timings.RequestReady.Value, timings.FirstByteReceived!.Value);
        Assert.IsGreaterThan(timings.FirstByteReceived.Value, timings.Completed);
    }

    private static async Task<TransferResult> Run(ScriptedConnection connection, TransferContext context, ConnectResult connect)
    {
        _ = connection;
        return await new SmbProtocolHandler(new RecordingConnector(connect), SmbCurlOperatingSystem.Linux).ExecuteAsync(context);
    }

    // A clock that moves 1000 ticks on every reading, so every later stamp is greater than every earlier one.
    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private long now;

        public override long GetTimestamp() => now += 1000;
    }
}
