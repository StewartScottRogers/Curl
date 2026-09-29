using Curl.Authentication;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>Maps the command line to the <see cref="NegotiateOptions" /> the Negotiate authenticator applies (ADR-0188).</summary>
internal static class NegotiateOptionsMapping
{
    /// <summary>
    /// Copies <c>--service-name</c>, <c>--proxy-service-name</c> and <c>--delegation</c> from
    /// <paramref name="options" />.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The two service names verbatim, <see langword="null" /> when not given, and the delegation level.</returns>
    internal static NegotiateOptions FromCommandLine(CommandLineOptions options) =>
        new(options.ServiceName, options.ProxyServiceName, DelegationOf(options.GssApiDelegation));

    private static SecurityDelegation DelegationOf(GssApiDelegation delegation) => delegation switch
    {
        GssApiDelegation.Policy => SecurityDelegation.Policy,
        GssApiDelegation.Always => SecurityDelegation.Always,
        _ => SecurityDelegation.None,
    };
}
