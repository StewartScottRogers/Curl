namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="TcpSocketOptions.FromCommandLine" /> turns <c>--keepalive-time</c> and
/// <c>--keepalive-cnt</c> into keepalive timers: 0 keeps libcurl's 60 seconds and 9 probes, as curl
/// 8.21.0's <c>--libcurl</c> output sets neither for 0 (BL-645 Notes), and a value past
/// <see cref="int.MaxValue" /> is clamped as libcurl clamps it.
/// </summary>
[TestClass]
public sealed class TcpSocketOptionsTests
{
    [TestMethod]
    public void Constructor_ByDefault_UsesLibcurlsKeepAliveTimers()
    {
        var options = new TcpSocketOptions();

        Assert.AreEqual(60, options.KeepAliveSeconds);
        Assert.AreEqual(9, options.KeepAliveProbeCount);
    }

    [TestMethod]
    public void FromCommandLine_WithZeros_KeepsLibcurlsDefaults()
    {
        Assert.AreEqual(new TcpSocketOptions(), TcpSocketOptions.FromCommandLine(true, true, 0, 0));
    }

    [TestMethod]
    public void FromCommandLine_WithValues_CarriesThemAndTheSwitches()
    {
        Assert.AreEqual(
            new TcpSocketOptions(NoDelay: false, KeepAlive: false, KeepAliveSeconds: 5, KeepAliveProbeCount: 3),
            TcpSocketOptions.FromCommandLine(false, false, 5, 3));
    }

    [TestMethod]
    public void FromCommandLine_PastIntMaxValue_ClampsToIt()
    {
        TcpSocketOptions options = TcpSocketOptions.FromCommandLine(true, true, long.MaxValue, (long)int.MaxValue + 1);

        Assert.AreEqual(int.MaxValue, options.KeepAliveSeconds);
        Assert.AreEqual(int.MaxValue, options.KeepAliveProbeCount);
    }
}
