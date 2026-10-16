namespace Curl.Api;

/// <summary>
/// Holds an <see cref="HttpRequestMessage"/>'s <see cref="HttpRequestMessage.Version"/> and
/// <see cref="HttpRequestMessage.VersionPolicy"/> to what <c>SocketsHttpHandler</c> allows (BL-2511, ADR-0649):
/// only 1.0, 1.1, 2.0 and 3.0 are spoken, a higher version under
/// <see cref="HttpVersionPolicy.RequestVersionOrLower"/> is asked for as 3.0, and a response whose version
/// the policy does not allow fails with <see cref="HttpRequestError.VersionNegotiationError"/>.
/// </summary>
internal static class HttpVersionPolicyGuard
{
    /// <summary>The highest version the engine speaks.</summary>
    private static readonly Version Highest = System.Net.HttpVersion.Version30;

    /// <summary>
    /// The version to ask the engine for: <paramref name="version"/> when it is 1.0, 1.1, 2.0 or 3.0, and
    /// 3.0 for a higher one under <see cref="HttpVersionPolicy.RequestVersionOrLower"/>.
    /// </summary>
    /// <param name="version">The request's version.</param>
    /// <param name="policy">The request's version policy.</param>
    /// <returns>The version to ask for.</returns>
    /// <exception cref="HttpRequestException">No version the engine speaks meets <paramref name="version"/> and <paramref name="policy"/>.</exception>
    public static Version SupportedVersion(Version version, HttpVersionPolicy policy) =>
        IsSpoken(version) ? version
        : version > Highest && policy == HttpVersionPolicy.RequestVersionOrLower ? Highest
        : throw new HttpRequestException(
            HttpRequestError.VersionNegotiationError,
            $"Requesting HTTP version {version} with version policy {policy} is not supported: only 1.0, 1.1, 2.0 and 3.0 are.");

    /// <summary>
    /// Whether the <paramref name="responded"/> version is one <paramref name="policy"/> allows for
    /// <paramref name="requested"/>: the same major version under <see cref="HttpVersionPolicy.RequestVersionExact"/>,
    /// no lower one under <see cref="HttpVersionPolicy.RequestVersionOrHigher"/>, and any under
    /// <see cref="HttpVersionPolicy.RequestVersionOrLower"/>, since the engine never goes above the version
    /// it is asked for. A response with no HTTP version
    /// (<see cref="System.Net.HttpVersion.Unknown"/>) meets every policy.
    /// </summary>
    /// <param name="requested">The version asked for, as <see cref="SupportedVersion"/> gave it.</param>
    /// <param name="policy">The request's version policy.</param>
    /// <param name="responded">The response's version.</param>
    /// <returns><see langword="true"/> when the policy is met.</returns>
    public static bool IsMet(Version requested, HttpVersionPolicy policy, Version responded) =>
        responded == System.Net.HttpVersion.Unknown || Allows(requested.Major, policy, responded.Major);

    /// <summary>The <see cref="HttpRequestError.VersionNegotiationError"/> for a response whose version <see cref="IsMet"/> refuses.</summary>
    /// <param name="requested">The version asked for.</param>
    /// <param name="policy">The request's version policy.</param>
    /// <param name="responded">The response's version.</param>
    /// <returns>The exception to throw.</returns>
    public static HttpRequestException NotMet(Version requested, HttpVersionPolicy policy, Version responded) => new(
        HttpRequestError.VersionNegotiationError,
        $"Requesting HTTP version {requested} with version policy {policy} got an HTTP/{responded} response.");

    /// <summary>Whether <paramref name="policy"/> allows a response of major version <paramref name="responded"/> to a request of <paramref name="requested"/>.</summary>
    /// <param name="requested">The requested major version.</param>
    /// <param name="policy">The version policy.</param>
    /// <param name="responded">The responded major version.</param>
    /// <returns><see langword="true"/> when the policy is met.</returns>
    private static bool Allows(int requested, HttpVersionPolicy policy, int responded) => policy switch
    {
        HttpVersionPolicy.RequestVersionExact => responded == requested,
        HttpVersionPolicy.RequestVersionOrHigher => responded >= requested,
        _ => true,
    };

    /// <summary>Whether the engine speaks <paramref name="version"/>: 1.0, 1.1, 2.0 or 3.0.</summary>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> for a spoken version.</returns>
    private static bool IsSpoken(Version version) =>
        version == System.Net.HttpVersion.Version10
        || version == System.Net.HttpVersion.Version11
        || version == System.Net.HttpVersion.Version20
        || version == System.Net.HttpVersion.Version30;
}
