using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that a <see cref="DatagramReceived" /> carries its length and source endpoint,
/// and compares by value.
/// </summary>
[TestClass]
public sealed class DatagramReceivedTests
{
    [TestMethod]
    public void Constructor_RoundTripsLengthAndRemoteEndPoint()
    {
        var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 49152);

        var received = new DatagramReceived(516, remoteEndPoint);

        Assert.AreEqual(516, received.Length);
        Assert.AreSame(remoteEndPoint, received.RemoteEndPoint);
    }

    [TestMethod]
    public void Equals_ForTheSameLengthFromADifferentPort_ReturnsFalse()
    {
        var fromServer = new DatagramReceived(4, new IPEndPoint(IPAddress.Loopback, 49152));
        var fromStranger = new DatagramReceived(4, new IPEndPoint(IPAddress.Loopback, 49153));

        Assert.AreNotEqual(fromServer, fromStranger);
    }

    [TestMethod]
    public void With_SettingEveryProperty_ReturnsCopyWithNewValuesAndLeavesOriginalUnchanged()
    {
        var originalEndPoint = new IPEndPoint(IPAddress.Loopback, 49152);
        var newEndPoint = new IPEndPoint(IPAddress.Loopback, 49153);
        var original = new DatagramReceived(4, originalEndPoint);

        var copy = original with { Length = 516, RemoteEndPoint = newEndPoint };

        Assert.AreEqual(516, copy.Length);
        Assert.AreSame(newEndPoint, copy.RemoteEndPoint);
        Assert.AreEqual(4, original.Length);
        Assert.AreSame(originalEndPoint, original.RemoteEndPoint);
    }
}
