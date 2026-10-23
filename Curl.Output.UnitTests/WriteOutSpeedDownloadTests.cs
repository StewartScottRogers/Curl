using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <c>%{speed_download}</c> to curl 8.22.0's <c>trspeed</c>: the bytes times a million over
/// the total time in microseconds, truncated (BL-3186). Each row's size and total are what curl
/// 8.22.0 Schannel measured for a <c>file://</c> download (see BL-3186's Notes), its total the
/// divisor curl used, which is its last progress update a few microseconds before
/// <c>time_total</c> (ADR-0035); the large size follows <c>trspeed</c>'s overflow branch.
/// </summary>
[TestClass]
public sealed class WriteOutSpeedDownloadTests
{
    private const string LoopbackUrl = "http://127.0.0.1/";

    private static readonly TimeProvider Clock = new MicrosecondTimeProvider();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(1L, 840L, "1190")]
    [DataRow(5_000_000L, 11_625L, "430107526")]
    [DataRow(5_000_000L, 2_552L, "1959247648")]
    [DataRow(379L, 1_000_000L, "379")]
    [DataRow(long.MaxValue / 1000, 2_000_000L, "4611686018427387")]
    [DataRow(long.MaxValue / 1000, 999_999L, "9223372036854775807")]
    public void TryGetVariableText_SizeOverTotalTime_PrintsTrspeedTruncated(long size, long totalMicroseconds, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferReport report = new()
        {
            DownloadSize = size,
            Timings = new TransferTimings(Started: 0, Connect: null, RequestReady: null, RequestSent: null, FirstByteReceived: null, Completed: totalMicroseconds),
        };
        diagnostics.Arrange("download size", size);
        diagnostics.Arrange("total (microseconds)", totalMicroseconds);

        string actual = SpeedDownload(report);

        diagnostics.Act("speed_download", actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(4L)]
    public void TryGetVariableText_ZeroTotalTime_PrintsZero(long size)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // A report without timings has a zero total: curl 8.22.0 prints 0 for a transfer that moved
        // no bytes (a refused connect: "0 0.000645 0"), and an unmeasured time gives no speed.
        TransferReport report = new() { DownloadSize = size };
        diagnostics.Arrange("download size", size);

        string actual = SpeedDownload(report);

        diagnostics.Act("speed_download", actual);
        Assert.AreEqual("0", actual);
    }

    private static string SpeedDownload(TransferReport report)
    {
        TransferWriteOutVariables variables = new(TransferResult.Success(0) with { Report = report }, LoopbackUrl, 0, LoopbackUrl, "http", Clock);
        Assert.IsTrue(variables.TryGetVariableText("speed_download", out string? text));
        return text;
    }

    private sealed class MicrosecondTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => 1_000_000;
    }
}
