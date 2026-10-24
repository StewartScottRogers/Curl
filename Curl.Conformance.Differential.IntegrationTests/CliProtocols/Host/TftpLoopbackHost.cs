using System.Net;
using System.Net.Sockets;
using Curl.Conformance.Differential.Protocols.Binary;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.IntegrationTests.CliProtocols.Host;

/// <summary>
/// Serves <see cref="TftpResponder"/> on a loopback UDP port and the shared wire log's control responder on the
/// TCP port after it, as proto2_servers.py did for TFTP: each request datagram gets its own socket on an
/// ephemeral port with a two second wait, like the Python handler's.
/// </summary>
internal sealed class TftpLoopbackHost : IAsyncDisposable
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(2);

    private readonly UdpClient _listener;
    private readonly ProtocolLoopbackHost _control;
    private readonly TftpResponder _responder;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _receiving;

    private TftpLoopbackHost(int port, ProtocolWireLog log)
    {
        Port = port;
        _responder = new TftpResponder(log);
        _listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        _control = ProtocolLoopbackHost.Start(port, log, static (_, _) => Task.CompletedTask);
        _receiving = ReceiveAsync(_stopping.Token);
    }

    /// <summary>Gets the UDP port TFTP is served on; the control port is the TCP port after it.</summary>
    public int Port { get; }

    /// <summary>Gets the control port, which answers <c>/reset</c> and <c>/log</c>.</summary>
    public int ControlPort => Port + 1;

    /// <summary>Starts serving TFTP on <paramref name="port"/> and the control responder on <paramref name="port"/> + 1.</summary>
    public static TftpLoopbackHost Start(int port, ProtocolWireLog log) => new(port, log);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Dispose();
        await _receiving;
        await _control.DisposeAsync();
        _stopping.Dispose();
    }

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        var transfers = new List<Task>();
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult request;
            try
            {
                request = await _listener.ReceiveAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                break;
            }

            transfers.Add(TransferAsync(request, cancellationToken));
        }

        await Task.WhenAll(transfers);
    }

    private async Task TransferAsync(UdpReceiveResult request, CancellationToken cancellationToken)
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        try
        {
            await _responder.RespondAsync(request.Buffer, new TransferSocket(socket, request.RemoteEndPoint), cancellationToken);
        }
        catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // A cancelled or broken transfer ends only itself.
        }
    }

    private sealed class TransferSocket(UdpClient socket, IPEndPoint client) : ITftpTransferSocket
    {
        public async Task SendAsync(byte[] datagram, CancellationToken cancellationToken) =>
            await socket.SendAsync(datagram, client, cancellationToken);

        public async Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ReceiveTimeout);
            try
            {
                return (await socket.ReceiveAsync(timeout.Token)).Buffer;
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return null;
            }
        }
    }
}
