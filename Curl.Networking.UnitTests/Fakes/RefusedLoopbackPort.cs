using System.Net;
using System.Net.Sockets;

namespace Curl.Networking.Fakes;

/// <summary>
/// A loopback port every platform refuses a connect to, held for the whole test so a test running beside it cannot
/// be handed the port and accept the connect (BL-4368). The port is the local end of a loopback connection this
/// holds open: nothing listens on it, so Windows, Linux and macOS all answer a new connect's SYN with a reset. A
/// socket that is only bound is not enough: macOS drops a SYN to a bound, never-listening socket without a reset,
/// so the connect times out instead of being refused (BL-4370). No byte is sent on the held connection.
/// </summary>
internal sealed class RefusedLoopbackPort : IDisposable
{
    private readonly Socket listener;
    private readonly Socket holder;
    private readonly Socket accepted;

    private RefusedLoopbackPort(Socket listener, Socket holder, Socket accepted)
    {
        this.listener = listener;
        this.holder = holder;
        this.accepted = accepted;
    }

    /// <summary>The refused loopback end point: 127.0.0.1 and the held port.</summary>
    public IPEndPoint EndPoint => (IPEndPoint)this.holder.LocalEndPoint!;

    /// <summary>The held port.</summary>
    public int Port => this.EndPoint.Port;

    /// <summary>Opens a loopback connection and holds its client end's port.</summary>
    public static async Task<RefusedLoopbackPort> OpenAsync()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var holder = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await holder.ConnectAsync(listener.LocalEndPoint!);
        var accepted = await listener.AcceptAsync();
        return new RefusedLoopbackPort(listener, holder, accepted);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.accepted.Dispose();
        this.holder.Dispose();
        this.listener.Dispose();
    }
}
