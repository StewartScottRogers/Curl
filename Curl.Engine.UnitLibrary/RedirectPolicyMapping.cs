using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Cli;
using Curl.Core;

namespace Curl.Engine;

/// <summary>
/// Maps the redirect options of an option group onto the <see cref="RedirectPolicy" />
/// <see cref="RedirectFollower" /> applies under <c>-L</c>: from <see cref="EasyHttpOptions" />
/// <c>--max-redirs</c>, <c>--post301</c>, <c>--post302</c>, <c>--post303</c> and <c>--follow</c>, from
/// <see cref="EasyAuthenticationOptions" /> <c>--location-trusted</c> and <c>--disallow-username-in-url</c>, which
/// the follower applies to every redirect target, and from the command line <c>--proto-redir</c>, with
/// <c>--proto</c>, which it applies to every URL.
/// </summary>
internal static class RedirectPolicyMapping
{
    /// <summary>
    /// Copies the redirect options from <paramref name="http" />, <paramref name="authentication" /> and
    /// <paramref name="options" />.
    /// </summary>
    /// <param name="options">The parsed command line, for <c>--proto</c> and <c>--proto-redir</c>.</param>
    /// <param name="http">The option group's HTTP options.</param>
    /// <param name="authentication">The option group's authentication options.</param>
    /// <returns>
    /// The policy: <see cref="EasyHttpOptions.MaxRedirects" /> verbatim, the three
    /// <see cref="EasyHttpOptions.PostRedirect" /> bits as the <c>KeepPostOn</c> flags,
    /// <see cref="EasyHttpOptions.FollowLocation" /> <see cref="FollowLocation.ObeyCode" /> (<c>--follow</c>) as
    /// <see cref="RedirectPolicy.DropsCustomMethodOnSwitchToGet" />,
    /// <see cref="EasyAuthenticationOptions.UnrestrictedAuthentication" /> as <see cref="RedirectPolicy.LocationTrusted" />,
    /// <see cref="CommandLineOptions.AllowedRedirectProtocols" /> as
    /// <see cref="RedirectPolicy.AllowedSchemes" /> (curl's default when not given),
    /// <see cref="CommandLineOptions.AllowedProtocols" /> as <see cref="RedirectPolicy.AllowedTransferSchemes" />,
    /// and <see cref="EasyAuthenticationOptions.DisallowUserNameInUrl" /> as <see cref="RedirectPolicy.DisallowsUserInUrl" />.
    /// </returns>
    internal static RedirectPolicy Of(CommandLineOptions options, EasyHttpOptions http, EasyAuthenticationOptions authentication)
    {
        RedirectPolicy policy = new()
        {
            MaxRedirects = (int)http.MaxRedirects,
            KeepPostOn301 = http.PostRedirect.HasFlag(PostRedirect.Post301),
            KeepPostOn302 = http.PostRedirect.HasFlag(PostRedirect.Post302),
            KeepPostOn303 = http.PostRedirect.HasFlag(PostRedirect.Post303),
            LocationTrusted = authentication.UnrestrictedAuthentication,
            DropsCustomMethodOnSwitchToGet = http.FollowLocation == FollowLocation.ObeyCode,
            AllowedTransferSchemes = options.AllowedProtocols,
            DisallowsUserInUrl = authentication.DisallowUserNameInUrl,
        };
        return options.AllowedRedirectProtocols is { } redirectSchemes
            ? policy with { AllowedSchemes = redirectSchemes }
            : policy;
    }
}
