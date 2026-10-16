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
        listener.Listen(1);
        using Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(listener.LocalEndPoint!);
        using Socket accepted = await listener.AcceptAsync();
        CurlSocket socket = new(client.Handle);

        CurlWaitEvents all = SocketWaitPoller.Instance.Poll(socket, CurlWaitEvents.PollIn | CurlWaitEvents.PollOut | CurlWaitEvents.PollPriority);
        CurlWaitEvents readOnly = SocketWaitPoller.Instance.Poll(socket, CurlWaitEvents.PollIn);
        CurlWaitEvents none = SocketWaitPoller.Instance.Poll(socket, CurlWaitEvents.None);

        Assert.AreEqual(CurlWaitEvents.PollOut, all);
        Assert.AreEqual(CurlWaitEvents.None, readOnly);
        Assert.AreEqual(CurlWaitEvents.None, none);
        Assert.IsTrue(client.Connected);
    }
}
