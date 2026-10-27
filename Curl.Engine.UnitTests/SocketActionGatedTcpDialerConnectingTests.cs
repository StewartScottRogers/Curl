using System.Net;
using System.Net.Sockets;

using Curl.Api.EngineContracts.Multi;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins the socket a socket-action transfer waits on while it connects: the inner dialer publishes the socket it
/// creates before connecting it, so <see cref="TransferSocketWait.Socket" /> holds its handle and
/// <see cref="TransferSocketWait.Waiting" /> is <see cref="CurlPoll.Out" /> during the connect, and a refused connect
/// leaves the handle known, as curl 8.22.0 tells the application <c>S=2</c> on its socket (BL-3890, CGR-034 C_65).
/// </summary>
[TestClass]
public sealed class SocketActionGatedTcpDialerConnectingTests
{
    private const nint PublishedSocket = 1234;
    private static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(30);
    private static readonly IPEndPoint Origin = new(IPAddress.Loopback, 9000);

    [TestMethod]
    public async Task DialAsync_InnerDialerPublishesItsSocket_WaitsOutOnThatSocketWhileTheConnectIsHeld()
    {
        PublishingHeldTcpDialer inner = new();
        TransferSocketWait wait = new(TimeProvider.System);
        SocketActionGatedTcpDialer dialer = new(inner, wait);

        Task<DialedTcpConnection> dial = dialer.DialAsync(Origin, CancellationToken.None).AsTask();
        await inner.Connecting.Task.WaitAsync(LongestWait);
        nint? socketDuringConnect = wait.Socket;
        CurlPoll waitingDuringConnect = wait.Waiting;
        inner.Refuse.SetResult();
        await Assert.ThrowsExactlyAsync<SocketException>(() => dial);

        Assert.AreEqual(PublishedSocket, socketDuringConnect);
        Assert.AreEqual(CurlPoll.Out, waitingDuringConnect);
        Assert.AreEqual(PublishedSocket, wait.Socket);
        Assert.AreEqual(CurlPoll.Out, wait.Waiting);
    }

    [TestMethod]
    public void Socket_BeforeAnySocketIsPublished_IsNull()
    {
        TransferSocketWait wait = new(TimeProvider.System);

        Assert.IsNull(wait.Socket);
    }

    /// <summary>
    /// A dialer whose publishing view hands the hook <see cref="PublishedSocket" /> as each dial starts and then
    /// holds the connect until the test refuses it.
    /// </summary>
    private sealed class PublishingHeldTcpDialer : ITcpDialer
    {
        private Action<nint> socketCreated = _ => throw new InvalidOperationException("The gated dialer dialed without the hook.");

        public TaskCompletionSource Connecting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Refuse { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ITcpDialer PublishingSocketsTo(Action<nint> socketCreated)
        {
            this.socketCreated = socketCreated;
            return this;
        }

        public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            socketCreated(PublishedSocket);
            Connecting.TrySetResult();
            await Refuse.Task.WaitAsync(cancellationToken);
            throw new SocketException((int)SocketError.ConnectionRefused);
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
