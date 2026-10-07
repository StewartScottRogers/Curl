using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins how <c>--tcp-fastopen</c> and <c>--mptcp</c> reach the socket on each operating system (BL-647,
/// ADR-0317): the Fast Open option in each system's numbers, and the protocol the socket is opened with.
/// </summary>
[TestClass]
public sealed class FastOpenSocketOptionTests
{
    [TestMethod]
    [DataRow(SocketPlatform.Windows, 15)]
    [DataRow(SocketPlatform.Linux, 30)]
    [DataRow(SocketPlatform.Darwin, 0x105)]
    public void For_APlatformWithFastOpen_SetsItsOptionAtTheTcpLevel(SocketPlatform platform, int name)
    {
        CollectionAssert.AreEqual(new[] { new RawSocketOption(6, name, 1) }, FastOpenSocketOption.For(new TcpSocketOptions(FastOpen: true), platform).ToArray());
    }

    [TestMethod]
    [DataRow(SocketPlatform.FreeBsd)]
    [DataRow(SocketPlatform.Other)]
    public void For_APlatformWithoutAKnownOption_SetsNothing(SocketPlatform platform)
    {
        Assert.IsEmpty(FastOpenSocketOption.For(new TcpSocketOptions(FastOpen: true), platform));
    }

    [TestMethod]
    [DataRow(SocketPlatform.Windows)]
    [DataRow(SocketPlatform.Linux)]
    [DataRow(SocketPlatform.Darwin)]
    public void For_WithoutFastOpen_SetsNothing(SocketPlatform platform)
    {
        Assert.IsEmpty(FastOpenSocketOption.For(new TcpSocketOptions(), platform));
    }

    [TestMethod]
    public void ConnectsThroughConnectx_WithFastOpenOnDarwin_IsTrue()
    {
        Assert.IsTrue(FastOpenSocketOption.ConnectsThroughConnectx(new TcpSocketOptions(FastOpen: true), SocketPlatform.Darwin));
    }

    [TestMethod]
    [DataRow(SocketPlatform.Windows)]
    [DataRow(SocketPlatform.Linux)]
    [DataRow(SocketPlatform.FreeBsd)]
    [DataRow(SocketPlatform.Other)]
    public void ConnectsThroughConnectx_WithFastOpenElsewhere_IsFalse(SocketPlatform platform)
    {
        Assert.IsFalse(FastOpenSocketOption.ConnectsThroughConnectx(new TcpSocketOptions(FastOpen: true), platform));
    }

    [TestMethod]
    public void ConnectsThroughConnectx_WithoutFastOpenOnDarwin_IsFalse()
    {
        Assert.IsFalse(FastOpenSocketOption.ConnectsThroughConnectx(new TcpSocketOptions(), SocketPlatform.Darwin));
    }

    [TestMethod]
    public void SocketProtocol_ByDefault_IsTcp()
    {
        Assert.AreEqual(ProtocolType.Tcp, new TcpSocketOptions().SocketProtocol);
    }

    [TestMethod]
    public void SocketProtocol_WithMultipathTcp_IsIpProtoMptcp()
    {
        Assert.AreEqual((ProtocolType)262, new TcpSocketOptions(MultipathTcp: true).SocketProtocol);
    }

    [TestMethod]
    public void FailureToOpenSocket_WithoutMultipathTcp_IsNone()
    {
        Assert.IsNull(new TcpDialer().FailureToOpenSocket(AddressFamily.InterNetwork));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal()
    {
        // Windows has no Multipath TCP: curl 8.21.0 fails to open the socket there too (BL-647 Notes).
        var refusal = new TcpDialer(new TcpSocketOptions(MultipathTcp: true)).FailureToOpenSocket(AddressFamily.InterNetwork);

        Assert.AreEqual(SocketError.ProtocolNotSupported, refusal?.SocketErrorCode);
    }

    [TestMethod]
    public void ApplySocketOptions_WithFastOpen_LeavesTheSocketUsable()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer(new TcpSocketOptions(NoDelay: false, FastOpen: true)).ApplySocketOptions(socket);

        Assert.IsFalse(socket.NoDelay);
        Assert.AreNotEqual(0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void ApplySocketOptions_WithFastOpenOnLinux_SetsTcpFastOpenConnect()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer(new TcpSocketOptions(FastOpen: true)).ApplySocketOptions(socket);

        var value = new byte[4];
        socket.GetRawSocketOption(6, 30, value);
        Assert.AreEqual(1, BitConverter.ToInt32(value));
    }
}
