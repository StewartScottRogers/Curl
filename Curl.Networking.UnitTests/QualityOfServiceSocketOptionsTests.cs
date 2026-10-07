using System.Net.Sockets;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the socket options <c>--ip-tos</c> and <c>--vlan-priority</c> ask for on each operating system, as
/// libcurl 8.21.0's <c>cf-socket.c</c> sets them where the system defines <c>IP_TOS</c>,
/// <c>IPV6_TCLASS</c> and <c>SO_PRIORITY</c> (BL-646 Notes).
/// </summary>
[TestClass]
public sealed class QualityOfServiceSocketOptionsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(SocketPlatform.Windows)]
    [DataRow(SocketPlatform.Linux)]
    [DataRow(SocketPlatform.Darwin)]
    [DataRow(SocketPlatform.FreeBsd)]
    [DataRow(SocketPlatform.Other)]
    public void For_WithBothZero_SetsNothing(SocketPlatform platform)
    {
        Diagnostics.Arrange("platform, family, type of service, VLAN priority", $"{platform}, InterNetwork, 0, 0");

        var options = QualityOfServiceSocketOptions.For(new TcpSocketOptions(), AddressFamily.InterNetwork, platform);

        Diagnostics.Act("options", string.Join(", ", options));
        Diagnostics.Assert("option count", 0, options.Count);

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
        Diagnostics.Arrange("platform, family, type of service", $"{platform}, {family}, 0x20");

        IReadOnlyList<RawSocketOption> options = QualityOfServiceSocketOptions.For(new TcpSocketOptions(TypeOfService: 0x20), family, platform);

        Diagnostics.Act("options", string.Join(", ", options));
        Diagnostics.Assert("options", new RawSocketOption(level, name, 0x20), string.Join(", ", options));

        CollectionAssert.AreEqual(new[] { new RawSocketOption(level, name, 0x20) }, options.ToArray());
    }

    [TestMethod]
    [DataRow(SocketPlatform.Other, AddressFamily.InterNetwork)]
    [DataRow(SocketPlatform.Other, AddressFamily.InterNetworkV6)]
    [DataRow(SocketPlatform.Linux, AddressFamily.Unix)]
    public void For_WithTypeOfServiceWhereNoOptionExists_SetsNothing(SocketPlatform platform, AddressFamily family)
    {
        Diagnostics.Arrange("platform, family, type of service", $"{platform}, {family}, 0x20");

        var options = QualityOfServiceSocketOptions.For(new TcpSocketOptions(TypeOfService: 0x20), family, platform);

        Diagnostics.Act("options", string.Join(", ", options));
        Diagnostics.Assert("option count", 0, options.Count);

        Assert.IsEmpty(QualityOfServiceSocketOptions.For(new TcpSocketOptions(TypeOfService: 0x20), family, platform));
    }

    [TestMethod]
    public void For_WithVlanPriorityOnLinux_SetsSoPriority()
    {
        Diagnostics.Arrange("platform, family, VLAN priority", "Linux, InterNetwork, 3");

        IReadOnlyList<RawSocketOption> options = QualityOfServiceSocketOptions.For(new TcpSocketOptions(VlanPriority: 3), AddressFamily.InterNetwork, SocketPlatform.Linux);

        Diagnostics.Act("options", string.Join(", ", options));
        Diagnostics.Assert("options", new RawSocketOption(1, 12, 3), string.Join(", ", options));

        CollectionAssert.AreEqual(new[] { new RawSocketOption(1, 12, 3) }, options.ToArray());
    }

    [TestMethod]
    [DataRow(SocketPlatform.Windows)]
    [DataRow(SocketPlatform.Darwin)]
    [DataRow(SocketPlatform.FreeBsd)]
    [DataRow(SocketPlatform.Other)]
    public void For_WithVlanPriorityWhereTheSystemHasNoSoPriority_SetsNothing(SocketPlatform platform)
    {
        Diagnostics.Arrange("platform, family, VLAN priority", $"{platform}, InterNetwork, 3");

        var options = QualityOfServiceSocketOptions.For(new TcpSocketOptions(VlanPriority: 3), AddressFamily.InterNetwork, platform);

        Diagnostics.Act("options", string.Join(", ", options));
        Diagnostics.Assert("option count", 0, options.Count);

        Assert.IsEmpty(QualityOfServiceSocketOptions.For(new TcpSocketOptions(VlanPriority: 3), AddressFamily.InterNetwork, platform));
    }

    [TestMethod]
    public void For_WithBothOnLinux_SetsTosThenPriority()
    {
        Diagnostics.Arrange("platform, family, type of service, VLAN priority", "Linux, InterNetworkV6, 0xb8, 5");

        IReadOnlyList<RawSocketOption> options = QualityOfServiceSocketOptions.For(
            new TcpSocketOptions(TypeOfService: 0xb8, VlanPriority: 5), AddressFamily.InterNetworkV6, SocketPlatform.Linux);

        Diagnostics.Act("options", string.Join(", ", options));
        Diagnostics.Assert("options", $"{new RawSocketOption(41, 67, 0xb8)}, {new RawSocketOption(1, 12, 5)}", string.Join(", ", options));

        CollectionAssert.AreEqual(new[] { new RawSocketOption(41, 67, 0xb8), new RawSocketOption(1, 12, 5) }, options.ToArray());
    }

    [TestMethod]
    public void TrySet_AnOptionTheSystemRefuses_LeavesTheSocketUsable()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        Diagnostics.Arrange("refused option", new RawSocketOption(0, 9999, 1));

        QualityOfServiceSocketOptions.TrySet(socket, new RawSocketOption(0, 9999, 1));
        socket.NoDelay = true;

        Diagnostics.Act("NoDelay after TrySet", socket.NoDelay);
        Diagnostics.Assert("NoDelay", true, socket.NoDelay);

        Assert.IsTrue(socket.NoDelay);
    }

    [TestMethod]
    public void ApplySocketOptions_WithTypeOfServiceAndPriority_SetsTheRestAsWithout()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        Diagnostics.Arrange("NoDelay, type of service, VLAN priority", "false, 0x20, 3");

        new TcpDialer(new TcpSocketOptions(NoDelay: false, TypeOfService: 0x20, VlanPriority: 3)).ApplySocketOptions(socket);

        var keepAliveSet = (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)! != 0;
        Diagnostics.Act("NoDelay, keep-alive set", $"{socket.NoDelay}, {keepAliveSet}");
        Diagnostics.Assert("NoDelay, keep-alive set", "False, True", $"{socket.NoDelay}, {keepAliveSet}");

        Assert.IsFalse(socket.NoDelay);
        Assert.AreNotEqual(0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void ApplySocketOptions_OnLinux_SetsIpTosAndSoPriority()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        Diagnostics.Arrange("type of service, VLAN priority", "0x20, 3");

        new TcpDialer(new TcpSocketOptions(TypeOfService: 0x20, VlanPriority: 3)).ApplySocketOptions(socket);

        var typeOfService = ReadInt(socket, 0, 1);
        var priority = ReadInt(socket, 1, 12);
        Diagnostics.Act("IP_TOS, SO_PRIORITY", $"{typeOfService}, {priority}");
        Diagnostics.Assert("IP_TOS, SO_PRIORITY", "32, 3", $"{typeOfService}, {priority}");

        Assert.AreEqual(0x20, ReadInt(socket, 0, 1));
        Assert.AreEqual(3, ReadInt(socket, 1, 12));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void ApplySocketOptions_OnAnIpV6Socket_SetsTheTrafficClass()
    {
        using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

        Diagnostics.Arrange("family, type of service", "InterNetworkV6, 0x20");

        new TcpDialer(new TcpSocketOptions(TypeOfService: 0x20)).ApplySocketOptions(socket);

        var trafficClass = ReadInt(socket, 41, OperatingSystem.IsLinux() ? 67 : 36);
        Diagnostics.Act("IPV6_TCLASS", trafficClass);
        Diagnostics.Assert("IPV6_TCLASS", 0x20, trafficClass);

        Assert.AreEqual(0x20, ReadInt(socket, 41, OperatingSystem.IsLinux() ? 67 : 36));
    }

    private static int ReadInt(Socket socket, int level, int name)
    {
        var value = new byte[4];
        socket.GetRawSocketOption(level, name, value);
        return BitConverter.ToInt32(value);
    }
}
