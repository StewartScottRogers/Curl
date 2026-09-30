using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// An <see cref="IConnector" /> whose every connection is an in-memory HTTP/2 server speaking to
/// a prior-knowledge client (BL-717): it sends its SETTINGS at once, with
/// <c>SETTINGS_MAX_CONCURRENT_STREAMS</c> when one is given, records the streams the client opens,
/// and answers each with <c>:status 200</c> and END_STREAM, at once or when the test releases it.
/// Connects can be held until several have been asked for, so a test decides whether transfers
/// find the first connection still being opened.
/// </summary>
/// <param name="maxConcurrentStreams">The server's stream limit, or <see langword="null" /> to send none.</param>
/// <param name="connectsHeldTogether">How many connects are held until all of them have been asked for.</param>
internal sealed class Http2ServerConnector(uint? maxConcurrentStreams = null, int connectsHeldTogether = 1) : IConnector
{
    private readonly Lock gate = new();

    private readonly TaskCompletionSource connectsAsked = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets every connection opened, in the order asked for.</summary>
    public List<Http2ServerConnection> Opened { get; } = [];

    /// <summary>Gets a value indicating whether each response waits for <see cref="Http2ServerConnection.ReleaseResponses" />.</summary>
    public bool HoldsResponses { get; init; }

    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Http2ServerConnection connection = new(target.Host, maxConcurrentStreams, HoldsResponses);
        lock (gate)
        {
            Opened.Add(connection);
            if (Opened.Count >= connectsHeldTogether)
            {
                connectsAsked.TrySetResult();
            }
        }

        await connectsAsked.Task.WaitAsync(cancellationToken);
        return ConnectResult.Connected(connection, new ConnectTimings(1, 1, 2, null), new IPEndPoint(IPAddress.Loopback, 50000));
    }

    /// <summary>Gets the connections opened to <paramref name="host" />.</summary>
    /// <param name="host">The host.</param>
    /// <returns>The connections, in the order asked for.</returns>
    public Http2ServerConnection[] To(string host)
    {
        lock (gate)
        {
            return [.. Opened.Where(connection => connection.Host == host)];
        }
    }
}
