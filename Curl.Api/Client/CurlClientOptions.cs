namespace Curl.Api;

/// <summary>
/// The defaults a <see cref="CurlClient"/> applies to every easy handle it creates. Each value
/// starts at libcurl's own default, so a client made with <c>new CurlClientOptions()</c> sets
/// nothing a fresh <c>curl_easy_init</c> handle does not already have.
/// </summary>
public sealed record CurlClientOptions
{
    /// <summary><c>CURLOPT_USERAGENT</c> for every handle; <see langword="null"/> (the default) sends no <c>User-Agent</c> header.</summary>
    public string? UserAgent { get; init; }

    /// <summary><c>CURLOPT_TIMEOUT_MS</c> for every handle: the whole transfer's limit. <see cref="TimeSpan.Zero"/> (the default) means none.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.Zero;

    /// <summary><c>CURLOPT_CONNECTTIMEOUT_MS</c> for every handle. <see cref="TimeSpan.Zero"/> (the default) means libcurl's built-in 300 seconds.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.Zero;
}
