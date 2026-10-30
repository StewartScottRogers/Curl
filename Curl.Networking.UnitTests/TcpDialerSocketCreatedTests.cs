using System.Net.Sockets;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="ITcpDialer.PublishingSocketsTo" />: <see cref="TcpDialer" /> hands each socket's handle to the hook
/// once its options are set and before it connects, so a socket-action transfer can tell the application its socket
/// while the connect is in progress (BL-3890). The sockets are opened and closed without connecting or sending a
/// byte, so these are unit tests (ADR-0421).
/// </summary>
[TestClass]
public sealed class TcpDialerSocketCreatedTests
{
    [TestMethod]
    public void PrepareToConnect_DialerPublishingSockets_HandsTheSocketsHandleBeforeItConnects()
    {
        List<nint> published = [];
        TcpSocketOptions options = new() { NoDelay = false };
        var dialer = (TcpDialer)new TcpDialer(options).PublishingSocketsTo(published.Add);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        dialer.PrepareToConnect(socket);

        CollectionAssert.AreEqual(new[] { socket.Handle }, published);
        Assert.IsFalse(socket.Connected);
        Assert.IsFalse(socket.NoDelay);
        Assert.AreSame(options, dialer.SocketOptions);
    }

    [TestMethod]
    public void PrepareToConnect_NoHook_PublishesNothingAndSetsTheOptions()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        new TcpDialer().PrepareToConnect(socket);

        Assert.IsTrue(socket.NoDelay);
    }

    [TestMethod]
    public void PublishingSocketsTo_GivesANewDialerAndLeavesThisOneSilent()
    {
        List<nint> published = [];
        TcpDialer dialer = new();

        ITcpDialer publishing = dialer.PublishingSocketsTo(published.Add);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        dialer.PublishCreated(socket);

        Assert.AreNotSame(dialer, publishing);
        Assert.IsEmpty(published);
    }

    [TestMethod]
    public void PublishingSocketsTo_NullHook_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TcpDialer().PublishingSocketsTo(null!));
    }

    [TestMethod]
    public void PublishingSocketsTo_DialerCreatingNoSocket_GivesItselfByDefault()
    {
        ITcpDialer dialer = new FakeTcpDialer();

        Assert.AreSame(dialer, dialer.PublishingSocketsTo(_ => { }));
        Assert.AreSame(dialer, dialer.HoldingFailedSocketsUntil(_ => ValueTask.CompletedTask));
    }

    [TestMethod]
    public async Task CloseFailedSocketAsync_DialerHoldingFailedSockets_ClosesOnlyOnceTheHoldEnds()
    {
        TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<nint> published = [];
        var dialer = (TcpDialer)new TcpDialer().HoldingFailedSocketsUntil(_ => new ValueTask(released.Task)).PublishingSocketsTo(published.Add);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        dialer.PrepareToConnect(socket);

        ValueTask closing = dialer.CloseFailedSocketAsync(socket, CancellationToken.None);
        bool openWhileHeld = socket.SafeHandle.IsClosed is false;
        released.SetResult();
        await closing;

        Assert.IsTrue(openWhileHeld);
        Assert.IsTrue(socket.SafeHandle.IsClosed);
        Assert.HasCount(1, published);
    }

    [TestMethod]
    public async Task CloseFailedSocketAsync_NoHold_ClosesAtOnce()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        await new TcpDialer().CloseFailedSocketAsync(socket, CancellationToken.None);

        Assert.IsTrue(socket.SafeHandle.IsClosed);
    }

    [TestMethod]
    public void HoldingFailedSocketsUntil_NullHook_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TcpDialer().HoldingFailedSocketsUntil(null!));
    }
}
