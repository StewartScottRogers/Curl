using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the address and port range a <see cref="ListenTarget" /> accepts.
/// </summary>
[TestClass]
public sealed class ListenTargetTests
{
    [TestMethod]
    public void Constructor_WithValidValues_RoundTripsEveryValue()
    {
        var target = new ListenTarget(IPAddress.Loopback, 5000, 5010);

        Assert.AreEqual(IPAddress.Loopback, target.Address);
        Assert.AreEqual(5000, target.LowPort);
        Assert.AreEqual(5010, target.HighPort);
    }

    [TestMethod]
    public void Constructor_WithPortZeroToZero_AcceptsAnyFreePort()
    {
        var target = new ListenTarget(IPAddress.IPv6Loopback, 0, 0);

        Assert.AreEqual(0, target.LowPort);
        Assert.AreEqual(0, target.HighPort);
    }

    [TestMethod]
    public void Constructor_WithFullPortRange_Accepts()
    {
        var target = new ListenTarget(IPAddress.Any, 0, 65535);

        Assert.AreEqual(65535, target.HighPort);
    }

    [TestMethod]
    public void Constructor_WithNullAddress_ThrowsArgumentNullException()
    {
        IPAddress? address = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new ListenTarget(address!, 0, 0));

        Assert.AreEqual("Address", exception.ParamName);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(65536)]
    public void Constructor_WithLowPortOutsideRange_ThrowsArgumentOutOfRangeException(int port)
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ListenTarget(IPAddress.Loopback, port, 65535));

        Assert.AreEqual("LowPort", exception.ParamName);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(65536)]
    public void Constructor_WithHighPortOutsideRange_ThrowsArgumentOutOfRangeException(int port)
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ListenTarget(IPAddress.Loopback, 0, port));

        Assert.AreEqual("HighPort", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithLowPortAboveHighPort_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ListenTarget(IPAddress.Loopback, 5010, 5000));

        Assert.AreEqual("HighPort", exception.ParamName);
    }
}
