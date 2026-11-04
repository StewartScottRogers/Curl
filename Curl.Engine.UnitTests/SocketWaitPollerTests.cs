using System.Net;
using System.Net.Sockets;
using Curl.Api.Callbacks;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

[TestClass]
public sealed class SocketWaitPollerTests
{
    /// <summary>
    /// Polls a UDP socket connected to a bound loopback peer that sends nothing (BL-4852): a TCP connection here was
    /// hung up or reset by something outside the test while it ran in parallel with others, so the socket polled
    /// readable on Linux and macOS (BL-4504, BL-4852); a connected UDP socket has no connection to lose and takes
    /// datagrams only from its peer, so nothing but the poller decides what it reports.
    /// </summary>
    [TestMethod]
    public void Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly()
    {
        using Socket peer = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        peer.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        using Socket client = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        client.Connect(peer.LocalEndPoint!);
        CurlSocket socket = new(client.Handle);

        CurlWaitEvents all = SocketWaitPoller.Instance.Poll(socket, CurlWaitEvents.PollIn | CurlWaitEvents.PollOut | CurlWaitEvents.PollPriority);
        CurlWaitEvents readOnly = SocketWaitPoller.Instance.Poll(socket, CurlWaitEvents.PollIn);
        CurlWaitEvents none = SocketWaitPoller.Instance.Poll(socket, CurlWaitEvents.None);

        string state = DescribeSocketState(client);
        Assert.AreEqual(CurlWaitEvents.PollOut, all, state);
        Assert.AreEqual(CurlWaitEvents.None, readOnly, state);
        Assert.AreEqual(CurlWaitEvents.None, none, state);
        Assert.IsTrue(client.Connected);
    }

    private static string DescribeSocketState(Socket client) =>
        $"client {client.LocalEndPoint} -> {client.RemoteEndPoint}, pending error {client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error)}, "
        + $"available {client.Available}";
}
