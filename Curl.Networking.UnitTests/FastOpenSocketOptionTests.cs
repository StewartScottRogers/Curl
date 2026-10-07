using System.Net;
using System.Net.Sockets;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins how <c>--tcp-fastopen</c> and <c>--mptcp</c> reach the socket on each operating system (BL-647,
/// ADR-0317): the Fast Open option in each system's numbers, and the protocol the socket is opened with.
/// </summary>
[TestClass]
public sealed class FastOpenSocketOptionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(SocketPlatform.Windows, 15)]
    [DataRow(SocketPlatform.Linux, 30)]
    [DataRow(SocketPlatform.Darwin, 0x105)]
    public void For_APlatformWithFastOpen_SetsItsOptionAtTheTcpLevel(SocketPlatform platform, int name)
    {
        Diagnostics.Arrange("platform", platform);
        Diagnostics.Arrange("--tcp-fastopen", true);

        var options = FastOpenSocketOption.For(new TcpSocketOptions(FastOpen: true), platform).ToArray();

        Diagnostics.Act("raw options", string.Join(", ", options));
        Diagnostics.Assert("raw options", new RawSocketOption(6, name, 1).ToString(), string.Join(", ", options));
        CollectionAssert.AreEqual(new[] { new RawSocketOption(6, name, 1) }, options);
    }

    [TestMethod]
    [DataRow(SocketPlatform.FreeBsd)]
    [DataRow(SocketPlatform.Other)]
    public void For_APlatformWithoutAKnownOption_SetsNothing(SocketPlatform platform)
    {
        Diagnostics.Arrange("platform", platform);
        Diagnostics.Arrange("--tcp-fastopen", true);

        var options = FastOpenSocketOption.For(new TcpSocketOptions(FastOpen: true), platform).ToArray();

        Diagnostics.Act("raw options", options.Length);
        Diagnostics.Assert("raw options", 0, options.Length);
        Assert.IsEmpty(options);
    }

    [TestMethod]
    [DataRow(SocketPlatform.Windows)]
    [DataRow(SocketPlatform.Linux)]
    [DataRow(SocketPlatform.Darwin)]
    public void For_WithoutFastOpen_SetsNothing(SocketPlatform platform)
    {
        Diagnostics.Arrange("platform", platform);
        Diagnostics.Arrange("--tcp-fastopen", false);

        var options = FastOpenSocketOption.For(new TcpSocketOptions(), platform).ToArray();

        Diagnostics.Act("raw options", options.Length);
        Diagnostics.Assert("raw options", 0, options.Length);
        Assert.IsEmpty(options);
    }

    [TestMethod]
    public void ConnectsThroughConnectx_WithFastOpenOnDarwin_IsTrue()
    {
        Diagnostics.Arrange("platform", SocketPlatform.Darwin);
        Diagnostics.Arrange("--tcp-fastopen", true);

        var throughConnectx = FastOpenSocketOption.ConnectsThroughConnectx(new TcpSocketOptions(FastOpen: true), SocketPlatform.Darwin);

        Diagnostics.Act("connects through connectx", throughConnectx);
        Diagnostics.Assert("connects through connectx", true, throughConnectx);
        Assert.IsTrue(throughConnectx);
    }

    [TestMethod]
    [DataRow(SocketPlatform.Windows)]
    [DataRow(SocketPlatform.Linux)]
    [DataRow(SocketPlatform.FreeBsd)]
    [DataRow(SocketPlatform.Other)]
    public void ConnectsThroughConnectx_WithFastOpenElsewhere_IsFalse(SocketPlatform platform)
    {
        Diagnostics.Arrange("platform", platform);
        Diagnostics.Arrange("--tcp-fastopen", true);

        var throughConnectx = FastOpenSocketOption.ConnectsThroughConnectx(new TcpSocketOptions(FastOpen: true), platform);

        Diagnostics.Act("connects through connectx", throughConnectx);
        Diagnostics.Assert("connects through connectx", false, throughConnectx);
        Assert.IsFalse(throughConnectx);
    }

    [TestMethod]
    public void ConnectsThroughConnectx_WithoutFastOpenOnDarwin_IsFalse()
    {
        Diagnostics.Arrange("platform", SocketPlatform.Darwin);
        Diagnostics.Arrange("--tcp-fastopen", false);

        var throughConnectx = FastOpenSocketOption.ConnectsThroughConnectx(new TcpSocketOptions(), SocketPlatform.Darwin);

        Diagnostics.Act("connects through connectx", throughConnectx);
        Diagnostics.Assert("connects through connectx", false, throughConnectx);
        Assert.IsFalse(throughConnectx);
    }

    [TestMethod]
    public void SocketProtocol_ByDefault_IsTcp()
    {
        Diagnostics.Arrange("--mptcp", false);

        var protocol = new TcpSocketOptions().SocketProtocol;

        Diagnostics.Act("socket protocol", (int)protocol);
        Diagnostics.Assert("socket protocol", (int)ProtocolType.Tcp, (int)protocol);
        Assert.AreEqual(ProtocolType.Tcp, protocol);
    }

    [TestMethod]
    public void SocketProtocol_WithMultipathTcp_IsIpProtoMptcp()
    {
        Diagnostics.Arrange("--mptcp", true);

        var protocol = new TcpSocketOptions(MultipathTcp: true).SocketProtocol;

        Diagnostics.Act("socket protocol", (int)protocol);
        Diagnostics.Assert("socket protocol", 262, (int)protocol);
        Assert.AreEqual((ProtocolType)262, protocol);
    }

    [TestMethod]
    public void FailureToOpenSocket_WithoutMultipathTcp_IsNone()
    {
        Diagnostics.Arrange("--mptcp", false);
        Diagnostics.Arrange("address family", AddressFamily.InterNetwork);

        var failure = new TcpDialer().FailureToOpenSocket(AddressFamily.InterNetwork);

        Diagnostics.Act("failure", failure?.SocketErrorCode.ToString() ?? "(none)");
        Diagnostics.Assert("failure", "(none)", failure?.SocketErrorCode.ToString() ?? "(none)");
        Assert.IsNull(failure);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal()
    {
        // Windows has no Multipath TCP: curl 8.21.0 fails to open the socket there too (BL-647 Notes).
        Diagnostics.Arrange("--mptcp", true);
        Diagnostics.Arrange("address family", AddressFamily.InterNetwork);

        var refusal = new TcpDialer(new TcpSocketOptions(MultipathTcp: true)).FailureToOpenSocket(AddressFamily.InterNetwork);

        Diagnostics.Act("refusal", refusal?.SocketErrorCode.ToString() ?? "(none)");
        Diagnostics.Assert("refusal", SocketError.ProtocolNotSupported, refusal?.SocketErrorCode);
        Assert.AreEqual(SocketError.ProtocolNotSupported, refusal?.SocketErrorCode);
    }

    [TestMethod]
    public void ApplySocketOptions_WithFastOpen_LeavesTheSocketUsable()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("options", "NoDelay false, FastOpen true");

        new TcpDialer(new TcpSocketOptions(NoDelay: false, FastOpen: true)).ApplySocketOptions(socket);

        var keepAlive = (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!;
        Diagnostics.Act("no delay", socket.NoDelay);
        Diagnostics.Act("keep-alive on", keepAlive != 0);
        Diagnostics.Assert("no delay", false, socket.NoDelay);
        Diagnostics.Assert("keep-alive on", true, keepAlive != 0);
        Assert.IsFalse(socket.NoDelay);
        Assert.AreNotEqual(0, keepAlive);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void ApplySocketOptions_WithFastOpenOnLinux_SetsTcpFastOpenConnect()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Diagnostics.Arrange("options", "FastOpen true");

        new TcpDialer(new TcpSocketOptions(FastOpen: true)).ApplySocketOptions(socket);

        var value = new byte[4];
        socket.GetRawSocketOption(6, 30, value);
        Diagnostics.Act("TCP_FASTOPEN_CONNECT", BitConverter.ToInt32(value));
        Diagnostics.Assert("TCP_FASTOPEN_CONNECT", 1, BitConverter.ToInt32(value));
        Assert.AreEqual(1, BitConverter.ToInt32(value));
    }
}
