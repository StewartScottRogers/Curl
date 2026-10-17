namespace Curl.Api;

public sealed partial record CurlClientOptions
{
    /// <summary>The network settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Network"/> replaces it.</summary>
    public CurlNetworkOptions? Network { get; init; }

    /// <summary>The TLS settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Tls"/> replaces it.</summary>
    public CurlTlsOptions? Tls { get; init; }

    /// <summary>The proxy settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Proxy"/> replaces it.</summary>
    public CurlProxyOptions? Proxy { get; init; }
}
