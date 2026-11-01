using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins third-party gap report issue CGR-107's FTP findings (BL-3962): an <c>ftp://</c>
/// transfer's <see cref="TransferReport" /> carries <see cref="TransferTimings" />, the source of
/// <c>%{time_connect}</c>, <c>%{time_pretransfer}</c>, <c>%{time_starttransfer}</c> and
/// <c>%{time_total}</c>, and an upload's <see cref="TransferReport.RequestSize" />, the source of
/// <c>%{size_request}</c>.
/// </summary>
/// <remarks>
/// Measured on 2026-10-28 with curl 8.22.0 against <c>Record-CurlExchange.ps1 -Ftp</c>: a download
/// writes <c>0.012720 0.237568 0.339245 0.728345</c> for those four times, and an eight-byte
/// <c>-T</c> upload writes <c>8 0 8 0</c> for <c>%{size_upload} %{size_download} %{size_request}
/// %{size_header}</c>, its <c>%{time_starttransfer}</c> within microseconds of <c>%{time_total}</c>.
/// </remarks>
[TestClass]
public sealed class Cgr107TimingTests
{
    private const string FileUrl = "ftp://127.0.0.1:18321/f.txt";

    private const string Login = "220 Ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Passive = "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n";

    private const string Download = Login + Passive + "213 10\r\n150 Opening data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    private const string Upload = Login + Passive + "150 Opening\r\n226 Transfer complete\r\n221 Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsOrderedTimesFromTheConnectToTheEnd()
    {
        var clock = new TickingTimeProvider();
        var connected = new ConnectTimings(clock.GetTimestamp(), clock.GetTimestamp(), clock.GetTimestamp(), null);

        TransferReport report = await RunAsync(clock, connected, Download, c => c, Encoding.Latin1.GetBytes("01234"), Encoding.Latin1.GetBytes("56789"));

        TransferTimings timings = report.Timings!;
        Assert.AreSame(connected, timings.Connect);
        Assert.IsTrue(timings.Started < timings.RequestReady, "pretransfer after the start");
        Assert.IsTrue(connected.Connected < timings.RequestReady, "pretransfer after the connect");
        Assert.IsTrue(timings.RequestReady < timings.FirstByteReceived, "starttransfer after pretransfer");
        Assert.IsTrue(timings.FirstByteReceived < timings.Completed, "the total after starttransfer");
        Assert.AreEqual(0L, report.RequestSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsTheBytesSentAsTheRequestSizeAndStartsTransferAtTheEnd()
    {
        var clock = new TickingTimeProvider();

        TransferReport report = await RunAsync(clock, null, Upload, c => new TransferContext { Url = c.Url, Output = c.Output, TimeProvider = c.TimeProvider, Upload = new MemoryStream(Encoding.Latin1.GetBytes("12345678")) });

        Assert.AreEqual("8 0 8 0", $"{report.UploadSize} {report.DownloadSize} {report.RequestSize} {report.HeaderSize}");
        TransferTimings timings = report.Timings!;
        Assert.IsTrue(timings.RequestReady > timings.Started, "pretransfer after the start");
        Assert.AreEqual(timings.Completed, timings.FirstByteReceived);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedLogin_ReportsNoPretransferOrStarttransfer()
    {
        var clock = new TickingTimeProvider();

        TransferReport report = await RunAsync(clock, null, "220 Ready\r\n331 Password required\r\n530 Login incorrect\r\n", c => c);

        TransferTimings timings = report.Timings!;
        Assert.IsNull(timings.RequestReady);
        Assert.IsNull(timings.FirstByteReceived);
        Assert.IsTrue(timings.Completed > timings.Started);
    }

    private static async Task<TransferReport> RunAsync(TimeProvider clock, ConnectTimings? connected, string replies, Func<TransferContext, TransferContext> adjust, params byte[][] data)
    {
        var control = ScriptedConnection.FromReplies(replies);
        var connector = new QueuedConnector(ConnectResult.Connected(control, connected), ConnectResult.Connected(new ScriptedConnection(data)));
        TransferContext context = adjust(new TransferContext { Url = CurlUrl.Parse(FileUrl), Output = new MemoryStream(), TimeProvider = clock });

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(context);

        return result.Report!;
    }

    /// <summary>A clock that moves one tick further on every reading, so every recorded moment is distinct.</summary>
    private sealed class TickingTimeProvider : TimeProvider
    {
        private long now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Increment(ref now);
    }
}
