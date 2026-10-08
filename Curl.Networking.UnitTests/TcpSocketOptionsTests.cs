using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_ByDefault_UsesLibcurlsKeepAliveTimers()
    {
        Diagnostics.Arrange("constructor arguments", "none");

        var options = new TcpSocketOptions();

        Diagnostics.Act("keep-alive seconds", options.KeepAliveSeconds);
        Diagnostics.Act("keep-alive probe count", options.KeepAliveProbeCount);
        Diagnostics.Assert("keep-alive seconds", 60, options.KeepAliveSeconds);
        Diagnostics.Assert("keep-alive probe count", 9, options.KeepAliveProbeCount);

        Assert.AreEqual(60, options.KeepAliveSeconds);
        Assert.AreEqual(9, options.KeepAliveProbeCount);
    }

    [TestMethod]
    public void FromCommandLine_WithZeros_KeepsLibcurlsDefaults()
    {
        Diagnostics.Arrange("no delay, keep alive, seconds, probe count", "True, True, 0, 0");

        var expected = new TcpSocketOptions();
        var actual = TcpSocketOptions.FromCommandLine(true, true, 0, 0);

        Diagnostics.Act("options", actual);
        Diagnostics.Assert("options", expected, actual);

        Assert.AreEqual(new TcpSocketOptions(), TcpSocketOptions.FromCommandLine(true, true, 0, 0));
    }

    [TestMethod]
    public void FromCommandLine_WithValues_CarriesThemAndTheSwitches()
    {
        Diagnostics.Arrange("no delay, keep alive, seconds, probe count", "False, False, 5, 3");

        var expected = new TcpSocketOptions(NoDelay: false, KeepAlive: false, KeepAliveSeconds: 5, KeepAliveProbeCount: 3);
        var actual = TcpSocketOptions.FromCommandLine(false, false, 5, 3);

        Diagnostics.Act("options", actual);
        Diagnostics.Assert("options", expected, actual);

        Assert.AreEqual(
            new TcpSocketOptions(NoDelay: false, KeepAlive: false, KeepAliveSeconds: 5, KeepAliveProbeCount: 3),
            TcpSocketOptions.FromCommandLine(false, false, 5, 3));
    }

    [TestMethod]
    public void FromCommandLine_PastIntMaxValue_ClampsToIt()
    {
        Diagnostics.Arrange("no delay, keep alive, seconds, probe count", $"True, True, {long.MaxValue}, {(long)int.MaxValue + 1}");

        TcpSocketOptions options = TcpSocketOptions.FromCommandLine(true, true, long.MaxValue, (long)int.MaxValue + 1);

        Diagnostics.Act("keep-alive seconds", options.KeepAliveSeconds);
        Diagnostics.Act("keep-alive probe count", options.KeepAliveProbeCount);
        Diagnostics.Assert("keep-alive seconds", int.MaxValue, options.KeepAliveSeconds);
        Diagnostics.Assert("keep-alive probe count", int.MaxValue, options.KeepAliveProbeCount);

        Assert.AreEqual(int.MaxValue, options.KeepAliveSeconds);
        Assert.AreEqual(int.MaxValue, options.KeepAliveProbeCount);
    }
}
