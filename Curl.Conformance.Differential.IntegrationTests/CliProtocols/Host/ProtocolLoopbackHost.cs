using System.Net;
using System.Net.Sockets;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.IntegrationTests.CliProtocols.Host;

/// <summary>
/// Serves one protocol responder on a loopback port and the shared wire log's control responder on the
/// port after it, as proto_servers.py did for each of its servers (BL-3285 to BL-3288 reuse it).
/// </summary>
internal sealed class ProtocolLoopbackHost : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly TcpListener _controlListener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _accepting;
    private readonly Task _acceptingControl;

    private ProtocolLoopbackHost(int port, ProtocolWireLog log, Func<Stream, CancellationToken, Task> respond)
    {
        Port = port;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _controlListener = new TcpListener(IPAddress.Loopback, port + 1);
        _listener.Start();
        _controlListener.Start();
        _accepting = AcceptAsync(_listener, respond, _stopping.Token);
        _acceptingControl = AcceptAsync(_controlListener, new ProtocolControlResponder(log).RespondAsync, _stopping.Token);
    }

    /// <summary>Gets the port the protocol is served on; the control port is the one after it.</summary>
    public int Port { get; }

    /// <summary>Gets the control port, which answers <c>/reset</c> and <c>/log</c>.</summary>
    public int ControlPort => Port + 1;

    /// <summary>Starts serving <paramref name="respond"/> on <paramref name="port"/> and the control responder on <paramref name="port"/> + 1.</summary>
    public static ProtocolLoopbackHost Start(int port, ProtocolWireLog log, Func<Stream, CancellationToken, Task> respond) => new(port, log, respond);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        _controlListener.Stop();
        await Task.WhenAll(_accepting, _acceptingControl);
        _stopping.Dispose();
    }

    private static async Task AcceptAsync(TcpListener listener, Func<Stream, CancellationToken, Task> respond, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            _ = ServeAsync(client, respond, cancellationToken);
        }
    }

    private static async Task ServeAsync(TcpClient client, Func<Stream, CancellationToken, Task> respond, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                await respond(stream, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException or SocketException or ObjectDisposedException)
            {
                // A client that hangs up mid-conversation ends only its own connection.
            }
        }
    }
}
