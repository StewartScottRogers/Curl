namespace Curl.Extension.Protocol.Grpc.Wire;

/// <summary>Checks the Timeout (TimeoutValue TimeoutUnit) form of PROTOCOL-HTTP2.md "Requests".</summary>
[TestClass]
public sealed class GrpcTimeoutTests
{
    [TestMethod]
    [DataRow(0L, "0n")]
    [DataRow(1L, "100n")]
    [DataRow(10_000_000L, "1000000u")]
    [DataRow(999_999L, "99999900n")]
    [DataRow(1_000_000L, "100000u")]
    [DataRow(1_000_001L, "100001u")]
    [DataRow(1_000_000_000L, "100000m")]
    [DataRow(1_000_000_000_000L, "100000S")]
    [DataRow(1_000_000_000_001L, "100001S")]
    [DataRow(1_000_000_000_000_000L, "1666667M")]
    [DataRow(100_000_000_000_000_000L, "2777778H")]
    public void Format_Requests_UsesTheFinestUnitThatFitsEightDigitsRoundingUp(long ticks, string expected) =>
        Assert.AreEqual(expected, GrpcTimeout.Format(TimeSpan.FromTicks(ticks)));

    [TestMethod]
    public void Format_Requests_PastTheLargestValue_IsTheLargestValue() =>
        Assert.AreEqual("99999999H", GrpcTimeout.Format(TimeSpan.MaxValue));

    [TestMethod]
    public void Format_Requests_Minutes_AreUsedBetweenSecondsAndHours() =>
        Assert.AreEqual("1666667M", GrpcTimeout.Format(TimeSpan.FromSeconds(100_000_000)));

    [TestMethod]
    public void Format_Requests_Negative_IsRefused() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GrpcTimeout.Format(TimeSpan.FromTicks(-1)));
}
