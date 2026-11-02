using System.Runtime.ExceptionServices;

namespace Curl.Conformance.Extended.Harness;

/// <summary>One run's open exchanges and the server tasks serving them.</summary>
internal sealed class ReferenceRun : IDisposable
{
    private readonly Lock gate = new();

    private readonly List<WireExchange> exchanges = [];

    private readonly List<Task> serving = [];

    private readonly CancellationTokenSource stopping = new();

    public CancellationToken Stopping => stopping.Token;

    public WireExchange Open(ReferenceRoute route)
    {
        WireExchange exchange = new(route.Port, route.Transport);
        lock (gate)
        {
            exchanges.Add(exchange);
        }

        return exchange;
    }

    public void Serve(Task server)
    {
        lock (gate)
        {
            serving.Add(server);
        }
    }

    // Stops every server still waiting once curl is done, waits for each, and returns the exchanges.
    // A server stopped while waiting ends cancelled, which is no failure; a server that failed, such
    // as a TLS handshake curl never completed, throws its exception here so the caller's test fails.
    public async Task<IReadOnlyList<WireExchange>> EndAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        Task[] servers;
        lock (gate)
        {
            servers = [.. serving];
        }

        await Task.WhenAll(servers).ContinueWith(static _ => { }, TaskScheduler.Default).ConfigureAwait(false);
        ExceptionDispatchInfo? failure = servers
            .Where(static server => server.IsFaulted)
            .Select(static server => ExceptionDispatchInfo.Capture(server.Exception!.InnerException!))
            .FirstOrDefault();
        failure?.Throw();

        lock (gate)
        {
            return [.. exchanges];
        }
    }

    public void Dispose() => stopping.Dispose();
}
