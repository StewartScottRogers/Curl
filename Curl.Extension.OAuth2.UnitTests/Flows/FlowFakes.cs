using System.Net;
using System.Text;
using Curl.Extension.OAuth2.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>Writes the HTTP/1.1 responses a fake authorization server answers with.</summary>
internal static class FlowResponses
{
    public static string Json(string body, int status = 200, string reason = "OK") =>
        $"HTTP/1.1 {status} {reason}\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";

    public static string Token(string accessToken = "2YotnFZFEjr1zCsicMWpAA") =>
        Json($$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":3600}""");

    public static string Error(string error, int status = 400, string reason = "Bad Request") =>
        Json($$"""{"error":"{{error}}"}""", status, reason);
}

/// <summary>A fake <see cref="IConnector"/> that hands out its connections in order, one per connect.</summary>
internal sealed class QueuedConnector(params RecordedConnection[] connections) : IConnector
{
    private int next;

    public IReadOnlyList<RecordedConnection> Connections => connections;

    public int ConnectCount => next;

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectResult.Connected(connections[next++]));
}

/// <summary>A fake <see cref="IConnectionListener"/> that answers every listen with one result and records the target.</summary>
internal sealed class RecordedListener(ListenResult result) : IConnectionListener
{
    public ListenTarget? Target { get; private set; }

    public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken)
    {
        Target = target;
        return ValueTask.FromResult(result);
    }
}

/// <summary>A fake <see cref="IPendingConnection"/> on 127.0.0.1:<see cref="Port"/> whose accept gives one result.</summary>
internal sealed class AcceptingPendingConnection(ConnectResult accepted) : IPendingConnection
{
    public const int Port = 49152;

    public AcceptingPendingConnection(RecordedConnection connection)
        : this(ConnectResult.Connected(connection))
    {
    }

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, Port);

    public bool IsDisposed { get; private set; }

    public ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken) => ValueTask.FromResult(accepted);

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A <see cref="TimeProvider"/> whose every timer moves the clock on by its due time and fires at once, recording the
/// delays asked for.
/// </summary>
internal sealed class AdvancingTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public List<TimeSpan> Delays { get; } = [];

    public override DateTimeOffset GetUtcNow() => now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Delays.Add(dueTime);
        now += dueTime;
        _ = Task.Run(() => callback(state));
        return new FiredTimer();
    }

    private sealed class FiredTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>A fake flow that gives one result and counts its runs.</summary>
internal sealed class CountingFlow(OAuth2FlowResult result) : IOAuth2TokenFlow
{
    public int Runs { get; private set; }

    public Task<OAuth2FlowResult> RunAsync(CancellationToken cancellationToken)
    {
        Runs++;
        return Task.FromResult(result);
    }
}
