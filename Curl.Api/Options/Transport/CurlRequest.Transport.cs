namespace Curl.Api;

public sealed partial record CurlRequest
{
    /// <summary>The network settings for this request alone, replacing the client's <see cref="CurlClientOptions.Network"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlNetworkOptions? Network { get; init; }

    /// <summary>The TLS settings for this request alone, replacing the client's <see cref="CurlClientOptions.Tls"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlTlsOptions? Tls { get; init; }

    /// <summary>The proxy settings for this request alone, replacing the client's <see cref="CurlClientOptions.Proxy"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlProxyOptions? Proxy { get; init; }
}
