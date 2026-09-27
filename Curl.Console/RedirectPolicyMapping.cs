using Curl.Cli;
using Curl.Core;

namespace Curl.Console;

/// <summary>
/// Maps the redirect options of a parsed command line onto the <see cref="RedirectPolicy" />
/// <see cref="RedirectFollower" /> applies under <c>-L</c>: <c>--max-redirs</c>,
/// <c>--post301</c>, <c>--post302</c>, <c>--post303</c> and <c>--location-trusted</c>.
/// </summary>
internal static class RedirectPolicyMapping
{
    /// <summary>
    /// Copies the redirect options from <paramref name="options" />.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>
    /// The policy: <see cref="CommandLineOptions.MaxRedirects" />, the three
    /// <c>KeepPostAfter</c> flags and <see cref="CommandLineOptions.SendCredentialsToRedirectHosts" />
    /// verbatim, and curl's default <see cref="RedirectPolicy.AllowedSchemes" />.
    /// </returns>
    internal static RedirectPolicy FromCommandLine(CommandLineOptions options) =>
        new()
        {
            MaxRedirects = options.MaxRedirects,
            KeepPostOn301 = options.KeepPostAfter301,
            KeepPostOn302 = options.KeepPostAfter302,
            KeepPostOn303 = options.KeepPostAfter303,
            LocationTrusted = options.SendCredentialsToRedirectHosts,
        };
}
