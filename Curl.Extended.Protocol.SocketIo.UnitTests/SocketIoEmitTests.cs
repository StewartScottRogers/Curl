using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SocketIo;

/// <summary>Checks <see cref="SocketIoEmit" />: the <c>-d</c> data is an EVENT packet's array, its first element the event name (Socket.IO v5, "Sending and receiving data").</summary>
[TestClass]
public sealed class SocketIoEmitTests
{
    [TestMethod]
    public void From_WithoutData_GivesNull() => Assert.IsNull(SocketIoEmit.From(null));

    [TestMethod]
    public void From_AnArrayStartingWithAName_GivesItsText() =>
        Assert.AreEqual("[\"chat\", {\"a\": 1}]", SocketIoEmit.From(Encoding.UTF8.GetBytes("[\"chat\", {\"a\": 1}]")));

    [TestMethod]
    [DataRow("{}")]
    [DataRow("[]")]
    [DataRow("[1]")]
    [DataRow("not json")]
    public void From_OtherData_FailsWithExit2(string data)
    {
        SocketIoException failure = Assert.ThrowsExactly<SocketIoException>(() => SocketIoEmit.From(Encoding.UTF8.GetBytes(data)));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(SocketIoEmit.NotAnEventMessage, failure.Message);
    }
}
