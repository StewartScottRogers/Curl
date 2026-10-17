using System.Net;

namespace Curl.Api;

/// <summary>The proxy half of the handler (BL-2511, ADR-0649), named as <see cref="HttpClientHandler"/> names them.</summary>
public sealed partial class CurlHttpMessageHandler
{
    /// <summary>
    /// The proxy every request goes through: its <see cref="IWebProxy.GetProxy"/> becomes <c>CURLOPT_PROXY</c>,
    /// a host it <see cref="IWebProxy.IsBypassed">bypasses</see> is reached directly, and its
    /// <see cref="IWebProxy.Credentials"/> become <c>CURLOPT_PROXYUSERNAME</c> and <c>CURLOPT_PROXYPASSWORD</c>.
    /// Default <see langword="null"/>: the client's <see cref="CurlClientOptions.Proxy"/> (or the proxy environment variables) apply.
    /// </summary>
    public IWebProxy? Proxy { get; set; }

    /// <summary>Whether requests may go through a proxy. Default <see langword="true"/>; <see langword="false"/> reaches every host directly, the client's proxy and the proxy environment variables included.</summary>
    public bool UseProxy { get; set; } = true;

    /// <summary>The proxy credentials used when <see cref="Proxy"/> has none of its own. Default <see langword="null"/>.</summary>
    public ICredentials? DefaultProxyCredentials { get; set; }
}
