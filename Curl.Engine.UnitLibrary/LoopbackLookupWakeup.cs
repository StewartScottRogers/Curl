using System.Net;
using System.Net.Sockets;

namespace Curl.Engine;

/// <summary>
/// The socket a socket-action transfer's look-up wakes the application on, the engine's stand-in for curl 8.22.0's
/// threaded resolver socket pair: a TCP socket listening on the IPv4 loopback address, which <see cref="Signal" />
/// makes readable by connecting to it, so an application's poll on <see cref="Handle" /> returns once the look-up
/// ends without a byte being sent (BL-4036, ADR-0984).
/// </summary>
internal sealed class LoopbackLookupWakeup : IDisposable
{
    private readonly Socket listener;
    private readonly object gate = new();
    private Socket? caller;
    private bool disposed;

    private LoopbackLookupWakeup(Socket listener) => this.listener = listener;

    /// <summary>Gets the operating system handle of the listening socket the application polls.</summary>
    internal nint Handle => listener.Handle;

    /// <summary>Opens a wakeup listening on an ephemeral loopback port.</summary>
    /// <returns>The wakeup, not yet signalled.</returns>
    internal static LoopbackLookupWakeup Open()
    {
        Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        return new LoopbackLookupWakeup(listener);
    }

    /// <summary>
    /// Makes <see cref="Handle" /> readable by connecting to it, once; a later call, or a call after <see cref="Dispose" />,
    /// does nothing.
    /// </summary>
    internal void Signal()
    {
        lock (gate)
        {
            if (disposed || caller is not null)
            {
                return;
            }

            caller = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            caller.Connect(listener.LocalEndPoint!);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            caller?.Dispose();
            listener.Dispose();
        }
    }
}
