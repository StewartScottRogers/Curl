using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.IntegrationTests.Host;

/// <summary>
/// Serves <see cref="DifferentialHttpResponder"/>'s routes on a loopback port and its control
/// port (<c>/reset</c>, <c>/log</c>) on the port after it, as the tester's <c>server.py</c> did.
/// </summary>
internal sealed class DifferentialLoopbackServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly TcpListener _controlListener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _accepting;
    private readonly Task _acceptingControl;
    private readonly ConcurrentDictionary<Task, bool> _inFlight = new();
    private readonly ConcurrentQueue<Exception> _faults = new();

    private DifferentialLoopbackServer(int port, Func<Stream, CancellationToken, Task>? routesResponder)
    {
        Port = port;
        var state = new DifferentialResponderState();
        var responder = new DifferentialHttpResponder(DifferentialRoutes.All, state, new DifferentialResponderOptions(port, TimeProvider.System));
        var control = new DifferentialControlResponder(state);
        _listener = new TcpListener(IPAddress.Loopback, port);
        _controlListener = new TcpListener(IPAddress.Loopback, port + 1);
        _listener.Start();
        _controlListener.Start();
        _accepting = AcceptAsync(_listener, routesResponder ?? responder.RespondAsync, _stopping.Token);
        _acceptingControl = AcceptAsync(_controlListener, control.RespondAsync, _stopping.Token);
    }

    /// <summary>Gets the port the routes are served on; the control port is the one after it.</summary>
    public int Port { get; }

    /// <summary>Gets the control port, which answers <c>/reset</c> and <c>/log</c>.</summary>
    public int ControlPort => Port + 1;

    /// <summary>Gets the base URL that replaces <c>{U}</c> in a case's arguments.</summary>
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    /// <summary>Starts serving on <paramref name="port"/> and <paramref name="port"/> + 1.</summary>
    public static DifferentialLoopbackServer Start(int port) => new(port, null);

    /// <summary>Starts serving on <paramref name="port"/> with <paramref name="routesResponder"/> in place of the routes, so a test can make the responder fail.</summary>
    public static DifferentialLoopbackServer StartWithResponder(int port, Func<Stream, CancellationToken, Task> routesResponder) =>
        new(port, routesResponder);

    /// <summary>Throws when the accept loop or a responder failed in any way other than a client hanging up.</summary>
    public void ThrowIfFaulted()
    {
        if (!_faults.IsEmpty)
        {
            throw new AggregateException("The loopback server failed, so a case result cannot be trusted.", _faults);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        _controlListener.Stop();
        await Task.WhenAll(_accepting, _acceptingControl);
        await Task.WhenAll(_inFlight.Keys);
        _stopping.Dispose();
        ThrowIfFaulted();
    }

    private async Task AcceptAsync(TcpListener listener, Func<Stream, CancellationToken, Task> respond, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _faults.Enqueue(exception);
                }

                return;
            }

            var serving = ServeAsync(client, respond, cancellationToken);
            _inFlight[serving] = true;
            _ = serving.ContinueWith(finished => _inFlight.TryRemove(finished, out _), TaskScheduler.Default);
        }
    }

    private async Task ServeAsync(TcpClient client, Func<Stream, CancellationToken, Task> respond, CancellationToken cancellationToken)
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
                // A client that hangs up mid-response ends only its own connection.
            }
            catch (Exception exception)
            {
                _faults.Enqueue(exception);
            }
        }
    }
}
