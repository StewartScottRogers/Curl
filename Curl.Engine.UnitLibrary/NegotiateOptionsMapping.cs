using Curl.Api.Options.Authentication;
using Curl.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>Maps the option group's authentication options to the <see cref="NegotiateOptions" /> the Negotiate authenticator applies (ADR-0188).</summary>
internal static class NegotiateOptionsMapping
{
    /// <summary>
    /// Copies <see cref="EasyAuthenticationOptions.ServiceName" /> (<c>--service-name</c>),
    /// <see cref="EasyAuthenticationOptions.ProxyServiceName" /> (<c>--proxy-service-name</c>) and
    /// <see cref="EasyAuthenticationOptions.GssApiDelegation" /> (<c>--delegation</c>) from <paramref name="authentication" />.
    /// </summary>
    /// <param name="authentication">The option group's authentication options.</param>
    /// <returns>The two service names verbatim, <see langword="null" /> when not given, and the delegation level.</returns>
    internal static NegotiateOptions Of(EasyAuthenticationOptions authentication) =>
        new(authentication.ServiceName, authentication.ProxyServiceName, DelegationOf(authentication.GssApiDelegation));

    private static SecurityDelegation DelegationOf(GssApiDelegation delegation) => delegation switch
    {
        GssApiDelegation.PolicyFlag => SecurityDelegation.Policy,
        GssApiDelegation.Flag => SecurityDelegation.Always,
        _ => SecurityDelegation.None,
    };
}
