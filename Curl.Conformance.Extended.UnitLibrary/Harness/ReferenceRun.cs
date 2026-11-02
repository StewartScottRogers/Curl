using System.Runtime.ExceptionServices;

namespace Curl.Conformance.Extended.Harness;

/// <summary>One run's open exchanges and the server tasks serving them.</summary>
internal sealed class ReferenceRun : IDisposable
{
    // How long, in real time, the servers may take to finish once curl's ends are closed.
    private static readonly TimeSpan DrainGrace = TimeSpan.FromSeconds(5);

    private readonly Lock gate = new();

    private readonly List<WireExchange> exchanges = [];

    private readonly List<Task> serving = [];

    private readonly List<InMemoryDuplexStream> clientEnds = [];

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

    public void Serve(Task server, InMemoryDuplexStream clientEnd)
    {
        lock (gate)
        {
            serving.Add(server);
            clientEnds.Add(clientEnd);
        }
    }

    // Once curl is done, closes curl's end of every connection, as a real socket closes when curl
    // exits, so each server reads what curl last sent and then the end of the stream (BL-4543:
    // stopping the servers at once lost a DISCONNECT still unread under load). A server still
    // waiting after DrainGrace, on something other than curl, is then stopped, which is no
    // failure; a server that failed, such as a TLS handshake curl never completed, throws its
    // exception here so the caller's test fails.
    public async Task<IReadOnlyList<WireExchange>> EndAsync()
    {
        Task[] servers;
        InMemoryDuplexStream[] ends;
        lock (gate)
        {
            servers = [.. serving];
            ends = [.. clientEnds];
        }

        foreach (InMemoryDuplexStream end in ends)
        {
            await end.DisposeAsync().ConfigureAwait(false);
        }

        Task allServed = Task.WhenAll(servers).ContinueWith(static _ => { }, TaskScheduler.Default);
        await Task.WhenAny(allServed, Task.Delay(DrainGrace, TimeProvider.System)).ConfigureAwait(false);
        await stopping.CancelAsync().ConfigureAwait(false);
        await allServed.ConfigureAwait(false);
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
