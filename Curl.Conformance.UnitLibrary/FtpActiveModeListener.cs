using System.Collections.Concurrent;
using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// The <see cref="IConnectionListener"/> curl's active-mode FTP (<c>-P</c>) listens on in the case
/// runner: an in-memory port, no socket. Upstream's <c>tests/ftpserver.pl</c> (at
/// <c>curl-8_21_0</c>) answers <c>PORT</c> and <c>EPRT</c> by connecting to the port the client
/// names; <see cref="Connect"/> stands for that connect, handing a new
/// <see cref="FtpDataConnection"/> to the curl listening on that port, whose accept then returns it.
/// A listen on port 0, curl's default, takes <see cref="FirstPort"/>.
/// </summary>
internal sealed class FtpActiveModeListener : IConnectionListener
{
    /// <summary>The port a listen on port 0 takes: a fixed stand-in for the free port the system picks, which no other stand-in uses.</summary>
    public const int FirstPort = 9006;

    private readonly ConcurrentDictionary<int, PendingConnection> listening = new();

    /// <summary>Listens on <paramref name="target"/>'s address and lowest port, or <see cref="FirstPort"/> when that is 0.</summary>
    /// <param name="target">The address and port range curl binds.</param>
    /// <param name="cancellationToken">Not used: the listen completes at once.</param>
    /// <returns>The listening result.</returns>
    public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken)
    {
        int port = target.LowPort == 0 ? FirstPort : target.LowPort;
        PendingConnection pending = new(new IPEndPoint(target.Address, port));
        listening[port] = pending;
        return ValueTask.FromResult(ListenResult.Listening(pending));
    }

    /// <summary>
    /// Connects to <paramref name="port"/> as ftpserver.pl's data <c>sockfilt</c> does after
    /// <c>PORT</c> or <c>EPRT</c>: the curl listening there accepts the returned connection.
    /// </summary>
    /// <param name="port">The port the client named.</param>
    /// <returns>The server's end of the data connection, or <see langword="null"/> when nothing listens there.</returns>
    public FtpDataConnection? Connect(int port)
    {
        if (!listening.TryRemove(port, out PendingConnection? pending))
        {
            return null;
        }

        FtpDataConnection connection = new() { LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 20), RemoteEndPoint = pending.LocalEndPoint };
        pending.Accepted.TrySetResult(connection);
        return connection;
    }

    private sealed class PendingConnection(EndPoint localEndPoint) : IPendingConnection
    {
        public TaskCompletionSource<FtpDataConnection> Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public EndPoint LocalEndPoint { get; } = localEndPoint;

        public async ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken) =>
            ConnectResult.Connected(await Accepted.Task.WaitAsync(cancellationToken).ConfigureAwait(false));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
