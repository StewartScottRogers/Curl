namespace Libcurl.Multi;

/// <summary>
/// The options <see cref="CurlMulti.SetOption"/> sets (<c>CURLMoption</c>, <c>CURLMOPT_*</c>), with
/// upstream curl 8.22.0's numbers from <c>include/curl/multi.h</c>: each is its <c>CURLOPTTYPE_*</c>
/// base (0 a <c>long</c>, 10000 an object pointer, 20000 a function pointer, 30000 a
/// <c>curl_off_t</c>) plus its number.
/// </summary>
public enum CurlMultiOption
{
    /// <summary><c>CURLMOPT_SOCKETFUNCTION</c> (20001): the socket callback, a <c>MultiSocketCallback</c>.</summary>
    SocketFunction = 20001,

    /// <summary><c>CURLMOPT_SOCKETDATA</c> (10002): the socket callback's user data; its closure carries it.</summary>
    SocketData = 10002,

    /// <summary><c>CURLMOPT_PIPELINING</c> (3): the <c>CURLPIPE_*</c> bitmask.</summary>
    Pipelining = 3,

    /// <summary><c>CURLMOPT_TIMERFUNCTION</c> (20004): the timer callback, a <c>MultiTimerCallback</c>.</summary>
    TimerFunction = 20004,

    /// <summary><c>CURLMOPT_TIMERDATA</c> (10005): the timer callback's user data; its closure carries it.</summary>
    TimerData = 10005,

    /// <summary><c>CURLMOPT_MAXCONNECTS</c> (6): the connection cache's size.</summary>
    MaxConnects = 6,

    /// <summary><c>CURLMOPT_MAX_HOST_CONNECTIONS</c> (7): connections to one host at a time.</summary>
    MaxHostConnections = 7,

    /// <summary><c>CURLMOPT_MAX_PIPELINE_LENGTH</c> (8): ignored since libcurl 7.62.0.</summary>
    MaxPipelineLength = 8,

    /// <summary><c>CURLMOPT_CONTENT_LENGTH_PENALTY_SIZE</c> (30009): ignored since libcurl 7.62.0.</summary>
    ContentLengthPenaltySize = 30009,

    /// <summary><c>CURLMOPT_CHUNK_LENGTH_PENALTY_SIZE</c> (30010): ignored since libcurl 7.62.0.</summary>
    ChunkLengthPenaltySize = 30010,

    /// <summary><c>CURLMOPT_PIPELINING_SITE_BL</c> (10011): ignored since libcurl 7.62.0.</summary>
    PipeliningSiteBlocklist = 10011,

    /// <summary><c>CURLMOPT_PIPELINING_SERVER_BL</c> (10012): ignored since libcurl 7.62.0.</summary>
    PipeliningServerBlocklist = 10012,

    /// <summary><c>CURLMOPT_MAX_TOTAL_CONNECTIONS</c> (13): connections open at a time.</summary>
    MaxTotalConnections = 13,

    /// <summary><c>CURLMOPT_PUSHFUNCTION</c> (20014): the server push callback, a <c>MultiPushCallback</c>.</summary>
    PushFunction = 20014,

    /// <summary><c>CURLMOPT_PUSHDATA</c> (10015): the push callback's user data; its closure carries it.</summary>
    PushData = 10015,

    /// <summary><c>CURLMOPT_MAX_CONCURRENT_STREAMS</c> (16): streams per multiplexed connection.</summary>
    MaxConcurrentStreams = 16,

    /// <summary><c>CURLMOPT_NETWORK_CHANGED</c> (17): the <c>CURLMNWC_*</c> bitmask of what to clear.</summary>
    NetworkChanged = 17,

    /// <summary><c>CURLMOPT_NOTIFYFUNCTION</c> (20018): the notification callback, a <c>MultiNotifyCallback</c>.</summary>
    NotifyFunction = 20018,

    /// <summary><c>CURLMOPT_NOTIFYDATA</c> (10019): the notification callback's user data; its closure carries it.</summary>
    NotifyData = 10019,
}
