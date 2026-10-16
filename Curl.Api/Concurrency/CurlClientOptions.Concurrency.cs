namespace Curl.Api;

public sealed partial record CurlClientOptions
{
    /// <summary>
    /// <c>CURLMOPT_MAX_TOTAL_CONNECTIONS</c> for the client's multi handle: the connections its
    /// concurrent transfers may hold open at once; a transfer past the limit waits for one to free.
    /// 0 (the default) means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long MaxConnections
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>
    /// <c>CURLMOPT_MAX_HOST_CONNECTIONS</c> for the client's multi handle: the connections its
    /// concurrent transfers may hold open to one host at once. 0 (the default) means no limit.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long MaxConnectionsPerHost
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }
}
