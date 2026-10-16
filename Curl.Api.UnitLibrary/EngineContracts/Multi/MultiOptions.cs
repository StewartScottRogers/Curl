namespace Curl.Api.EngineContracts.Multi;

/// <summary>
/// The options of one multi handle: the <c>curl_multi_setopt</c> <c>CURLMOPT_*</c> options, each
/// defaulting to libcurl 8.22.0's value, so a new instance is a fresh multi handle's options. Each
/// callback's <c>*DATA</c> user pointer is the delegate's closure.
/// </summary>
public sealed record MultiOptions
{
    /// <summary><c>CURLMOPT_MAXCONNECTS</c>: the connection cache's size. Default 0 (libcurl sizes it to 4 times the added transfers).</summary>
    public long MaxConnects { get; init; }

    /// <summary><c>CURLMOPT_MAX_HOST_CONNECTIONS</c>: connections to one host at a time. Default 0 (no limit).</summary>
    public long MaxHostConnections { get; init; }

    /// <summary><c>CURLMOPT_MAX_TOTAL_CONNECTIONS</c>: connections open at a time. Default 0 (no limit).</summary>
    public long MaxTotalConnections { get; init; }

    /// <summary><c>CURLMOPT_MAX_CONCURRENT_STREAMS</c>: streams per multiplexed connection. Default 100.</summary>
    public long MaxConcurrentStreams { get; init; } = 100;

    /// <summary><c>CURLMOPT_PIPELINING</c>: whether transfers share connections. Default <see cref="CurlPipelining.Multiplex"/>.</summary>
    public CurlPipelining Pipelining { get; init; } = CurlPipelining.Multiplex;

    /// <summary><c>CURLMOPT_NETWORK_CHANGED</c>: what to clear because the network changed. Default <see cref="CurlNetworkChange.None"/>.</summary>
    public CurlNetworkChange NetworkChanged { get; init; }

    /// <summary><c>CURLMOPT_SOCKETFUNCTION</c>: the socket callback. Default <see langword="null"/>.</summary>
    public MultiSocketCallback? Socket { get; init; }

    /// <summary><c>CURLMOPT_TIMERFUNCTION</c>: the timer callback. Default <see langword="null"/>.</summary>
    public MultiTimerCallback? Timer { get; init; }

    /// <summary><c>CURLMOPT_PUSHFUNCTION</c>: the server push callback. Default <see langword="null"/> (pushes are refused).</summary>
    public MultiPushCallback? Push { get; init; }

    /// <summary><c>CURLMOPT_NOTIFYFUNCTION</c>: the notification callback. Default <see langword="null"/>.</summary>
    public MultiNotifyCallback? Notify { get; init; }
}
