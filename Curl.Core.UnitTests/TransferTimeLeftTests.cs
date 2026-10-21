namespace Curl.Core;

/// <summary>Pins <see cref="TransferTimeLeft" /> to the rows of upstream's <c>tests/unit/unit1303.c</c>.</summary>
[TestClass]
public sealed class TransferTimeLeftTests
{
    [TestMethod]
    [DataRow(10000, 8000, false, 4000, 6000L, DisplayName = "-m left after connect")]
    [DataRow(10000, 8000, false, 10000, -1L, DisplayName = "-m exactly expired is -1")]
    [DataRow(10000, 8000, false, 12000, -2000L, DisplayName = "-m overdue is negative")]
    [DataRow(10000, 8000, true, 4990, 3010L, DisplayName = "connecting, the shorter connect timeout counts")]
    [DataRow(10000, 8000, true, 8000, -1L, DisplayName = "connecting, connect timeout exactly expired")]
    [DataRow(10000, 12000, true, 4000, 6000L, DisplayName = "connecting, the shorter -m counts")]
    [DataRow(0, 10000, false, 4000, 0L, DisplayName = "connected with only a connect timeout, none active")]
    [DataRow(0, 10000, true, 12000, -2000L, DisplayName = "connecting with only a connect timeout")]
    [DataRow(0, 0, false, 4000, 0L, DisplayName = "no timeout set, none active")]
    [DataRow(0, 0, true, 4990, 295010L, DisplayName = "connecting with none set, the 300 s default")]
    public void Milliseconds_Unit1303Rows_AnswersCurlTimeleftMs(int maxTimeMs, int connectTimeoutMs, bool connecting, int elapsedMs, long expected)
    {
        long left = TransferTimeLeft.Milliseconds(
            TimeSpan.FromMilliseconds(maxTimeMs),
            TimeSpan.FromMilliseconds(connectTimeoutMs),
            connecting,
            TimeSpan.FromMilliseconds(elapsedMs));

        Assert.AreEqual(expected, left);
    }

    [TestMethod]
    public void DefaultConnectTimeout_Is300Seconds()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(300), TransferTimeLeft.DefaultConnectTimeout);
    }
}
