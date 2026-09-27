namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that <see cref="ConnectTimings" /> carries the timestamps it is given, and
/// <see langword="null" /> for the events that did not happen.
/// </summary>
[TestClass]
public sealed class ConnectTimingsTests
{
    [TestMethod]
    public void New_WithEveryTimestamp_CarriesEachOne()
    {
        var timings = new ConnectTimings(10, 20, 30, 40);

        Assert.AreEqual(10L, timings.Started);
        Assert.AreEqual(20L, timings.NameResolved);
        Assert.AreEqual(30L, timings.Connected);
        Assert.AreEqual(40L, timings.TlsHandshakeCompleted);
    }

    [TestMethod]
    public void New_ForALiteralAddressOverPlainText_LeavesResolveAndHandshakeNull()
    {
        var timings = new ConnectTimings(10, null, 30, null);

        Assert.IsNull(timings.NameResolved);
        Assert.IsNull(timings.TlsHandshakeCompleted);
    }

    [TestMethod]
    public void With_ReplacingEveryTimestamp_CarriesTheNewValues()
    {
        var timings = new ConnectTimings(0, null, 0, null) with
        {
            Started = 10,
            NameResolved = 20,
            Connected = 30,
            TlsHandshakeCompleted = 40,
        };

        Assert.AreEqual(new ConnectTimings(10, 20, 30, 40), timings);
    }
}
