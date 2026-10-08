using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the address and port range a <see cref="ListenTarget" /> accepts.
/// </summary>
[TestClass]
public sealed class ListenTargetTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_WithValidValues_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("address and ports", "127.0.0.1, 5000-5010");

        var target = new ListenTarget(IPAddress.Loopback, 5000, 5010);

        diagnostics.Act("address", target.Address);
        diagnostics.Act("port range", $"{target.LowPort}-{target.HighPort}");
        diagnostics.Assert("low port", 5000, target.LowPort);
        Assert.AreEqual(IPAddress.Loopback, target.Address);
        Assert.AreEqual(5000, target.LowPort);
        Assert.AreEqual(5010, target.HighPort);
    }

    [TestMethod]
    public void Constructor_WithPortZeroToZero_AcceptsAnyFreePort()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ports", "0-0");

        var target = new ListenTarget(IPAddress.IPv6Loopback, 0, 0);

        diagnostics.Act("port range", $"{target.LowPort}-{target.HighPort}");
        diagnostics.Assert("low port", 0, target.LowPort);
        Assert.AreEqual(0, target.LowPort);
        Assert.AreEqual(0, target.HighPort);
    }

    [TestMethod]
    public void Constructor_WithFullPortRange_Accepts()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ports", "0-65535");

        var target = new ListenTarget(IPAddress.Any, 0, 65535);

        diagnostics.Act("high port", target.HighPort);
        diagnostics.Assert("high port", 65535, target.HighPort);
        Assert.AreEqual(65535, target.HighPort);
    }

    [TestMethod]
    public void Constructor_WithNullAddress_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IPAddress? address = null;
        diagnostics.Arrange("address", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new ListenTarget(address!, 0, 0));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "Address", exception.ParamName);
        Assert.AreEqual("Address", exception.ParamName);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(65536)]
    public void Constructor_WithLowPortOutsideRange_ThrowsArgumentOutOfRangeException(int port)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("low port", port);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ListenTarget(IPAddress.Loopback, port, 65535));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "LowPort", exception.ParamName);
        Assert.AreEqual("LowPort", exception.ParamName);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(65536)]
    public void Constructor_WithHighPortOutsideRange_ThrowsArgumentOutOfRangeException(int port)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("high port", port);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ListenTarget(IPAddress.Loopback, 0, port));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "HighPort", exception.ParamName);
        Assert.AreEqual("HighPort", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithLowPortAboveHighPort_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ports", "5010-5000");

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ListenTarget(IPAddress.Loopback, 5010, 5000));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "HighPort", exception.ParamName);
        Assert.AreEqual("HighPort", exception.ParamName);
    }
}
