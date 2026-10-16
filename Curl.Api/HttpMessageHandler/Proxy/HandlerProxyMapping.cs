using System.Net;

namespace Curl.Api;

/// <summary>Puts a <see cref="CurlHttpMessageHandler"/>'s proxy and credentials on the <see cref="CurlRequest"/> that sends a request (ADR-0649).</summary>
internal static class HandlerProxyMapping
{
    /// <summary>The <c>CURLOPT_PROXY</c> value that reaches the host directly, even past the proxy environment variables.</summary>
    internal const string NoProxy = "";

    /// <summary>
    /// <paramref name="request"/> with the handler's proxy, built on the client's <see cref="CurlClientOptions.Proxy"/>,
    /// and the handler's server and proxy credentials, built on the client's <see cref="CurlClientOptions.Authentication"/>.
    /// Each group is left alone (the client's apply) when the handler sets nothing for it.
    /// </summary>
    /// <param name="request">The mapped request.</param>
    /// <param name="uri">The request's absolute URI.</param>
    /// <param name="handler">The handler whose settings apply.</param>
    /// <returns>The request to send.</returns>
    public static CurlRequest Apply(CurlRequest request, Uri uri, CurlHttpMessageHandler handler)
    {
        Uri? through = ProxyThrough(uri, handler);
        string? proxyValue = ProxyValue(through, handler);
        NetworkCredential? server = handler.Credentials?.GetCredential(uri, "Basic");
        NetworkCredential? proxyCredential = ProxyCredential(through, handler);
        return request with
        {
            Proxy = proxyValue is null ? request.Proxy : (handler.Client.Options.Proxy ?? new CurlProxyOptions()) with { Proxy = proxyValue },
            Authentication = server is null && proxyCredential is null ? request.Authentication : WithCredentials(handler.Client.Options.Authentication, server, proxyCredential),
        };
    }

    /// <summary>The credential for the proxy gone <paramref name="through"/>: the proxy's own, else the handler's <see cref="CurlHttpMessageHandler.DefaultProxyCredentials"/>; none when no proxy is gone through.</summary>
    /// <param name="through">The proxy as <see cref="ProxyThrough"/> gave it.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The credential, or <see langword="null"/>.</returns>
    private static NetworkCredential? ProxyCredential(Uri? through, CurlHttpMessageHandler handler) =>
        through is null ? null : (handler.Proxy!.Credentials ?? handler.DefaultProxyCredentials)?.GetCredential(through, "Basic");

    /// <summary>The proxy the handler sends <paramref name="uri"/> through, or <see langword="null"/> when it has none, does not use one, bypasses the host or gives no proxy for it.</summary>
    /// <param name="uri">The request's URI.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The proxy's URI, or <see langword="null"/>.</returns>
    private static Uri? ProxyThrough(Uri uri, CurlHttpMessageHandler handler) =>
        handler.UseProxy && handler.Proxy is { } proxy && !proxy.IsBypassed(uri) ? proxy.GetProxy(uri) : null;

    /// <summary>
    /// The <c>CURLOPT_PROXY</c>: the scheme, host and port of the proxy gone <paramref name="through"/>,
    /// <see langword="null"/> (the client's applies) when the handler uses a proxy but has none set, and
    /// <see cref="NoProxy"/> when the host is reached directly.
    /// </summary>
    /// <param name="through">The proxy as <see cref="ProxyThrough"/> gave it.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The proxy string, or <see langword="null"/>.</returns>
    private static string? ProxyValue(Uri? through, CurlHttpMessageHandler handler) =>
        through is not null ? through.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
        : handler.UseProxy && handler.Proxy is null ? null
        : NoProxy;

    /// <summary>The client's authentication options with the user names and passwords of <paramref name="server"/> and <paramref name="proxy"/> set where given.</summary>
    /// <param name="clientAuthentication">The client's authentication options; <see langword="null"/> for libcurl's.</param>
    /// <param name="server">The server credential, or <see langword="null"/>.</param>
    /// <param name="proxy">The proxy credential, or <see langword="null"/>.</param>
    /// <returns>The request's authentication options.</returns>
    private static CurlAuthenticationOptions WithCredentials(CurlAuthenticationOptions? clientAuthentication, NetworkCredential? server, NetworkCredential? proxy)
    {
        CurlAuthenticationOptions authentication = clientAuthentication ?? new CurlAuthenticationOptions();
        return authentication with
        {
            UserName = server is null ? authentication.UserName : UserNameOf(server),
            Password = server is null ? authentication.Password : server.Password,
            ProxyUserName = proxy is null ? authentication.ProxyUserName : UserNameOf(proxy),
            ProxyPassword = proxy is null ? authentication.ProxyPassword : proxy.Password,
        };
    }

    /// <summary>The user name libcurl takes for <paramref name="credential"/>: <c>domain\user</c> when it has a domain, else the user.</summary>
    /// <param name="credential">The credential.</param>
    /// <returns>The user name.</returns>
    private static string UserNameOf(NetworkCredential credential) =>
        credential.Domain.Length == 0 ? credential.UserName : $"{credential.Domain}\\{credential.UserName}";
}
