using Curl.Api.Options.Proxy;
using Curl.Authentication;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Maps the option group's <see cref="EasyProxyOptions" /> to the <see cref="Socks5AuthenticationOptions" /> the SOCKS5 greeting
/// and its GSS-API exchange follow (BL-615, ADR-0276).
/// </summary>
internal static class Socks5AuthenticationMapping
{
    /// <summary>
    /// Reads <see cref="EasyProxyOptions.Socks5Authentication" /> - <c>--socks5-basic</c> and
    /// <c>--socks5-gssapi</c>, where neither is libcurl's default of both methods (ADR-0515) - with the
    /// GSS-API service from <c>--socks5-gssapi-service</c>, else <c>--proxy-service-name</c>, else
    /// <c>rcmd</c>, and <c>--socks5-gssapi-nec</c>. <c>--delegation</c> applies only off Windows: curl's SSPI build
    /// asks no delegation of the SOCKS5 context, its GSS-API build asks what the option says.
    /// </summary>
    /// <param name="proxy">The option group's proxy options.</param>
    /// <param name="negotiate">
    /// The <c>--proxy-service-name</c> and <c>--delegation</c> of <see cref="NegotiateOptionsMapping.FromCommandLine" />,
    /// which belong to the authentication options.
    /// </param>
    /// <param name="securityContexts">Makes the Kerberos contexts GSS-API runs on; <see langword="null" /> for none.</param>
    /// <param name="usesSspi">Whether this is the SSPI build: Windows.</param>
    /// <returns>The options.</returns>
    internal static Socks5AuthenticationOptions Of(
        EasyProxyOptions proxy,
        NegotiateOptions negotiate,
        ISecurityContextFactory? securityContexts,
        bool usesSspi) =>
        new(proxy.Socks5Authentication.HasFlag(Socks5Authentication.Basic), proxy.Socks5Authentication.HasFlag(Socks5Authentication.GssApi))
        {
            GssapiServiceName = proxy.Socks5GssApiService ?? negotiate.ProxyServiceName ?? Socks5AuthenticationOptions.DefaultGssapiServiceName,
            GssapiNec = proxy.Socks5GssApiNec,
            GssapiDelegation = usesSspi ? SecurityDelegation.None : negotiate.Delegation,
            SecurityContexts = securityContexts,
            UsesSspiTexts = usesSspi,
        };
}
