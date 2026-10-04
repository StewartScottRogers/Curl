using System.Net.Sockets;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins curl 8.21.0's words for a failed socket read or write: the Schannel build's
/// <c>get_winsock_error</c> table on Windows, the error's own (strerror) message elsewhere,
/// behind <c>Recv failure: </c> and <c>Send failure: </c>.
/// </summary>
[TestClass]
public sealed class CurlSocketErrorTextTests
{
    [TestMethod]
    [DataRow(SocketError.ConnectionAborted, "Connection was aborted")]
    [DataRow(SocketError.ConnectionReset, "Connection was reset")]
    [DataRow(SocketError.NetworkReset, "Network has been reset")]
    [DataRow(SocketError.NotConnected, "Socket is not connected")]
    [DataRow(SocketError.Shutdown, "Socket has been shut down")]
    [DataRow(SocketError.TimedOut, "Timed out")]
    [DataRow(SocketError.ConnectionRefused, "Connection refused")]
    [DataRow(SocketError.NetworkDown, "Network down")]
    [DataRow(SocketError.NetworkUnreachable, "Network unreachable")]
    [DataRow(SocketError.HostDown, "Host down")]
    [DataRow(SocketError.HostUnreachable, "Host unreachable")]
    [DataRow(SocketError.NoBufferSpaceAvailable, "No buffer space")]
    public void Words_OnWindows_UsesTheWinsockTable(SocketError error, string expected)
    {
        Assert.AreEqual(expected, CurlSocketErrorText.Words(new SocketException((int)error), isWindows: true));
    }

    [TestMethod]
    public void Words_OnWindowsForAnErrorOutsideTheTable_IsTheExceptionsOwnMessage()
    {
        var failure = new SocketException((int)SocketError.AccessDenied);

        Assert.AreEqual(failure.Message, CurlSocketErrorText.Words(failure, isWindows: true));
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionAborted)]
    public void Words_OffWindows_IsTheExceptionsOwnMessage(SocketError error)
    {
        var failure = new SocketException((int)error);

        Assert.AreEqual(failure.Message, CurlSocketErrorText.Words(failure, isWindows: false));
    }

    [TestMethod]
    public void ReceiveFailure_WithASocketErrorInside_IsCurlsRecvFailureLine()
    {
        var failure = new IOException("x", new SocketException((int)SocketError.ConnectionAborted));

        Assert.AreEqual("Recv failure: Connection was aborted", CurlSocketErrorText.ReceiveFailure(failure, isWindows: true));
    }

    [TestMethod]
    public void SendFailure_WithASocketErrorInside_IsCurlsSendFailureLine()
    {
        var failure = new IOException("x", new SocketException((int)SocketError.ConnectionAborted));

        Assert.AreEqual("Send failure: Connection was aborted", CurlSocketErrorText.SendFailure(failure, isWindows: true));
    }

    [TestMethod]
    public void SendFailure_WithTheSocketErrorTwoLevelsDown_FindsIt()
    {
        var failure = new IOException("outer", new InvalidOperationException("middle", new SocketException((int)SocketError.ConnectionReset)));

        Assert.AreEqual("Send failure: Connection was reset", CurlSocketErrorText.SendFailure(failure, isWindows: true));
    }

    [TestMethod]
    public void ReceiveFailure_WithNoSocketErrorInside_IsNull()
    {
        Assert.IsNull(CurlSocketErrorText.ReceiveFailure(new IOException("x", new InvalidOperationException("y")), isWindows: true));
    }

    [TestMethod]
    public void SendFailure_WithNoSocketErrorInside_IsNull()
    {
        Assert.IsNull(CurlSocketErrorText.SendFailure(new IOException("x"), isWindows: false));
    }

    [TestMethod]
    public void ReceiveFailure_ForThisPlatform_MatchesTheOverloadGivenThePlatform()
    {
        var failure = new IOException("x", new SocketException((int)SocketError.ConnectionReset));

        Assert.AreEqual(CurlSocketErrorText.ReceiveFailure(failure, OperatingSystem.IsWindows()), CurlSocketErrorText.ReceiveFailure(failure));
    }

    [TestMethod]
    public void SendFailure_ForThisPlatform_MatchesTheOverloadGivenThePlatform()
    {
        var failure = new IOException("x", new SocketException((int)SocketError.ConnectionReset));

        Assert.AreEqual(CurlSocketErrorText.SendFailure(failure, OperatingSystem.IsWindows()), CurlSocketErrorText.SendFailure(failure));
    }
}
