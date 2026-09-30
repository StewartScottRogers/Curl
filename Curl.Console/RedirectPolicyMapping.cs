using Curl.Cli;
using Curl.Core;

namespace Curl.Console;

/// <summary>
/// Maps the redirect options of a parsed command line onto the <see cref="RedirectPolicy" />
/// <see cref="RedirectFollower" /> applies under <c>-L</c>: <c>--max-redirs</c>,
/// <c>--post301</c>, <c>--post302</c>, <c>--post303</c>, <c>--location-trusted</c>, <c>--follow</c> and
/// <c>--proto-redir</c>, with <c>--proto</c>, which the follower applies to every URL, and
/// <c>--disallow-username-in-url</c>, which it applies to every redirect target.
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
    /// verbatim, <see cref="CommandLineOptions.AllowedRedirectProtocols" /> as
    /// <see cref="RedirectPolicy.AllowedSchemes" /> (curl's default when not given), and
    /// <see cref="CommandLineOptions.AllowedProtocols" /> as <see cref="RedirectPolicy.AllowedTransferSchemes" />,
    /// <see cref="CommandLineOptions.FollowRedirectsPerSpec" /> as <see cref="RedirectPolicy.DropsCustomMethodOnSwitchToGet" />,
    /// and <see cref="CommandLineOptions.DisallowUsernameInUrl" /> as <see cref="RedirectPolicy.DisallowsUserInUrl" />.
    /// </returns>
    internal static RedirectPolicy FromCommandLine(CommandLineOptions options)
    {
        RedirectPolicy policy = new()
        {
            MaxRedirects = options.MaxRedirects,
            KeepPostOn301 = options.KeepPostAfter301,
            KeepPostOn302 = options.KeepPostAfter302,
            KeepPostOn303 = options.KeepPostAfter303,
            LocationTrusted = options.SendCredentialsToRedirectHosts,
            DropsCustomMethodOnSwitchToGet = options.FollowRedirectsPerSpec,
            AllowedTransferSchemes = options.AllowedProtocols,
            DisallowsUserInUrl = options.DisallowUsernameInUrl,
        };
        return options.AllowedRedirectProtocols is { } redirectSchemes
            ? policy with { AllowedSchemes = redirectSchemes }
            : policy;
    }
}
