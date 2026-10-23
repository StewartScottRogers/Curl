using System.Collections.Concurrent;
using Curl.Mcp.Protocol.JsonRpc;

namespace Curl.Mcp.Protocol.Server;

/// <summary>Holds the cancellation source of every request in flight, keyed by its id, so a request can be cancelled by id or all at once.</summary>
public sealed class McpRequestTracker
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlight = new(StringComparer.Ordinal);

    /// <summary>Starts tracking a request and returns its cancellation source, linked to <paramref name="cancellationToken" />.</summary>
    /// <param name="id">The request's id.</param>
    /// <param name="cancellationToken">The host's token.</param>
    /// <returns>The request's own cancellation source.</returns>
    public CancellationTokenSource Start(JsonRpcRequestId id, CancellationToken cancellationToken)
    {
        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _inFlight[KeyOf(id)] = source;
        return source;
    }

    /// <summary>Stops tracking a request that has finished, unless a later request with the same id has replaced it.</summary>
    /// <param name="id">The request's id.</param>
    /// <param name="source">The source <see cref="Start" /> returned for it.</param>
    public void Finish(JsonRpcRequestId id, CancellationTokenSource source) =>
        _inFlight.TryRemove(new KeyValuePair<string, CancellationTokenSource>(KeyOf(id), source));

    /// <summary>Cancels the request in flight whose id has the JSON text <paramref name="idText" />; does nothing when there is none.</summary>
    /// <param name="idText">The id's raw JSON text, e.g. <c>7</c>, or <c>"a-1"</c> with its quotes.</param>
    public void Cancel(string idText)
    {
        if (_inFlight.TryGetValue(idText, out CancellationTokenSource? source))
        {
            source.Cancel();
        }
    }

    /// <summary>Cancels every request in flight.</summary>
    public void CancelAll()
    {
        foreach (CancellationTokenSource source in _inFlight.Values)
        {
            source.Cancel();
        }
    }

    private static string KeyOf(JsonRpcRequestId id) => id.Value.GetRawText();
}
