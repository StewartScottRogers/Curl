using System.Net.Sockets;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the reason <see cref="ConnectFailureReason" /> gives each platform's build.
/// </summary>
[TestClass]
public sealed class ConnectFailureReasonTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Describe_WithNullException_ThrowsArgumentNullException()
    {
        Diagnostics.Arrange("exception", "null");
        Diagnostics.Arrange("uses Winsock wording", true);
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ConnectFailureReason.Describe(null!, usesWinsockWording: true));
        Diagnostics.Act("exception type", exception.GetType().Name);
        Diagnostics.Act("parameter name", exception.ParamName);
        Diagnostics.Assert("parameter name", "exception", exception.ParamName);

        Assert.AreEqual("exception", exception.ParamName);
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionRefused, "Connection refused")]
    [DataRow(SocketError.TimedOut, "Timed out")]
    [DataRow(SocketError.NetworkUnreachable, "Network unreachable")]
    [DataRow(SocketError.HostUnreachable, "Host unreachable")]
    [DataRow(SocketError.AddressNotAvailable, "Address not available")]
    [DataRow(SocketError.NetworkDown, "Network down")]
    [DataRow(SocketError.InvalidArgument, "Invalid arguments")]
    [DataRow(SocketError.AddressAlreadyInUse, "Address already in use")]
    public void Describe_WithWinsockWording_GivesCurlsWords(SocketError error, string expected)
    {
        Diagnostics.Arrange("socket error", error);
        Diagnostics.Arrange("uses Winsock wording", true);
        var reason = ConnectFailureReason.Describe(new SocketException((int)error), usesWinsockWording: true);
        Diagnostics.Act("reason", reason);
        Diagnostics.Assert("reason", expected, reason);

        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    public void Describe_WithWinsockWordingForAnErrorCurlDoesNotWord_GivesTheSystemMessage()
    {
        Diagnostics.Arrange("socket error", SocketError.AccessDenied);
        Diagnostics.Arrange("uses Winsock wording", true);
        var exception = new SocketException((int)SocketError.AccessDenied);

        var reason = ConnectFailureReason.Describe(exception, usesWinsockWording: true);
        Diagnostics.Act("reason equals system message", reason == exception.Message);
        Diagnostics.Assert("reason equals system message", true, reason == exception.Message);

        Assert.AreEqual(exception.Message, reason);
    }

    [TestMethod]
    public void Describe_WithoutWinsockWording_GivesTheSystemMessage()
    {
        Diagnostics.Arrange("socket error", SocketError.ConnectionRefused);
        Diagnostics.Arrange("uses Winsock wording", false);
        var exception = new SocketException((int)SocketError.ConnectionRefused);

        var reason = ConnectFailureReason.Describe(exception, usesWinsockWording: false);
        Diagnostics.Act("reason equals system message", reason == exception.Message);
        Diagnostics.Assert("reason equals system message", true, reason == exception.Message);

        Assert.AreEqual(exception.Message, reason);
    }
}
