using System.Net.Sockets;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins curl 8.21.0's words for a failed socket read or write: the Schannel build's
/// <c>get_winsock_error</c> table on Windows, the error's own (strerror) message elsewhere,
/// behind <c>Recv failure: </c> and <c>Send failure: </c>.
/// </summary>
[TestClass]
public sealed class CurlSocketErrorTextTests
{
    public TestContext TestContext { get; set; } = null!;

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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("socket error", error);
        diagnostics.Arrange("is windows", true);

        string actual = CurlSocketErrorText.Words(new SocketException((int)error), isWindows: true);

        diagnostics.Act("words", actual);
        diagnostics.Diff("words", expected, actual);
        Assert.AreEqual(expected, CurlSocketErrorText.Words(new SocketException((int)error), isWindows: true));
    }

    [TestMethod]
    public void Words_OnWindowsForAnErrorOutsideTheTable_IsTheExceptionsOwnMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var failure = new SocketException((int)SocketError.AccessDenied);
        diagnostics.Arrange("socket error", SocketError.AccessDenied);
        diagnostics.Arrange("is windows", true);

        string words = CurlSocketErrorText.Words(failure, isWindows: true);

        diagnostics.Act("words equal the exception message", words == failure.Message);
        diagnostics.Assert("words equal the exception message", true, words == failure.Message);
        Assert.AreEqual(failure.Message, CurlSocketErrorText.Words(failure, isWindows: true));
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionAborted)]
    public void Words_OffWindows_IsTheExceptionsOwnMessage(SocketError error)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var failure = new SocketException((int)error);
        diagnostics.Arrange("socket error", error);
        diagnostics.Arrange("is windows", false);

        string words = CurlSocketErrorText.Words(failure, isWindows: false);

        diagnostics.Act("words equal the exception message", words == failure.Message);
        diagnostics.Assert("words equal the exception message", true, words == failure.Message);
        Assert.AreEqual(failure.Message, CurlSocketErrorText.Words(failure, isWindows: false));
    }

    [TestMethod]
    public void ReceiveFailure_WithASocketErrorInside_IsCurlsRecvFailureLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var failure = new IOException("x", new SocketException((int)SocketError.ConnectionAborted));
        diagnostics.Arrange("inner socket error", SocketError.ConnectionAborted);
        diagnostics.Arrange("is windows", true);

        string? line = CurlSocketErrorText.ReceiveFailure(failure, isWindows: true);

        diagnostics.Act("line", line);
        diagnostics.Diff("line", "Recv failure: Connection was aborted", line!);
        Assert.AreEqual("Recv failure: Connection was aborted", CurlSocketErrorText.ReceiveFailure(failure, isWindows: true));
    }

    [TestMethod]
    public void SendFailure_WithASocketErrorInside_IsCurlsSendFailureLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var failure = new IOException("x", new SocketException((int)SocketError.ConnectionAborted));
        diagnostics.Arrange("inner socket error", SocketError.ConnectionAborted);
        diagnostics.Arrange("is windows", true);

        string? line = CurlSocketErrorText.SendFailure(failure, isWindows: true);

        diagnostics.Act("line", line);
        diagnostics.Diff("line", "Send failure: Connection was aborted", line!);
        Assert.AreEqual("Send failure: Connection was aborted", CurlSocketErrorText.SendFailure(failure, isWindows: true));
    }

    [TestMethod]
    public void SendFailure_WithTheSocketErrorTwoLevelsDown_FindsIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var failure = new IOException("outer", new InvalidOperationException("middle", new SocketException((int)SocketError.ConnectionReset)));
        diagnostics.Arrange("nesting", "IOException > InvalidOperationException > SocketException");
        diagnostics.Arrange("inner socket error", SocketError.ConnectionReset);

        string? line = CurlSocketErrorText.SendFailure(failure, isWindows: true);

        diagnostics.Act("line", line);
        diagnostics.Diff("line", "Send failure: Connection was reset", line!);
        Assert.AreEqual("Send failure: Connection was reset", CurlSocketErrorText.SendFailure(failure, isWindows: true));
    }

    [TestMethod]
    public void ReceiveFailure_WithNoSocketErrorInside_IsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("nesting", "IOException > InvalidOperationException");

        string? line = CurlSocketErrorText.ReceiveFailure(new IOException("x", new InvalidOperationException("y")), isWindows: true);

        diagnostics.Act("line", line);
        diagnostics.Assert("line", null, line);
        Assert.IsNull(CurlSocketErrorText.ReceiveFailure(new IOException("x", new InvalidOperationException("y")), isWindows: true));
    }

    [TestMethod]
    public void SendFailure_WithNoSocketErrorInside_IsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("nesting", "IOException alone");
        diagnostics.Arrange("is windows", false);

        string? line = CurlSocketErrorText.SendFailure(new IOException("x"), isWindows: false);

        diagnostics.Act("line", line);
        diagnostics.Assert("line", null, line);
        Assert.IsNull(CurlSocketErrorText.SendFailure(new IOException("x"), isWindows: false));
    }

    [TestMethod]
    public void ReceiveFailure_ForThisPlatform_MatchesTheOverloadGivenThePlatform()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var failure = new IOException("x", new SocketException((int)SocketError.ConnectionReset));
        diagnostics.Arrange("inner socket error", SocketError.ConnectionReset);

        string? given = CurlSocketErrorText.ReceiveFailure(failure, OperatingSystem.IsWindows());
        string? implicitPlatform = CurlSocketErrorText.ReceiveFailure(failure);

        diagnostics.Act("overloads agree", given == implicitPlatform);
        diagnostics.Assert("overloads agree", true, given == implicitPlatform);
        Assert.AreEqual(CurlSocketErrorText.ReceiveFailure(failure, OperatingSystem.IsWindows()), CurlSocketErrorText.ReceiveFailure(failure));
    }

    [TestMethod]
    public void SendFailure_ForThisPlatform_MatchesTheOverloadGivenThePlatform()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var failure = new IOException("x", new SocketException((int)SocketError.ConnectionReset));
        diagnostics.Arrange("inner socket error", SocketError.ConnectionReset);

        string? given = CurlSocketErrorText.SendFailure(failure, OperatingSystem.IsWindows());
        string? implicitPlatform = CurlSocketErrorText.SendFailure(failure);

        diagnostics.Act("overloads agree", given == implicitPlatform);
        diagnostics.Assert("overloads agree", true, given == implicitPlatform);
        Assert.AreEqual(CurlSocketErrorText.SendFailure(failure, OperatingSystem.IsWindows()), CurlSocketErrorText.SendFailure(failure));
    }
}
