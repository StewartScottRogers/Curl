using System.Net;
using System.Net.Sockets;
using Curl.Api.Callbacks;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

[TestClass]
public sealed class SocketWaitPollerTests
{
    [TestMethod]
    public async Task Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly()
    {
        using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(8);
        using Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(listener.LocalEndPoint!);
        using Socket accepted = await AcceptThePeerOfAsync(listener, client);
        CurlSocket socket = new(client.Handle);

        CurlWaitEvents all = SocketWaitPoller.Instance.Poll(socket, CurlWaitEvents.PollIn | CurlWaitEvents.PollOut | CurlWaitEvents.PollPriority);
        CurlWaitEvents readOnly = SocketWaitPoller.Instance.Poll(socket, CurlWaitEvents.PollIn);
        CurlWaitEvents none = SocketWaitPoller.Instance.Poll(socket, CurlWaitEvents.None);

        string state = DescribeSocketState(client, accepted);
        Assert.AreEqual(CurlWaitEvents.PollOut, all, state);
        Assert.AreEqual(CurlWaitEvents.None, readOnly, state);
        Assert.AreEqual(CurlWaitEvents.None, none, state);
        Assert.IsTrue(client.Connected);
    }

    /// <summary>
    /// Accepts connections until the one whose remote end is <paramref name="client" />, closing any other: another
    /// test process probing a loopback port can land a connection in this listener's queue first (BL-4504).
    /// </summary>
    private static async Task<Socket> AcceptThePeerOfAsync(Socket listener, Socket client)
    {
        while (true)
        {
            Socket accepted = await listener.AcceptAsync();
            if (Equals(accepted.RemoteEndPoint, client.LocalEndPoint))
            {
                return accepted;
            }

            accepted.Dispose();
        }
    }

    private static string DescribeSocketState(Socket client, Socket accepted) =>
        $"client {client.LocalEndPoint} -> {client.RemoteEndPoint}, pending error {client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error)}, "
        + $"available {client.Available}; accepted {accepted.LocalEndPoint} <- {accepted.RemoteEndPoint}";
}
