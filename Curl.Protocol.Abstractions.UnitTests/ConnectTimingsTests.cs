using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that <see cref="ConnectTimings" /> carries the timestamps it is given, and
/// <see langword="null" /> for the events that did not happen.
/// </summary>
[TestClass]
public sealed class ConnectTimingsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void New_WithEveryTimestamp_CarriesEachOne()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timestamps", "10, 20, 30, 40");

        var timings = new ConnectTimings(10, 20, 30, 40);

        diagnostics.Act("timings", timings);
        diagnostics.Assert("tls handshake completed", 40L, timings.TlsHandshakeCompleted);
        Assert.AreEqual(10L, timings.Started);
        Assert.AreEqual(20L, timings.NameResolved);
        Assert.AreEqual(30L, timings.Connected);
        Assert.AreEqual(40L, timings.TlsHandshakeCompleted);
    }

    [TestMethod]
    public void New_WithoutResolveOrHandshake_LeavesThemNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timestamps", "10, null, 30, null");

        var timings = new ConnectTimings(10, null, 30, null);

        diagnostics.Act("name resolved", timings.NameResolved);
        diagnostics.Act("tls handshake completed", timings.TlsHandshakeCompleted);
        diagnostics.Assert("name resolved", null, timings.NameResolved);
        Assert.IsNull(timings.NameResolved);
        Assert.IsNull(timings.TlsHandshakeCompleted);
    }

    [TestMethod]
    public void New_ForAFailedDial_LeavesConnectedNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("timestamps", "10, 20, null, null");

        var timings = new ConnectTimings(10, 20, null, null);

        diagnostics.Act("name resolved", timings.NameResolved);
        diagnostics.Act("connected", timings.Connected);
        diagnostics.Assert("name resolved", 20L, timings.NameResolved);
        Assert.AreEqual(20L, timings.NameResolved);
        Assert.IsNull(timings.Connected);
    }

    [TestMethod]
    public void With_ReplacingEveryTimestamp_CarriesTheNewValues()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("replacement timestamps", "10, 20, 30, 40");

        var timings = new ConnectTimings(0, null, 0, null) with
        {
            Started = 10,
            NameResolved = 20,
            Connected = 30,
            TlsHandshakeCompleted = 40,
        };

        var expected = new ConnectTimings(10, 20, 30, 40);
        diagnostics.Act("timings", timings);
        diagnostics.Assert("timings", expected, timings);
        Assert.AreEqual(new ConnectTimings(10, 20, 30, 40), timings);
    }
}
