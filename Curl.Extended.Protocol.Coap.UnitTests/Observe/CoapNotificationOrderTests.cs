namespace Curl.Extended.Protocol.Coap.Observe;

/// <summary>Checks notification ordering against RFC 7641 section 3.4.</summary>
[TestClass]
public sealed class CoapNotificationOrderTests
{
    private static readonly DateTimeOffset LastArrival = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow(5u, 6u, 0, true, DisplayName = "V2 just ahead of V1")]
    [DataRow(5u, 5u, 0, false, DisplayName = "the same number")]
    [DataRow(6u, 5u, 0, false, DisplayName = "V2 just behind V1")]
    [DataRow(0u, 8388608u, 0, false, DisplayName = "V2 2^23 ahead is behind")]
    [DataRow(0u, 8388607u, 0, true, DisplayName = "V2 2^23 - 1 ahead")]
    [DataRow(16777215u, 0u, 0, true, DisplayName = "V2 wrapped past 2^24")]
    [DataRow(8388608u, 0u, 0, false, DisplayName = "V1 2^23 ahead of V2")]
    [DataRow(8388609u, 0u, 0, true, DisplayName = "V1 2^23 + 1 ahead of V2 wraps")]
    [DataRow(6u, 5u, 128, false, DisplayName = "an older number at exactly 128 seconds")]
    [DataRow(6u, 5u, 129, true, DisplayName = "an older number after 128 seconds")]
    public void IsNewer_ANotification_FollowsSection3Point4(uint lastSequence, uint sequence, int secondsLater, bool newer) =>
        Assert.AreEqual(newer, CoapNotificationOrder.IsNewer(lastSequence, LastArrival, sequence, LastArrival.AddSeconds(secondsLater)));
}
