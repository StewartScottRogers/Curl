namespace Curl.Core;

/// <summary>
/// The command-line options that decide how <see cref="RedirectFollower" /> follows a
/// redirect under <c>-L</c>/<c>--location</c>; every member defaults to curl 8.21.0's
/// behaviour when the option is not given. It also carries the <c>--proto</c> schemes, which
/// <see cref="RedirectFollower" /> applies to the first URL as well as to every redirect target.
/// </summary>
public sealed record RedirectPolicy
{
    /// <summary>
    /// curl's <c>--max-redirs</c> default: 50 redirects.
    /// </summary>
    public const int DefaultMaxRedirects = 50;

    /// <summary>
    /// Gets the most redirects to follow, per <c>--max-redirs</c>; a negative value, as
    /// curl's <c>-1</c>, means no limit. Following one more fails with exit 47
    /// (<c>Maximum (N) redirects followed</c>).
    /// </summary>
    public int MaxRedirects { get; init; } = DefaultMaxRedirects;

    /// <summary>
    /// Gets a value indicating whether <c>--post301</c> was given: a POST answered with
    /// 301 is re-sent as POST instead of becoming GET.
    /// </summary>
    public bool KeepPostOn301 { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--post302</c> was given: a POST answered with
    /// 302 is re-sent as POST instead of becoming GET.
    /// </summary>
    public bool KeepPostOn302 { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--post303</c> was given: a POST answered with
    /// 303 is re-sent as POST instead of becoming GET.
    /// </summary>
    public bool KeepPostOn303 { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--location-trusted</c> was given: credentials,
    /// the bearer token and <c>-H</c> <c>Authorization:</c> and <c>Cookie:</c> headers are
    /// sent to every redirect target, not only to the first URL's host, port and scheme.
    /// </summary>
    public bool LocationTrusted { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--follow</c> was given rather than <c>-L</c>: a <c>-X</c>
    /// method is dropped whenever a redirect switches the request to GET - a 301 or 302 that drops
    /// the body, and every 303 unless <c>--post303</c> keeps the body - where <c>-L</c> keeps it
    /// (curl 8.21.0, measured, BL-627 Notes).
    /// </summary>
    public bool DropsCustomMethodOnSwitchToGet { get; init; }

    /// <summary>
    /// Gets the lowercase schemes a redirect may lead to; curl's default, <c>http</c>,
    /// <c>https</c>, <c>ftp</c> and <c>ftps</c>, when not given.
    /// </summary>
    public IReadOnlySet<string> AllowedSchemes { get; init; } =
        new HashSet<string>(["http", "https", "ftp", "ftps"], StringComparer.Ordinal);

    /// <summary>
    /// Gets the lowercase schemes <c>--proto</c> allows every URL the transfer requests to use,
    /// the first URL and each redirect target alike; <see langword="null" />, allowing every scheme,
    /// when not given. A redirect target must be allowed by both this and <see cref="AllowedSchemes" />
    /// (curl 8.21.0, measured, BL-523 Notes).
    /// </summary>
    public IReadOnlySet<string>? AllowedTransferSchemes { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--disallow-username-in-url</c> was given: a redirect
    /// target with user information is refused with <see cref="RedirectFollower.CredentialsInUrlMessage" />
    /// after it is counted as followed (curl 8.21.0, measured, BL-626 Notes).
    /// </summary>
    public bool DisallowsUserInUrl { get; init; }
}
