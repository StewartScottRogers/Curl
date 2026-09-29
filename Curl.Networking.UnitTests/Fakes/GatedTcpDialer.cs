using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ITcpDialer" /> whose dials stay pending until the test connects or refuses each
/// end point, recording when on the <see cref="TimeProvider" /> each dial started. A gate opened
/// before its dial starts ends that dial at once.
/// </summary>
/// <param name="timeProvider">The clock each dial's start is read from.</param>
public sealed class GatedTcpDialer(TimeProvider timeProvider) : ITcpDialer
{
    private readonly Dictionary<IPEndPoint, TaskCompletionSource<IConnection>> _gates = [];
    private readonly List<TaskCompletionSource> _dialWaiters = [];
    private readonly Lock _lock = new();

    /// <summary>Gets each dial started, in order, with the timestamp it started at.</summary>
    public List<(IPEndPoint EndPoint, long StartedAt)> Dials { get; } = [];

    /// <summary>Gets a value indicating whether a dial runs on after its cancellation, as a slow socket might.</summary>
    public bool IgnoresCancellation { get; init; }

    /// <inheritdoc />
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        Task<IConnection> gate;
        TaskCompletionSource[] reached;
        lock (_lock)
        {
            Dials.Add((endPoint, timeProvider.GetTimestamp()));
            gate = GateFor(endPoint).Task;
            reached = [.. _dialWaiters.Take(Dials.Count)];
        }

        foreach (var waiter in reached)
        {
            waiter.TrySetResult();
        }

        var connection = IgnoresCancellation ? await gate : await gate.WaitAsync(cancellationToken);
        return new DialedTcpConnection(connection, new IPEndPoint(IPAddress.Loopback, 50000));
    }

    /// <inheritdoc />
    public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <summary>Completes the dial to <paramref name="endPoint" /> with <paramref name="connection" />.</summary>
    public void Connect(IPEndPoint endPoint, IConnection connection) => Gate(endPoint).SetResult(connection);

    /// <summary>Fails the dial to <paramref name="endPoint" /> as refused.</summary>
    public void Refuse(IPEndPoint endPoint) =>
        Gate(endPoint).SetException(new SocketException((int)SocketError.ConnectionRefused));

    /// <summary>Waits until at least <paramref name="count" /> dials have started.</summary>
    public Task WaitForDialsAsync(int count)
    {
        lock (_lock)
        {
            while (_dialWaiters.Count < count)
            {
                _dialWaiters.Add(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            }

            var waiter = _dialWaiters[count - 1];
            if (Dials.Count >= count)
            {
                waiter.TrySetResult();
            }

            return waiter.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private TaskCompletionSource<IConnection> Gate(IPEndPoint endPoint)
    {
        lock (_lock)
        {
            return GateFor(endPoint);
        }
    }

    private TaskCompletionSource<IConnection> GateFor(IPEndPoint endPoint)
    {
        if (!_gates.TryGetValue(endPoint, out var gate))
        {
            gate = new TaskCompletionSource<IConnection>();
            _gates[endPoint] = gate;
        }

        return gate;
    }
}
