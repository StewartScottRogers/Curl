using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that a <see cref="DatagramReceived" /> carries its length and source endpoint,
/// and compares by value.
/// </summary>
[TestClass]
public sealed class DatagramReceivedTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_RoundTripsLengthAndRemoteEndPoint()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 49152);
        diagnostics.Arrange("length", 516);
        diagnostics.Arrange("remote end point", remoteEndPoint);

        var received = new DatagramReceived(516, remoteEndPoint);

        diagnostics.Act("length", received.Length);
        diagnostics.Act("remote end point", received.RemoteEndPoint);
        diagnostics.Assert("length", 516, received.Length);
        Assert.AreEqual(516, received.Length);
        Assert.AreSame(remoteEndPoint, received.RemoteEndPoint);
    }

    [TestMethod]
    public void Equals_ForTheSameLengthFromADifferentPort_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ports", "49152, 49153");
        var fromServer = new DatagramReceived(4, new IPEndPoint(IPAddress.Loopback, 49152));
        var fromStranger = new DatagramReceived(4, new IPEndPoint(IPAddress.Loopback, 49153));

        diagnostics.Act("equal", fromServer.Equals(fromStranger));
        diagnostics.Assert("equal", false, fromServer.Equals(fromStranger));
        Assert.AreNotEqual(fromServer, fromStranger);
    }

    [TestMethod]
    public void With_SettingEveryProperty_ReturnsCopyWithNewValuesAndLeavesOriginalUnchanged()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var originalEndPoint = new IPEndPoint(IPAddress.Loopback, 49152);
        var newEndPoint = new IPEndPoint(IPAddress.Loopback, 49153);
        var original = new DatagramReceived(4, originalEndPoint);
        diagnostics.Arrange("original", original);

        var copy = original with { Length = 516, RemoteEndPoint = newEndPoint };

        diagnostics.Act("copy", copy);
        diagnostics.Act("original after", original);
        diagnostics.Assert("copy length", 516, copy.Length);
        Assert.AreEqual(516, copy.Length);
        Assert.AreSame(newEndPoint, copy.RemoteEndPoint);
        Assert.AreEqual(4, original.Length);
        Assert.AreSame(originalEndPoint, original.RemoteEndPoint);
    }
}
