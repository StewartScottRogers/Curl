using System.Net.Sockets;

namespace Curl.Protocol.Telnet;

[TestClass]
public sealed class TelnetSocketErrorTextTests
{
    [TestMethod]
    [DataRow(SocketError.ConnectionReset, "Connection was reset")]
    [DataRow(SocketError.ConnectionAborted, "Connection was aborted")]
    public void For_WindowsAndAWinsockTableError_ReturnsTheSchannelBuildsWords(SocketError error, string expected)
    {
        string text = TelnetSocketErrorText.For(new SocketException((int)error), isWindows: true);

        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void For_WindowsAndAnotherError_ReturnsTheErrorsOwnMessage()
    {
        var failure = new SocketException((int)SocketError.Shutdown);

        string text = TelnetSocketErrorText.For(failure, isWindows: true);

        Assert.AreEqual(failure.Message, text);
    }

    [TestMethod]
    public void For_OffWindows_ReturnsTheErrorsOwnMessageEvenForAWinsockTableError()
    {
        var failure = new SocketException((int)SocketError.ConnectionReset);

        string text = TelnetSocketErrorText.For(failure, isWindows: false);

        Assert.AreEqual(failure.Message, text);
    }

    [TestMethod]
    public void Current_AnyError_WordsItForThisPlatform()
    {
        var failure = new SocketException((int)SocketError.ConnectionAborted);

        string text = TelnetSocketErrorText.Current(failure);

        Assert.AreEqual(TelnetSocketErrorText.For(failure, OperatingSystem.IsWindows()), text);
    }
}
