using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins the reason <see cref="ConnectFailureReason" /> gives each platform's build.
/// </summary>
[TestClass]
public sealed class ConnectFailureReasonTests
{
    [TestMethod]
    public void Describe_WithNullException_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ConnectFailureReason.Describe(null!, usesWinsockWording: true));

        Assert.AreEqual("exception", exception.ParamName);
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionRefused, "Connection refused")]
    [DataRow(SocketError.TimedOut, "Timed out")]
    [DataRow(SocketError.NetworkUnreachable, "Network unreachable")]
    [DataRow(SocketError.HostUnreachable, "Host unreachable")]
    [DataRow(SocketError.AddressNotAvailable, "Address not available")]
    public void Describe_WithWinsockWording_GivesCurlsWords(SocketError error, string expected)
    {
        var reason = ConnectFailureReason.Describe(new SocketException((int)error), usesWinsockWording: true);

        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    public void Describe_WithWinsockWordingForAnErrorCurlDoesNotWord_GivesTheSystemMessage()
    {
        var exception = new SocketException((int)SocketError.AccessDenied);

        var reason = ConnectFailureReason.Describe(exception, usesWinsockWording: true);

        Assert.AreEqual(exception.Message, reason);
    }

    [TestMethod]
    public void Describe_WithoutWinsockWording_GivesTheSystemMessage()
    {
        var exception = new SocketException((int)SocketError.ConnectionRefused);

        var reason = ConnectFailureReason.Describe(exception, usesWinsockWording: false);

        Assert.AreEqual(exception.Message, reason);
    }
}
