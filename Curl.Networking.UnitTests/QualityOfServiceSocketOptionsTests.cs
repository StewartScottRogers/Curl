using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins the socket options <c>--ip-tos</c> and <c>--vlan-priority</c> ask for on each operating system, as
/// libcurl 8.21.0's <c>cf-socket.c</c> sets them where the system defines <c>IP_TOS</c>,
/// <c>IPV6_TCLASS</c> and <c>SO_PRIORITY</c> (BL-646 Notes).
/// </summary>
[TestClass]
public sealed class QualityOfServiceSocketOptionsTests
{
    [TestMethod]
    [DataRow(SocketPlatform.Windows)]
    [DataRow(SocketPlatform.Linux)]
    [DataRow(SocketPlatform.Darwin)]
    [DataRow(SocketPlatform.FreeBsd)]
    [DataRow(SocketPlatform.Other)]
    public void For_WithBothZero_SetsNothing(SocketPlatform platform)
    {
        Assert.IsEmpty(QualityOfServiceSocketOptions.For(new TcpSocketOptions(), AddressFamily.InterNetwork, platform));
    }

    [TestMethod]
    [DataRow(SocketPlatform.Windows, AddressFamily.InterNetwork, 0, 3)]
    [DataRow(SocketPlatform.Linux, AddressFamily.InterNetwork, 0, 1)]
    [DataRow(SocketPlatform.Darwin, AddressFamily.InterNetwork, 0, 3)]
    [DataRow(SocketPlatform.FreeBsd, AddressFamily.InterNetwork, 0, 3)]
    [DataRow(SocketPlatform.Windows, AddressFamily.InterNetworkV6, 41, 39)]
    [DataRow(SocketPlatform.Linux, AddressFamily.InterNetworkV6, 41, 67)]
    [DataRow(SocketPlatform.Darwin, AddressFamily.InterNetworkV6, 41, 36)]
    [DataRow(SocketPlatform.FreeBsd, AddressFamily.InterNetworkV6, 41, 61)]
    public void For_WithTypeOfService_SetsThePlatformsTosOrTrafficClass(SocketPlatform platform, AddressFamily family, int level, int name)
    {
        IReadOnlyList<RawSocketOption> options = QualityOfServiceSocketOptions.For(new TcpSocketOptions(TypeOfService: 0x20), family, platform);

        CollectionAssert.AreEqual(new[] { new RawSocketOption(level, name, 0x20) }, options.ToArray());
    }

    [TestMethod]
    [DataRow(SocketPlatform.Other, AddressFamily.InterNetwork)]
    [DataRow(SocketPlatform.Other, AddressFamily.InterNetworkV6)]
    [DataRow(SocketPlatform.Linux, AddressFamily.Unix)]
    public void For_WithTypeOfServiceWhereNoOptionExists_SetsNothing(SocketPlatform platform, AddressFamily family)
    {
        Assert.IsEmpty(QualityOfServiceSocketOptions.For(new TcpSocketOptions(TypeOfService: 0x20), family, platform));
    }

    [TestMethod]
    public void For_WithVlanPriorityOnLinux_SetsSoPriority()
    {
        IReadOnlyList<RawSocketOption> options = QualityOfServiceSocketOptions.For(new TcpSocketOptions(VlanPriority: 3), AddressFamily.InterNetwork, SocketPlatform.Linux);

        CollectionAssert.AreEqual(new[] { new RawSocketOption(1, 12, 3) }, options.ToArray());
    }

    [TestMethod]
    [DataRow(SocketPlatform.Windows)]
    [DataRow(SocketPlatform.Darwin)]
    [DataRow(SocketPlatform.FreeBsd)]
    [DataRow(SocketPlatform.Other)]
    public void For_WithVlanPriorityWhereTheSystemHasNoSoPriority_SetsNothing(SocketPlatform platform)
    {
        Assert.IsEmpty(QualityOfServiceSocketOptions.For(new TcpSocketOptions(VlanPriority: 3), AddressFamily.InterNetwork, platform));
    }

    [TestMethod]
    public void For_WithBothOnLinux_SetsTosThenPriority()
    {
        IReadOnlyList<RawSocketOption> options = QualityOfServiceSocketOptions.For(
            new TcpSocketOptions(TypeOfService: 0xb8, VlanPriority: 5), AddressFamily.InterNetworkV6, SocketPlatform.Linux);

        CollectionAssert.AreEqual(new[] { new RawSocketOption(41, 67, 0xb8), new RawSocketOption(1, 12, 5) }, options.ToArray());
    }

    [TestMethod]
    public void TrySet_AnOptionTheSystemRefuses_LeavesTheSocketUsable()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        QualityOfServiceSocketOptions.TrySet(socket, new RawSocketOption(0, 9999, 1));
        socket.NoDelay = true;

        Assert.IsTrue(socket.NoDelay);
    }

    [TestMethod]
    public void ApplySocketOptions_WithTypeOfServiceAndPriority_SetsTheRestAsWithout()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer(new TcpSocketOptions(NoDelay: false, TypeOfService: 0x20, VlanPriority: 3)).ApplySocketOptions(socket);

        Assert.IsFalse(socket.NoDelay);
        Assert.AreNotEqual(0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void ApplySocketOptions_OnLinux_SetsIpTosAndSoPriority()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer(new TcpSocketOptions(TypeOfService: 0x20, VlanPriority: 3)).ApplySocketOptions(socket);

        Assert.AreEqual(0x20, ReadInt(socket, 0, 1));
        Assert.AreEqual(3, ReadInt(socket, 1, 12));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void ApplySocketOptions_OnAnIpV6Socket_SetsTheTrafficClass()
    {
        using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer(new TcpSocketOptions(TypeOfService: 0x20)).ApplySocketOptions(socket);

        Assert.AreEqual(0x20, ReadInt(socket, 41, OperatingSystem.IsLinux() ? 67 : 36));
    }

    private static int ReadInt(Socket socket, int level, int name)
    {
        var value = new byte[4];
        socket.GetRawSocketOption(level, name, value);
        return BitConverter.ToInt32(value);
    }
}
