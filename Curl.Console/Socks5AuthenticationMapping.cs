using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Maps the command line to the <see cref="Socks5AuthenticationOptions" /> the SOCKS5 greeting
/// and its GSS-API exchange follow (BL-615, ADR-0276).
/// </summary>
internal static class Socks5AuthenticationMapping
{
    /// <summary>
    /// Reads <c>--socks5-basic</c> and <c>--socks5-gssapi</c> as curl 8.21.0 does - neither
    /// allows both methods, else only the ones given - with the GSS-API service from
    /// <c>--socks5-gssapi-service</c>, else <c>--proxy-service-name</c>, else <c>rcmd</c>, and
    /// <c>--socks5-gssapi-nec</c>. <c>--delegation</c> applies only off Windows: curl's SSPI build
    /// asks no delegation of the SOCKS5 context, its GSS-API build asks what the option says.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="securityContexts">Makes the Kerberos contexts GSS-API runs on; <see langword="null" /> for none.</param>
    /// <param name="usesSspi">Whether this is the SSPI build: Windows.</param>
    /// <returns>The options.</returns>
    internal static Socks5AuthenticationOptions FromCommandLine(
        CommandLineOptions options,
        ISecurityContextFactory? securityContexts,
        bool usesSspi)
    {
        var bothAllowed = !options.Socks5BasicAuth && !options.Socks5GssapiAuth;
        return new(bothAllowed || options.Socks5BasicAuth, bothAllowed || options.Socks5GssapiAuth)
        {
            GssapiServiceName = GssapiServiceNameOf(options),
            GssapiNec = options.Socks5GssapiNec,
            GssapiDelegation = usesSspi ? SecurityDelegation.None : NegotiateOptionsMapping.FromCommandLine(options).Delegation,
            SecurityContexts = securityContexts,
            UsesSspiTexts = usesSspi,
        };
    }

    private static string GssapiServiceNameOf(CommandLineOptions options) =>
        options.Socks5GssapiServiceName ?? options.ProxyServiceName ?? Socks5AuthenticationOptions.DefaultGssapiServiceName;
}
